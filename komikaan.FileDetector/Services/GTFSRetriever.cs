using komikaan.FileDetector.Contexts;
using System.Net;
using System.Collections.Concurrent;
using System.Net.Http.Headers;

namespace komikaan.FileDetector.Services
{
    public class GTFSRetriever : BackgroundService
    {
        private static readonly ConcurrentDictionary<string, string> _etagCache = new ConcurrentDictionary<string, string>();
        private readonly ILogger<GTFSRetriever> _logger;
        private readonly HarvesterContext _harvesterContext;
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private readonly GTFSContext _gtfsContext;
        private string _contactPoint;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _supplierCancellation = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly ConcurrentDictionary<string, Task> _supplierWorkers = new ConcurrentDictionary<string, Task>();

        public GTFSRetriever(ILogger<GTFSRetriever> logger, HarvesterContext harvesterContext, IConfiguration config, HttpClient httpClient, GTFSContext gtfsContext)
        {
            _logger = logger;
            _harvesterContext = harvesterContext;
            _config = config;
            _httpClient = httpClient;
            _gtfsContext = gtfsContext;
            _contactPoint = Environment.GetEnvironmentVariable("Komikaan_ContactPoint") ?? throw new ArgumentNullException("_contactPoint");
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started the gtfs retriever!");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", $"detector/komikaan.nl {GetType().Assembly.GetName().Version} ({_contactPoint})");

            await _harvesterContext.StartAsync(cancellationToken);
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopped the gtfs retriever!");
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            // Start a manager that ensures a worker per supplier runs concurrently
            var managerTask = Task.Run(() => SupplierManagerLoop(cancellationToken), cancellationToken);

            try
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            catch (OperationCanceledException) { }
            finally
            {
                // cancel all supplier workers
                foreach (var cts in _supplierCancellation.Values)
                {
                    try { cts.Cancel(); } catch { }
                }

                try { await Task.WhenAll(_supplierWorkers.Values); } catch { }
                try { await managerTask; } catch { }
            }
        }

        private async Task SupplierManagerLoop(CancellationToken cancellationToken)
        {
            var refresh = _config.GetValue<TimeSpan>("WorkInterval");

            while (!cancellationToken.IsCancellationRequested)
            {
                IEnumerable<DatabaseSupplierConfiguration> suppliers = Array.Empty<DatabaseSupplierConfiguration>();
                try
                {
                    suppliers = await _gtfsContext.GetAllSuppliersAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh suppliers list");
                }

                var supplierNames = new HashSet<string>(suppliers.Select(s => s.Name));

                // start workers for new suppliers
                foreach (var s in suppliers)
                {
                    if (!_supplierWorkers.ContainsKey(s.Name))
                    {
                        var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        if (_supplierCancellation.TryAdd(s.Name, linked))
                        {
                            var task = Task.Run(() => SupplierWorkerLoop(s.Name, linked.Token), linked.Token);
                            _supplierWorkers.TryAdd(s.Name, task);
                            _logger.LogInformation("Started worker for supplier {name}", s.Name);
                        }
                    }
                }

                // stop workers for suppliers that were removed
                var toRemove = _supplierWorkers.Keys.Where(k => !supplierNames.Contains(k)).ToList();
                foreach (var name in toRemove)
                {
                    if (_supplierCancellation.TryRemove(name, out var cts))
                    {
                        try { cts.Cancel(); } catch { }
                    }

                    if (_supplierWorkers.TryRemove(name, out var task))
                    {
                        try { await task; } catch { }
                        _logger.LogInformation("Stopped worker for removed supplier {name}", name);
                    }
                }

                try
                {
                    await Task.Delay(refresh, cancellationToken);
                }
                catch (OperationCanceledException) { }
            }
        }

        private async Task SupplierWorkerLoop(string supplierName, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Worker loop started for {name}", supplierName);

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var suppliers = await _gtfsContext.GetAllSuppliersAsync();
                    var supplier = suppliers.FirstOrDefault(s => s.Name == supplierName);
                    if (supplier == null)
                    {
                        _logger.LogWarning("Supplier {name} not found, exiting worker", supplierName);
                        break;
                    }

                    // ProcessSupplier already checks DownloadPending and PollingRate/LastCheck
                    await ProcessSupplier(supplier, cancellationToken);

                    // Wait for the supplier's configured polling rate (fallback to 1 minute)
                    var delay = supplier.PollingRate != TimeSpan.Zero ? supplier.PollingRate : TimeSpan.FromMinutes(1);
                    await Task.Delay(delay, cancellationToken);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in worker loop for {name}", supplierName);
                    try { await Task.Delay(TimeSpan.FromMinutes(1), cancellationToken); } catch { break; }
                }
            }

            _logger.LogInformation("Worker loop ended for {name}", supplierName);
        }


        private async Task ProcessSuppliers(CancellationToken cancellationToken)
        {
            var supplierConfigurations = await _gtfsContext.GetAllSuppliersAsync();
            _logger.LogInformation("Starting going through suppliers");
            foreach (var supplier in supplierConfigurations)
                using (_logger.BeginScope(supplier.Name))
                {
                    try
                    {
                        _logger.LogInformation("Starting");
                        await ProcessSupplier(supplier, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unknown error while processing supplier");
                    }
                }
            _logger.LogInformation("Finished going through suppliers");
        }

        private async Task ProcessSupplier(DatabaseSupplierConfiguration supplier, CancellationToken cancellationToken)
        {
            if (!supplier.DownloadPending)
            {
                if (supplier.LastCheck == null || DateTime.UtcNow - supplier.LastCheck!.Value.ToUniversalTime() >= supplier.PollingRate)
                {
                    if (supplier.RetrievalType == Common.Enums.RetrievalType.REST)
                    {
                        await ProcessRestSupplier(supplier, cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("Not supported as type was set to {type}, bye!", supplier.RetrievalType);
                    }
                }
                else
                {
                    _logger.LogWarning("Not outside of the interval. Ignoring");
                }
            }
            else
            {
                _logger.LogWarning("A download is pending for this supplier, ignored.");
            }
        }

        private async Task ProcessRestSupplier(DatabaseSupplierConfiguration supplier, CancellationToken cancellationToken)
        {
            // The cancellation token comes from the caller. You can still make a call without it.            
            var request = new HttpRequestMessage(HttpMethod.Get, supplier.Url);

            // Prefer in-memory cached ETag (faster) falling back to DB-stored value
            string? previousEtag = null;
            if (_etagCache.TryGetValue(supplier.Name, out var cached))
            {
                previousEtag = cached;
            }
            else if (!string.IsNullOrEmpty(supplier.ETag))
            {
                previousEtag = supplier.ETag;
            }

            if (!string.IsNullOrEmpty(previousEtag))
            {
                request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(previousEtag));
            }

            // Accept compressed responses
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("br"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));
            request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("deflate"));

            if (!string.IsNullOrWhiteSpace(supplier.HeaderKey)) {
                request.Headers.Add(supplier.HeaderKey, supplier.HeaderValue);
            }

            HttpResponseMessage? response = null;
            try
            {
                _logger.LogInformation("Request generated towards {url}", supplier.Url);
                // This instructs HttpClient to not download the entire content
                response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                _logger.LogInformation("Got response: {status}", response.StatusCode);
                _logger.LogInformation("Got {headers} headers", response.Headers?.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed a supplier call");
            }

            await _gtfsContext.MarkAsCheckedAsync(supplier);
            if (response != null)
            {
                var newETag = response.Headers?.ETag?.Tag;

                if (!string.IsNullOrWhiteSpace(newETag))
                {
                    // update DB model and in-memory cache
                    supplier.ETag = newETag;
                    _etagCache.AddOrUpdate(supplier.Name, newETag, (_, __) => newETag);
                }

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    _logger.LogInformation("The resource has not changed.");
                }
                else if (response.IsSuccessStatusCode)
                {
                    // If we have an ETag, only treat as new when it differs from what we had before.
                    if (!string.IsNullOrEmpty(newETag))
                    {
                        if (string.IsNullOrEmpty(previousEtag) || previousEtag != newETag)
                        {
                            await ProcessNewUpdate(supplier);
                        }
                        else
                        {
                            _logger.LogInformation("ETag unchanged but server returned content; skipping import.");
                        }
                    }
                    else
                    {
                        // Fallback to Last-Modified header when no ETag provided
                        var lastModifiedHeader = response.Content?.Headers?.LastModified;
                        var lastModified = DateTime.UtcNow - TimeSpan.FromHours(24);

                        if (lastModifiedHeader != null)
                        {
                            _logger.LogInformation("Last modified is currently {data}", lastModifiedHeader);
                            lastModified = lastModifiedHeader!.Value.DateTime;
                        }
                        else
                        {
                            _logger.LogInformation("No last modified header found (or it was empty). Assuming last modified is an arbitrary 24 hours ago.");
                        }

                        if (lastModified >= supplier.LastUpdated)
                        {
                            await ProcessNewUpdate(supplier);
                        }
                    }
                }
                else
                {
                    _logger.LogError("Failed, {code} - {phrase}", response.StatusCode, response.ReasonPhrase);
                    await _gtfsContext.MarkAsFailedAsync(supplier);
                }
            }
            
            supplier.LastCheck = DateTimeOffset.UtcNow;
        }

        private async Task ProcessNewUpdate(DatabaseSupplierConfiguration supplier)
        {
            _logger.LogInformation("A new file has been detected! Notifying a harvester");
            supplier.QueuedImportId = Guid.NewGuid();
            supplier.ImportRequestedAt = DateTimeOffset.UtcNow;
            await _gtfsContext.MarkAsPendingAsync(supplier);
            await NotifyHarverster(supplier);
            _logger.LogInformation("Notified a harvester!");
        }

        private async Task NotifyHarverster(DatabaseSupplierConfiguration supplier)
        {
            _logger.LogInformation("Notifying a harvester");
            await _harvesterContext.SendMessageAsync(supplier);
        }
    }
}

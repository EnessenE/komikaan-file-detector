using komikaan.FileDetector.Contexts;
using System.Net;

namespace komikaan.FileDetector.Services
{
    public class GTFSRetriever : BackgroundService
    {
        private readonly ILogger<GTFSRetriever> _logger;
        private readonly HarvesterContext _harvesterContext;
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private readonly GTFSContext _gtfsContext;
        private string _contactPoint;

        public GTFSRetriever(ILogger<GTFSRetriever> logger, HarvesterContext harvesterContext, IConfiguration config, HttpClient httpClient, GTFSContext gtfsContext)
        {
            _logger = logger;
            _harvesterContext = harvesterContext;
            _config = config;
            _httpClient = httpClient;
            _gtfsContext = gtfsContext;
            _contactPoint = Environment.GetEnvironmentVariable("Komikaan_ContactPoint") ?? "enes@reasulus.nl"??throw new ArgumentNullException("_contactPoint");
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
            var interval = _config.GetValue<TimeSpan>("WorkInterval");
            while (!cancellationToken.IsCancellationRequested)
            {
                await ProcessSuppliers(cancellationToken);
                _logger.LogInformation("Finished, waiting for the interval of {time}", interval);
                await Task.Delay(interval, cancellationToken);
            }
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

            if (!string.IsNullOrEmpty(supplier.ETag))
            {
                request.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(supplier.ETag));
            }

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
                    supplier.ETag = newETag;
                }

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    _logger.LogInformation("The resource has not changed.");
                }
                else if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(supplier.ETag))
                {
                    await ProcessNewUpdate(supplier);
                }
                else if (response.IsSuccessStatusCode)
                {
                    var lastModifiedHeader = response.Content?.Headers?.LastModified;

                    var lastModified = DateTime.UtcNow - TimeSpan.FromHours(24);


                    if (lastModifiedHeader != null)
                    {
                        _logger.LogInformation("Last modified is currently {data}", lastModifiedHeader);
                        lastModified = lastModifiedHeader!.Value.DateTime;
                    }
                    else
                    {
                        _logger.LogInformation("No last modified header found (or it was empty). Assuming last modified is an abritrary 24 hours ago.");
                    }

                    if (lastModified >= supplier.LastUpdated)
                    {
                        await ProcessNewUpdate(supplier);
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
            supplier.ImportId = Guid.NewGuid();
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

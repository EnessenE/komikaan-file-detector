using RestSharp;
using komikaan.FileDetector.Contexts;
using komikaan.Common.Models;
using System.Net.Http;
using Azure.Core;
using Microsoft.IdentityModel.Tokens;
using System.Net;

namespace komikaan.FileDetector.Services
{
    public class GTFSRetriever : BackgroundService
    {
        private readonly SupplierContext _supplierContext;
        private readonly ILogger<GTFSRetriever> _logger;
        private readonly HarvesterContext _harvesterContext;
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public GTFSRetriever(ILogger<GTFSRetriever> logger, SupplierContext supplierContext, HarvesterContext harvesterContext, IConfiguration config, HttpClient httpClient)
        {
            _logger = logger;
            _supplierContext = supplierContext;
            _harvesterContext = harvesterContext;
            _config = config;
            _httpClient = httpClient;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started the gtfs retriever!");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "detector/reasulus.nl");

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

                _logger.LogInformation("Starting a process cycle");
                await ReloadItemsAsync();
                _logger.LogInformation("Sync complete");
                await ProcessSuppliers(cancellationToken);
                _logger.LogInformation("Finished, waiting for the interval of {time}", interval);
                await Task.Delay(interval, cancellationToken);
            }
        }

        private async Task ReloadItemsAsync()
        {
            _logger.LogInformation("Reloading info");
            var entitiesList = _supplierContext.ChangeTracker.Entries().ToList();
            foreach (var entity in entitiesList)
            {
                await entity.ReloadAsync();
            }
            _logger.LogInformation("Reloaded info");
        }

        private async Task ProcessSuppliers(CancellationToken cancellationToken)
        {
            var supplierConfigurations = GetSupplierConfigs();
            _logger.LogInformation("Starting going through suppliers");
            foreach (var supplier in supplierConfigurations)
                using (_logger.BeginScope(supplier.Name))
                {
                    try
                    {
                        await ProcessSupplier(supplier, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unknown error while processing supplier");
                    }
                }
            _logger.LogInformation("Finished going through suppliers");
        }

        private async Task ProcessSupplier(SupplierConfiguration supplier, CancellationToken cancellationToken)
        {
            if (!supplier.DownloadPending)
            {
                if (supplier.LastChecked == null || DateTime.UtcNow - supplier.LastChecked!.Value.ToUniversalTime() >= supplier.PollingRate)
                {
                    if (supplier.RetrievalType == Common.Enums.RetrievalType.REST)
                    {
                        await ProcessRestSupplier(supplier, cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("Not supported, bye!");
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

        private async Task ProcessRestSupplier(SupplierConfiguration supplier, CancellationToken cancellationToken)
        {
            // The cancellation token comes from the caller. You can still make a call without it.            
            var request = new HttpRequestMessage(HttpMethod.Get, supplier.Url);

            if (!string.IsNullOrEmpty(supplier.ETag))
            {
                request.Headers.IfNoneMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue(supplier.ETag));
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

            if (response != null)
            {

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    _logger.LogInformation("The resource has not changed.");
                }
                else if (response.IsSuccessStatusCode && !string.IsNullOrEmpty(supplier.ETag))
                {
                    await NotifyHarvester(supplier);
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
                        await NotifyHarvester(supplier);
                    }
                }
                else
                {
                    _logger.LogError("Failed, {code} - {phrase}", response.StatusCode, response.ReasonPhrase);
                }

                var newETag = response.Headers?.ETag?.Tag;
                if (!string.IsNullOrWhiteSpace(newETag))
                {
                    supplier.ETag = newETag;
                }
            }
            
            supplier.LastChecked = DateTimeOffset.UtcNow;
            await _supplierContext.SaveChangesAsync();
        }

        private async Task NotifyHarvester(SupplierConfiguration supplier)
        {
            supplier.ImportId = Guid.NewGuid();
            _logger.LogInformation("A new file has been detected! Notifying a harvester");
            await NotifyHarverster(supplier);
            supplier.DownloadPending = true;
            _logger.LogInformation("Notified a harvester!");
        }

        private IEnumerable<SupplierConfiguration> GetSupplierConfigs()
        {
            return _supplierContext.SupplierConfigurations.ToList();
        }

        private async Task NotifyHarverster(SupplierConfiguration supplier)
        {
            _logger.LogInformation("Notifying a harverster");
            await _harvesterContext.SendMessageAsync(supplier);
        }
    }
}

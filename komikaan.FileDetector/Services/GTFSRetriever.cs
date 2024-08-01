using RestSharp;
using komikaan.FileDetector.Contexts;
using komikaan.Common.Models;
using Z.EntityFramework.Plus;

namespace komikaan.FileDetector.Services
{
    public class GTFSRetriever : BackgroundService
    {
        private readonly SupplierContext _supplierContext;
        private readonly ILogger<GTFSRetriever> _logger;
        private readonly HarvesterContext _harvesterContext;
        private IConfiguration _config;

        public GTFSRetriever(ILogger<GTFSRetriever> logger, SupplierContext supplierContext, HarvesterContext harvesterContext, IConfiguration config)
        {
            _logger = logger;
            _supplierContext = supplierContext;
            _harvesterContext = harvesterContext;
            _config = config;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started the gtfs retriever!");
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
            while (true)
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
                    catch(Exception ex)
                    {
                        _logger.LogError(ex, "Unknown error while processing supplier");
                    }
                }
            _logger.LogInformation("Finished going through suppliers");
        }

        private async Task ProcessSupplier(SupplierConfiguration supplier, CancellationToken cancellationToken)
        {
            if (!supplier.DownloadPending )
            {
                if (DateTime.UtcNow - supplier.LastUpdated.ToUniversalTime() >= supplier.PollingRate)
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
            var options = new RestClientOptions(supplier.Url);
            var client = new RestClient(options);
            var request = new RestRequest() { Method = Method.Head };
            _logger.LogInformation("Request generated towards {url}", supplier.Url);
            // The cancellation token comes from the caller. You can still make a call without it.
            var response = await client.ExecuteAsync(request, cancellationToken);

            _logger.LogInformation("Got response: {status}", response.StatusCode);
            _logger.LogInformation("Got {headers}/{contentHeaders} headers", response.Headers?.Count, response.ContentHeaders?.Count);
            var lastModifiedHeader = response.ContentHeaders?.FirstOrDefault(header => string.Equals(header.Name, "last-modified", StringComparison.InvariantCultureIgnoreCase));

            if (response.IsSuccessStatusCode)
            {
                var lastModified = DateTime.UtcNow - TimeSpan.FromHours(6);


                if (lastModifiedHeader != null && lastModifiedHeader.Value != null)
                {
                    _logger.LogInformation("Last modified is currently {data}", DateTime.Parse(lastModifiedHeader.Value.ToString()));
                    lastModified = DateTime.Parse(lastModifiedHeader.Value.ToString());
                }
                else
                {
                    _logger.LogInformation("No last modified header found (or it was empty). Assuming last modified is an abritrary 6 hours ago.");
                }

                if (lastModified >= supplier.LastUpdated)
                {
                    supplier.ImportId = Guid.NewGuid();
                    _logger.LogInformation("A new file has been detected! Notifying a harvester");
                    await NotifyHarverster(supplier);
                    supplier.DownloadPending = true;
                    await _supplierContext.SaveChangesAsync();
                    _logger.LogInformation("Notified a harvester!");
                }
            }
            else
            {
                _logger.LogError(response.ErrorException, "Call failed");
            }
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

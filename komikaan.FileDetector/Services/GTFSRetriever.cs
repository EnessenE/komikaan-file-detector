using RestSharp;
using komikaan.FileDetector.Models;
using komikaan.FileDetector.Contexts;

namespace komikaan.FileDetector.Services
{
    public class GTFSRetriever : BackgroundService
    {
        private readonly SupplierContext _supplierContext;
        private readonly ILogger<GTFSRetriever> _logger;
        private readonly HarvesterContext _harvesterContext;

        public GTFSRetriever(ILogger<GTFSRetriever> logger, SupplierContext supplierContext, HarvesterContext harvesterContext)
        {
            _logger = logger;
            _supplierContext = supplierContext;
            _harvesterContext = harvesterContext;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Staretd the gtfs retriever!");
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
            var supplierConfigurations = GetSupplierConfigs();
            _logger.LogInformation("Starting going through suppliers");
            foreach (var supplier in supplierConfigurations)
                using (_logger.BeginScope(supplier.Name))
                {
                    if (supplier.RetrievalType == Enums.RetrievalType.REST)
                    {
                        var options = new RestClientOptions(supplier.Url);
                        var client = new RestClient(options);
                        var request = new RestRequest() { Method = Method.Head};
                        _logger.LogInformation("Request generated towards {url}", supplier.Url);
                        // The cancellation token comes from the caller. You can still make a call without it.
                        var response = await client.ExecuteAsync(request, cancellationToken);

                        _logger.LogInformation("Got response: {status}", response.StatusCode);
                        _logger.LogInformation("Got {headers}/{contentHeaders} headers", response.Headers?.Count, response.ContentHeaders?.Count);
                        var lastModifiedHeader = response.ContentHeaders?.FirstOrDefault(header => string.Equals(header.Name, "last-modified", StringComparison.InvariantCultureIgnoreCase));

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
                            _logger.LogInformation("A new file has been detected!");
                            await NotifyHarverster(supplier);

                        }
                    }
                    else
                    {
                        _logger.LogWarning("Not supported, bye!");
                    }
                }
            _logger.LogInformation("Finished going through suppliers");
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

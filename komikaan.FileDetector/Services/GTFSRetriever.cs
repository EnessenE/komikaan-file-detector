using ProtoBuf;
using RestSharp.Authenticators;
using RestSharp;
using System.Net;
using TransitRealtime;
using NetTopologySuite.Index.HPRtree;
using komikaan.FileDetector.Models;
using System.Threading;
using komikaan.FileDetector.Contexts;

namespace komikaan.FileDetector.Services
{
    public class GTFSRetriever : BackgroundService
    {
        private readonly SupplierContext _supplierContext;
        private ILogger<GTFSRetriever> _logger;

        public GTFSRetriever(ILogger<GTFSRetriever> logger, SupplierContext supplierContext)
        {
            _logger = logger;
            _supplierContext = supplierContext;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await base.StartAsync(cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Started the gtfs retriever!");
            return Task.CompletedTask;
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var supplierConfigurations = GetSupplierConfigs();   
            _logger.LogInformation("Starting going through suppliers");
            foreach (var supplier in supplierConfigurations)
                using (_logger.BeginScope(supplier.Name))
                {
                    var options = new RestClientOptions("https://api.reasulus.nl/v1/stats/all");
                    var client = new RestClient(options);
                    var request = new RestRequest();
                    _logger.LogInformation("Request generated towards {url}", supplier.Url);
                    // The cancellation token comes from the caller. You can still make a call without it.
                    var response = await client.GetAsync(request, cancellationToken);

                    var lastModified = response.Headers?.FirstOrDefault(header => string.Equals(header.Name, "last-modified", StringComparison.InvariantCultureIgnoreCase));


                    if (lastModified != null && lastModified.Value != null)
                    {
                        _logger.LogInformation("Last modified is currently {data}", DateTime.Parse(lastModified.Value.ToString()));
                    }
                    else
                    {
                        _logger.LogInformation("No last modified header found (or it was empty). Assuming its been updated");
                    }

                    _logger.LogInformation("Got a response!");
                    _logger.LogInformation("Code: {0}", response.StatusCode);
                    await NotifyHarverster(supplier);
                }
            _logger.LogInformation("Finished going through suppliers");
        }

        private IEnumerable<SupplierConfiguration> GetSupplierConfigs()
        {
            return _supplierContext.SupplierConfigurations.ToList();
        }

        private Task NotifyHarverster(SupplierConfiguration supplier)
        {
            _logger.LogWarning("Harverster implementation not created. Please fix.");
            return Task.CompletedTask;
        }
    }
}

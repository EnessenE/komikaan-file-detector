using Dapper;
using komikaan.Common.Models;
using Npgsql;
using System.Data;

namespace komikaan.FileDetector.Contexts
{
    public class GTFSContext
    {
        private readonly NpgsqlDataSource _dataSource;
        private readonly ILogger<GTFSContext> _logger;

        public GTFSContext(IConfiguration configuration, ILogger<GTFSContext> logger)
        {
            _logger = logger;
            // Get the connection string from configuration
            var connectionString = configuration.GetConnectionString("gtfs");

            // Build the NpgsqlDataSource
            var builder = new NpgsqlDataSourceBuilder(connectionString);

            // Build the NpgsqlDataSource
            _dataSource = builder.Build();
        }

        public async Task MarkAsPendingAsync(SupplierConfiguration config)
        {
            using var dbConnection = _dataSource.CreateConnection();

            await dbConnection.ExecuteAsync(
             @"CALL public.filedetector_mark_pending(@data_origin, @state)",
                new
                {
                    data_origin = config.Name,
                    state = "Import pending"
                },
                 commandType: CommandType.Text
             );
        }
        public async Task MarkAsFailedAsync(SupplierConfiguration config)
        {
            using var dbConnection = _dataSource.CreateConnection();

            await dbConnection.ExecuteAsync(
             @"CALL public.filedetector_mark_failed(@data_origin, @state)",
                new
                {
                    data_origin = config.Name,
                    state = "Couldn't find a file"
                },
                 commandType: CommandType.Text
             );
        }
    }
}

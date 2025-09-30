using Dapper;
using komikaan.Common.Enums;
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
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

            _logger = logger;
            // Get the connection string from configuration
            var connectionString = configuration.GetConnectionString("gtfs");

            // Build the NpgsqlDataSource
            var builder = new NpgsqlDataSourceBuilder(connectionString);

            SqlMapper.AddTypeHandler(new IntEnumHandler<RetrievalType>());
            SqlMapper.AddTypeHandler(new IntEnumHandler<SupplierType>());

            // Build the NpgsqlDataSource
            _dataSource = builder.Build();
        }

        public async Task MarkAsPendingAsync(DatabaseSupplierConfiguration config)
        {
            using var dbConnection = _dataSource.CreateConnection();

            await dbConnection.ExecuteAsync(
             @"CALL public.filedetector_mark_pending(@data_origin, @state, @uuid, @etag)",
                new
                {
                    data_origin = config.Name,
                    state = "Import pending",
                    uuid = config.ImportId,
                    etag = config.ETag
                },
                 commandType: CommandType.Text
             );
        }
        public async Task MarkAsFailedAsync(DatabaseSupplierConfiguration config)
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


        public async Task MarkAsCheckedAsync(DatabaseSupplierConfiguration config)
        {
            using var dbConnection = _dataSource.CreateConnection();

            await dbConnection.ExecuteAsync(
             @"CALL public.filedetector_mark_checked(@data_origin)",
                new
                {
                    data_origin = config.Name,
                },
                 commandType: CommandType.Text
             );
        }

        internal async Task<IEnumerable<DatabaseSupplierConfiguration>> GetAllSuppliersAsync()
        {
            using var dbConnection = _dataSource.CreateConnection();

            return await dbConnection.QueryAsync<DatabaseSupplierConfiguration>(@"SELECT * FROM public.filedetector_get_all_suppliers()");
        }
    }
}

public class IntEnumHandler<T> : SqlMapper.TypeHandler<T> where T : struct, Enum
{
    public override void SetValue(IDbDataParameter parameter, T value)
        => parameter.Value = Convert.ToInt32(value);

    public override T Parse(object value)
    {
        if (value is int intValue)
            return (T)Enum.ToObject(typeof(T), intValue);

        // handle nullable DB values if needed
        if (value is long longValue)
            return (T)Enum.ToObject(typeof(T), (int)longValue);

        throw new DataException($"Cannot convert {value} to enum {typeof(T)}");
    }
}
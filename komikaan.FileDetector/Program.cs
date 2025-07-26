using komikaan.FileDetector.Contexts;
using komikaan.FileDetector.Services;
using Serilog;
using System.Reflection;

namespace komikaan.FileDetector
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Configuration.AddEnvironmentVariables();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(builder.Configuration)
                .CreateLogger();

            builder.Host.UseSerilog();
            Log.Logger.Information("Starting {app} {version} - {env}",
                Assembly.GetExecutingAssembly().GetName().Name,
                Assembly.GetExecutingAssembly().GetName().Version,
                builder.Environment.EnvironmentName);

            // Add services to the container.

            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            builder.Services.AddSingleton<GTFSContext>();

            AddSuppliers(builder.Services);

            builder.Services.AddHttpClient();


            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);


            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();
            app.UseSerilogRequestLogging();

            app.MapControllers();

            app.Run();
        }

        private static void AddSuppliers(IServiceCollection serviceCollection)
        {
            serviceCollection.AddHostedService<GTFSRetriever>();
            serviceCollection.AddSingleton<HarvesterContext>();
        }
    }
}

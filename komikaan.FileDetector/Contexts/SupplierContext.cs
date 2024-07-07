using komikaan.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace komikaan.FileDetector.Contexts
{
    public class SupplierContext : DbContext
    {
        public DbSet<SupplierConfiguration> SupplierConfigurations { get; set; }


        public SupplierContext(DbContextOptions<SupplierContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("postgis");
        }
        protected void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder.UseNpgsql("Host=my_host;Database=my_db;Username=my_user;Password=my_pw");
    }

}

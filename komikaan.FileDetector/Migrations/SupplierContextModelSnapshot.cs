﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using komikaan.FileDetector.Contexts;

#nullable disable

namespace komikaan.FileDetector.Migrations
{
    [DbContext(typeof(SupplierContext))]
    partial class SupplierContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.6")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.HasPostgresExtension(modelBuilder, "postgis");
            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("komikaan.FileDetector.Models.SupplierConfiguration", b =>
                {
                    b.Property<string>("Name")
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.Property<int>("DataType")
                        .HasColumnType("integer")
                        .HasColumnName("data_type");

                    b.Property<DateTime>("LastUpdated")
                        .HasColumnType("timestamp without time zone")
                        .HasColumnName("last_updated");

                    b.Property<TimeSpan>("PollingRate")
                        .HasColumnType("interval")
                        .HasColumnName("polling_rate");

                    b.Property<int>("RetrievalType")
                        .HasColumnType("integer")
                        .HasColumnName("retrieval_type");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("url");

                    b.HasKey("Name")
                        .HasName("pk_supplier_configurations");

                    b.ToTable("supplier_configurations", (string)null);
                });
#pragma warning restore 612, 618
        }
    }
}

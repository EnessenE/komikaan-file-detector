using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace komikaan.FileDetector.Migrations
{
    /// <inheritdoc />
    public partial class GeneralDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "supplier_configurations",
                columns: table => new
                {
                    name = table.Column<string>(type: "text", nullable: false),
                    retrieval_type = table.Column<int>(type: "integer", nullable: false),
                    data_type = table.Column<int>(type: "integer", nullable: false),
                    polling_rate = table.Column<TimeSpan>(type: "interval", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    last_updated = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_configurations", x => x.name);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_configurations");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace komikaan.FileDetector.Migrations
{
    /// <inheritdoc />
    public partial class AddLastUpdated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_attempt",
                table: "supplier_configurations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_attempt",
                table: "supplier_configurations");
        }
    }
}

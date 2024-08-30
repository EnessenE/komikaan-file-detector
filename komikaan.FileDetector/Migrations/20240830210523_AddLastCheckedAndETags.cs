using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace komikaan.FileDetector.Migrations
{
    /// <inheritdoc />
    public partial class AddLastCheckedAndETags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "e_tag",
                table: "supplier_configurations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_checked",
                table: "supplier_configurations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "e_tag",
                table: "supplier_configurations");

            migrationBuilder.DropColumn(
                name: "last_checked",
                table: "supplier_configurations");
        }
    }
}

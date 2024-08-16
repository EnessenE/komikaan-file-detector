using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace komikaan.FileDetector.Migrations
{
    /// <inheritdoc />
    public partial class AddLastUpdatedGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "latest_succesfull_import_id",
                table: "supplier_configurations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "latest_succesfull_import_id",
                table: "supplier_configurations");
        }
    }
}

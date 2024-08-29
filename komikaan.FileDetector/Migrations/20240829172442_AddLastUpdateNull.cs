using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace komikaan.FileDetector.Migrations
{
    /// <inheritdoc />
    public partial class AddLastUpdateNull : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_type_mapping");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "last_attempt",
                table: "supplier_configurations",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "last_attempt",
                table: "supplier_configurations",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)),
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "supplier_type_mapping",
                columns: table => new
                {
                    listed_type = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    new_type = table.Column<int>(type: "integer", nullable: false),
                    supplier_configuration_name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_supplier_type_mapping", x => x.listed_type);
                    table.ForeignKey(
                        name: "fk_supplier_type_mapping_supplier_configurations_supplier_conf",
                        column: x => x.supplier_configuration_name,
                        principalTable: "supplier_configurations",
                        principalColumn: "name");
                });

            migrationBuilder.CreateIndex(
                name: "ix_supplier_type_mapping_supplier_configuration_name",
                table: "supplier_type_mapping",
                column: "supplier_configuration_name");
        }
    }
}

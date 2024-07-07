using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace komikaan.FileDetector.Migrations
{
    /// <inheritdoc />
    public partial class Mappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "supplier_type_mapping");
        }
    }
}

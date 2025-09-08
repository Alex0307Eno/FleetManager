using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cars.Migrations
{
    public partial class AddCarRouteStops : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CarRouteStops",
                columns: table => new
                {
                    StopId = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApplyId = table.Column<int>(nullable: false),
                    OrderNo = table.Column<int>(nullable: false),
                    // 若允許空值，這兩行改 nullable: true
                    Place = table.Column<string>(nullable: false),
                    Address = table.Column<string>(nullable: false),
                    Lat = table.Column<decimal>(type: "decimal(9,6)", nullable: true),
                    Lng = table.Column<decimal>(type: "decimal(9,6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarRouteStops", x => x.StopId);
                    table.ForeignKey(
                        name: "FK_CarRouteStops_CarApplications_ApplyId",
                        column: x => x.ApplyId,
                        principalTable: "CarApplications",
                        principalColumn: "ApplyId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarRouteStops_ApplyId",
                table: "CarRouteStops",
                column: "ApplyId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CarRouteStops");
        }
    }
}

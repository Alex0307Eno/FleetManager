using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cars.Migrations
{
    /// <inheritdoc />
    public partial class FixDelegationAgentToDriver : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DriverDelegations_DriverAgents_AgentId",
                table: "DriverDelegations");

            migrationBuilder.DropTable(
                name: "RepairRequests");

            migrationBuilder.AlterColumn<int>(
                name: "Capacity",
                table: "Vehicles",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<bool>(
                name: "IsPresent",
                table: "Schedules",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "AgentId",
                table: "DriverDelegations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "DriverDelegations",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "DriverAgentAgentId",
                table: "DriverDelegations",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLongTrip",
                table: "Dispatches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "isLongTrip",
                table: "CarApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "VehicleInspections",
                columns: table => new
                {
                    InspectionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    InspectionDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Station = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NextDueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OdometerKm = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleInspections", x => x.InspectionId);
                    table.ForeignKey(
                        name: "FK_VehicleInspections_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VehicleRepairs",
                columns: table => new
                {
                    RepairRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    PlateNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Place = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Issue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CostEstimate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Vendor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleRepairs", x => x.RepairRequestId);
                });

            migrationBuilder.CreateTable(
                name: "VehicleViolations",
                columns: table => new
                {
                    ViolationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    ViolationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Points = table.Column<int>(type: "int", nullable: true),
                    FineAmount = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaidDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VehicleViolations", x => x.ViolationId);
                    table.ForeignKey(
                        name: "FK_VehicleViolations_Vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "Vehicles",
                        principalColumn: "VehicleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DriverDelegations_DriverAgentAgentId",
                table: "DriverDelegations",
                column: "DriverAgentAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleInspections_VehicleId",
                table: "VehicleInspections",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_VehicleViolations_VehicleId",
                table: "VehicleViolations",
                column: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_DriverDelegations_DriverAgents_DriverAgentAgentId",
                table: "DriverDelegations",
                column: "DriverAgentAgentId",
                principalTable: "DriverAgents",
                principalColumn: "AgentId");

            migrationBuilder.AddForeignKey(
                name: "FK_DriverDelegations_Drivers_AgentId",
                table: "DriverDelegations",
                column: "AgentId",
                principalTable: "Drivers",
                principalColumn: "DriverId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DriverDelegations_DriverAgents_DriverAgentAgentId",
                table: "DriverDelegations");

            migrationBuilder.DropForeignKey(
                name: "FK_DriverDelegations_Drivers_AgentId",
                table: "DriverDelegations");

            migrationBuilder.DropTable(
                name: "VehicleInspections");

            migrationBuilder.DropTable(
                name: "VehicleRepairs");

            migrationBuilder.DropTable(
                name: "VehicleViolations");

            migrationBuilder.DropIndex(
                name: "IX_DriverDelegations_DriverAgentAgentId",
                table: "DriverDelegations");

            migrationBuilder.DropColumn(
                name: "IsPresent",
                table: "Schedules");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "DriverDelegations");

            migrationBuilder.DropColumn(
                name: "DriverAgentAgentId",
                table: "DriverDelegations");

            migrationBuilder.DropColumn(
                name: "IsLongTrip",
                table: "Dispatches");

            migrationBuilder.DropColumn(
                name: "isLongTrip",
                table: "CarApplications");

            migrationBuilder.AlterColumn<int>(
                name: "Capacity",
                table: "Vehicles",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AgentId",
                table: "DriverDelegations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "RepairRequests",
                columns: table => new
                {
                    RepairRequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CostEstimate = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Issue = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PlateNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VehicleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepairRequests", x => x.RepairRequestId);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_DriverDelegations_DriverAgents_AgentId",
                table: "DriverDelegations",
                column: "AgentId",
                principalTable: "DriverAgents",
                principalColumn: "AgentId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

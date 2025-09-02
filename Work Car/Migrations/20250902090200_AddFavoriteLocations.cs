using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cars.Migrations
{
    /// <inheritdoc />
    public partial class AddFavoriteLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DriverDelegations_Drivers_AgentDriverId",
                table: "DriverDelegations");

            migrationBuilder.DropForeignKey(
                name: "FK_DriverDelegations_Drivers_PrincipalDriverId",
                table: "DriverDelegations");

            migrationBuilder.DropTable(
                name: "MaintenanceRecords");

            migrationBuilder.DropColumn(
                name: "Odometer",
                table: "RepairRequests");

            migrationBuilder.DropColumn(
                name: "Vendor",
                table: "RepairRequests");

            migrationBuilder.DropColumn(
                name: "ApplicantBirth",
                table: "CarApplications");

            migrationBuilder.DropColumn(
                name: "ApplicantDept",
                table: "CarApplications");

            migrationBuilder.DropColumn(
                name: "ApplicantEmail",
                table: "CarApplications");

            migrationBuilder.DropColumn(
                name: "ApplicantExt",
                table: "CarApplications");

            migrationBuilder.DropColumn(
                name: "ApplicantName",
                table: "CarApplications");

            migrationBuilder.DropColumn(
                name: "Seats",
                table: "CarApplications");

            migrationBuilder.RenameColumn(
                name: "AgentDriverId",
                table: "DriverDelegations",
                newName: "AgentId");

            migrationBuilder.RenameIndex(
                name: "IX_DriverDelegations_AgentDriverId",
                table: "DriverDelegations",
                newName: "IX_DriverDelegations_AgentId");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalNo",
                table: "Vehicles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EngineCC",
                table: "Vehicles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EngineNo",
                table: "Vehicles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InspectionDate",
                table: "Vehicles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LicenseDate",
                table: "Vehicles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PurchaseDate",
                table: "Vehicles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Retired",
                table: "Vehicles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "Vehicles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartUseDate",
                table: "Vehicles",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Vehicles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Value",
                table: "Vehicles",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "Vehicles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "Drivers",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "DriverDelegations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "PrincipalDriverId",
                table: "DriverDelegations",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<int>(
                name: "ApplicantId",
                table: "DispatchOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "CarPassengers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Origin",
                table: "CarApplications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Destination",
                table: "CarApplications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApplicantId",
                table: "CarApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "VehicleId",
                table: "CarApplications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DriverAgents",
                columns: table => new
                {
                    AgentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgentName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NationalId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BirthDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HouseholdAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ContactAddress = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Mobile = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmergencyContactName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EmergencyContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DriverAgents", x => x.AgentId);
                });

            migrationBuilder.CreateTable(
                name: "FavoriteLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CustomName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PlaceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Lat = table.Column<double>(type: "float", nullable: true),
                    Lng = table.Column<double>(type: "float", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FavoriteLocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Account = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "Applicants",
                columns: table => new
                {
                    ApplicantId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Birth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Dept = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Ext = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Applicants", x => x.ApplicantId);
                    table.ForeignKey(
                        name: "FK_Applicants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarApplications_ApplicantId",
                table: "CarApplications",
                column: "ApplicantId");

            migrationBuilder.CreateIndex(
                name: "IX_CarApplications_VehicleId",
                table: "CarApplications",
                column: "VehicleId");

            migrationBuilder.CreateIndex(
                name: "IX_Applicants_UserId",
                table: "Applicants",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CarApplications_Applicants_ApplicantId",
                table: "CarApplications",
                column: "ApplicantId",
                principalTable: "Applicants",
                principalColumn: "ApplicantId");

            migrationBuilder.AddForeignKey(
                name: "FK_CarApplications_Vehicles_VehicleId",
                table: "CarApplications",
                column: "VehicleId",
                principalTable: "Vehicles",
                principalColumn: "VehicleId");

            migrationBuilder.AddForeignKey(
                name: "FK_DriverDelegations_DriverAgents_AgentId",
                table: "DriverDelegations",
                column: "AgentId",
                principalTable: "DriverAgents",
                principalColumn: "AgentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DriverDelegations_Drivers_PrincipalDriverId",
                table: "DriverDelegations",
                column: "PrincipalDriverId",
                principalTable: "Drivers",
                principalColumn: "DriverId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CarApplications_Applicants_ApplicantId",
                table: "CarApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_CarApplications_Vehicles_VehicleId",
                table: "CarApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_DriverDelegations_DriverAgents_AgentId",
                table: "DriverDelegations");

            migrationBuilder.DropForeignKey(
                name: "FK_DriverDelegations_Drivers_PrincipalDriverId",
                table: "DriverDelegations");

            migrationBuilder.DropTable(
                name: "Applicants");

            migrationBuilder.DropTable(
                name: "DriverAgents");

            migrationBuilder.DropTable(
                name: "FavoriteLocations");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropIndex(
                name: "IX_CarApplications_ApplicantId",
                table: "CarApplications");

            migrationBuilder.DropIndex(
                name: "IX_CarApplications_VehicleId",
                table: "CarApplications");

            migrationBuilder.DropColumn(
                name: "ApprovalNo",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "EngineCC",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "EngineNo",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "InspectionDate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "LicenseDate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "PurchaseDate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Retired",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "StartUseDate",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "ApplicantId",
                table: "DispatchOrders");

            migrationBuilder.DropColumn(
                name: "ApplicantId",
                table: "CarApplications");

            migrationBuilder.DropColumn(
                name: "VehicleId",
                table: "CarApplications");

            migrationBuilder.RenameColumn(
                name: "AgentId",
                table: "DriverDelegations",
                newName: "AgentDriverId");

            migrationBuilder.RenameIndex(
                name: "IX_DriverDelegations_AgentId",
                table: "DriverDelegations",
                newName: "IX_DriverDelegations_AgentDriverId");

            migrationBuilder.AddColumn<int>(
                name: "Odometer",
                table: "RepairRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vendor",
                table: "RepairRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "DriverDelegations",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<int>(
                name: "PrincipalDriverId",
                table: "DriverDelegations",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "CarPassengers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Origin",
                table: "CarApplications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Destination",
                table: "CarApplications",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "ApplicantBirth",
                table: "CarApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantDept",
                table: "CarApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantEmail",
                table: "CarApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantExt",
                table: "CarApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantName",
                table: "CarApplications",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Seats",
                table: "CarApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "MaintenanceRecords",
                columns: table => new
                {
                    MaintenanceRecordId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Item = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Odometer = table.Column<int>(type: "int", nullable: true),
                    PlateNo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Qty = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VehicleId = table.Column<int>(type: "int", nullable: false),
                    Vendor = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceRecords", x => x.MaintenanceRecordId);
                });

            migrationBuilder.AddForeignKey(
                name: "FK_DriverDelegations_Drivers_AgentDriverId",
                table: "DriverDelegations",
                column: "AgentDriverId",
                principalTable: "Drivers",
                principalColumn: "DriverId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DriverDelegations_Drivers_PrincipalDriverId",
                table: "DriverDelegations",
                column: "PrincipalDriverId",
                principalTable: "Drivers",
                principalColumn: "DriverId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

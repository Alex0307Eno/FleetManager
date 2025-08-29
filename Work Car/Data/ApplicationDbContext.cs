using Microsoft.EntityFrameworkCore;
using Cars.Models;

namespace Cars.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Applicant> Applicants { get; set; }
        public DbSet<DispatchAdmin> DispatchAdmins { get; set; }

        public DbSet<CarApply> CarApplications { get; set; }
        public DbSet<CarPassenger> CarPassengers { get; set; }
        public DbSet<Cars.Models.Vehicle> Vehicles { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<Cars.Models.Dispatch> Dispatches { get; set; }
        public DbSet<DispatchOrder> DispatchOrders { get; set; }
        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<DriverDelegation> DriverDelegations { get; set; }
        public DbSet<DriverAgent> DriverAgents { get; set; }

        public DbSet<VehicleMaintenance> VehicleMaintenances { get; set; }
        public DbSet<FuelFillUp> FuelFillUps { get; set; }
        public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }
        public DbSet<RepairRequest> RepairRequests { get; set; }

        public DbSet<PlaceAlias> PlaceAliases { get; set; }

    }
}

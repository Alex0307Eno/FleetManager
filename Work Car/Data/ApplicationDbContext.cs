using Cars.Models;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.EntityFrameworkCore;

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

        public DbSet<CarApplication> CarApplications { get; set; }
        public DbSet<CarPassenger> CarPassengers { get; set; }
        public DbSet<Cars.Models.Vehicle> Vehicles { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<Cars.Models.Dispatch> Dispatches { get; set; }
        public DbSet<DispatchOrder> v_DispatchOrders { get; set; }
        public DbSet<Schedule> Schedules { get; set; }
        public DbSet<DriverDelegation> DriverDelegations { get; set; }

        public DbSet<FuelFillUp> FuelFillUps { get; set; }
        public DbSet<VehicleMaintenance> VehicleMaintenances { get; set; }
        public DbSet<VehicleRepair> VehicleRepairs { get; set; }
        public DbSet<VehicleInspection> VehicleInspections { get; set; }
        public DbSet<VehicleViolation> VehicleViolations { get; set; }

        public DbSet<FavoriteLocation> FavoriteLocations { get; set; }
        public DbSet<DispatchLink> DispatchLinks { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<DispatchLink>()
                .HasKey(dl => new { dl.ParentDispatchId, dl.ChildDispatchId });

            modelBuilder.Entity<DispatchLink>()
                .HasOne(dl => dl.ParentDispatch)
                .WithMany(d => d.ChildLinks)
                .HasForeignKey(dl => dl.ParentDispatchId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DispatchLink>()
                .HasOne(dl => dl.ChildDispatch)
                .WithMany(d => d.ParentLinks)
                .HasForeignKey(dl => dl.ChildDispatchId)
                .OnDelete(DeleteBehavior.Restrict);
        }


    }
}

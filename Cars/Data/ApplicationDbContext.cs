using Cars.Models;
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
        public DbSet<DriverLineAssignment> DriverLineAssignments { get; set; }
        public DbSet<ResolvedSchedule> ResolvedSchedules { get; set; } // 對應 View

        public DbSet<LineUser> LineUsers { get; set; }

        public DbSet<Leave> Leaves { get; set; } // 請假資料表

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

            modelBuilder.Entity<ResolvedSchedule>()
       .ToView("v_ScheduleResolved")
       .HasNoKey(); // 沒主鍵的 View 一定要這樣設

            modelBuilder.Entity<DispatchOrder>()
                .ToView("v_DispatchOrders")
                .HasNoKey();

            // ==== Schedules：唯一鍵與長度 ====
            modelBuilder.Entity<Schedule>(e =>
            {
                e.HasIndex(s => new { s.WorkDate, s.Shift }).IsUnique(); // 一天每個班別唯一
                e.Property(s => s.Shift).HasMaxLength(10).IsRequired();
                e.Property(s => s.LineCode).HasMaxLength(1).IsRequired(); // A~E
            });

            // ==== DriverLineAssignment：欄位與檢查 ====
            modelBuilder.Entity<DriverLineAssignment>(e =>
            {
                e.Property(x => x.LineCode).HasMaxLength(1).IsRequired();
                e.HasCheckConstraint("CK_AssignmentDate", "(EndDate IS NULL OR EndDate >= StartDate)");
            });

        }


    }
}

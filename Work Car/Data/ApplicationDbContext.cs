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

        // 這裡放你的資料表，舉例 CarApply
        public DbSet<CarApply> CarApplications { get; set; }
        public DbSet<CarPassenger> CarPassengers { get; set; }
        public DbSet<Vehicle> Vehicles { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<Dispatch> Dispatches { get; set; }
        public DbSet<DispatchOrder> DispatchOrders { get; set; }
    }
}

using Cars.Data;
using Microsoft.EntityFrameworkCore;

namespace Cars.Services
{
    public class VehicleService
    {
        private readonly ApplicationDbContext _db;
        public VehicleService(ApplicationDbContext db) => _db = db;

        public async Task<List<object>> GetAvailableVehiclesAsync(DateTime from, DateTime to, int? capacity = null)
        {
            var q = _db.Vehicles.AsQueryable();

            // 只取可用車
            q = q.Where(v => (v.Status ?? "") == "可用");

            // 載客量限制
            if (capacity.HasValue)
                q = q.Where(v => v.Capacity >= capacity.Value);

            // 避開該時段已被派工的車
            q = q.Where(v => !_db.Dispatches.Any(d =>
                d.VehicleId == v.VehicleId &&
                from < d.EndTime &&
                d.StartTime < to));

            // 避開該時段已被申請的車
            q = q.Where(v => !_db.CarApplications.Any(a =>
                a.VehicleId == v.VehicleId &&
                from < a.UseEnd &&
                a.UseStart < to));

            return await q
                .OrderBy(v => v.PlateNo)
                .Select(v => new {
                    v.VehicleId,
                    v.PlateNo,
                    v.Brand,
                    v.Model,
                    v.Capacity,
                    v.Status
                })
                .ToListAsync<object>();
        }

        public async Task<int> GetMaxAvailableCapacityAsync(DateTime from, DateTime to)
        {
            var q = _db.Vehicles.AsQueryable();

            // 只取可用車
            q = q.Where(v => (v.Status ?? "") == "可用");

            // 避開該時段被派工(Dispatches)
            q = q.Where(v => !_db.Dispatches.Any(d =>
                d.VehicleId == v.VehicleId && from < d.EndTime && d.StartTime < to));

            // 避開該時段已被申請(CarApplications)的車
            q = q.Where(v => !_db.CarApplications.Any(a =>
                a.VehicleId == v.VehicleId && from < a.UseEnd && a.UseStart < to));

            // 回傳最大載客量（沒有車則回 0）
            var max = await q.Select(v => (int?)v.Capacity).MaxAsync();
            return max ?? 0;
        }
        
    }

}

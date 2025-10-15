using Cars.Data;
using Microsoft.EntityFrameworkCore;
using Cars.Shared.Line;

namespace Cars.Application.Services.Line
{
    public class LineVehicleService
    {
        private readonly ApplicationDbContext _db;
        public LineVehicleService(ApplicationDbContext db) => _db = db;

        public async Task<string> BuildCarSelectBubbleAsync(int applyId)
        {
            var app = await _db.CarApplications.FindAsync(applyId);
            if (app == null)
                return @"[{ ""type"": ""text"", ""text"": ""⚠️ 找不到該申請單"" }]";

            var useStart = app.UseStart;
            var useEnd = app.UseEnd;

            var cars = await _db.Vehicles
                .Where(v => v.Status == "可用" &&
                    !_db.CarApplications.Any(ca =>
                        ca.VehicleId == v.VehicleId &&
                        ca.ApplyId != applyId &&
                        ca.UseStart < useEnd &&
                        ca.UseEnd > useStart))
                .Select(v => new { v.VehicleId, v.PlateNo })
                .Take(5)
                .ToListAsync();

            var list = cars.Select(c => (c.VehicleId, c.PlateNo)).ToList();
            return LineMessageBuilder.BuildManagerReviewBubble(applyId, list);
        }
    }

}

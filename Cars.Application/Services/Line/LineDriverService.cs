using Cars.Data;
using Microsoft.EntityFrameworkCore;
using Cars.Shared.Line;

namespace Cars.Application.Services.Line
{
    public class LineDriverService
    {
        private readonly ApplicationDbContext _db;
        public LineDriverService(ApplicationDbContext db) => _db = db;

        public async Task<string> BuildDriverSelectBubbleAsync(int applyId)
        {
            var app = await _db.CarApplications.FindAsync(applyId);
            if (app == null)
                return @"{ ""type"": ""text"", ""text"": ""⚠️ 找不到申請單"" }";

            var availableDrivers = await _db.Drivers
                .Where(d => !d.IsAgent &&
                    !_db.CarApplications.Any(ca =>
                        ca.DriverId == d.DriverId &&
                        ca.ApplyId != applyId &&
                        ca.UseStart < app.UseEnd &&
                        ca.UseEnd > app.UseStart))
                .Select(d => new { d.DriverId, d.DriverName })
                .ToListAsync();

            var list = availableDrivers.Select(d => (d.DriverId, d.DriverName)).ToList();
            return LineMessageBuilder.BuildDriverSelectBubble(applyId, list);
        }
    }

}

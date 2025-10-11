using Cars.Core.Models;
using Cars.Data;
using Cars.Models;
using Cars.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
namespace Cars.Application.Services
{
    public sealed class CarApplicationUseCase
    {
        private readonly ApplicationDbContext _db;
        private readonly IDistanceService _distance;
        private readonly VehicleService _vehicleService;

        public CarApplicationUseCase(
            ApplicationDbContext db, IDistanceService distance, VehicleService vehicleService)
        {
            _db = db; _distance = distance; _vehicleService = vehicleService;
        }

        public async Task<(bool ok, string? message, int applyId)> CreateAsync(CreateCarApplicationRequest req)
        {
            // 1) 找申請人
            var applicant = req.Source switch
            {
                AppSource.Web => await _db.Applicants.FirstOrDefaultAsync(a => a.UserId == req.WebUserId),
                AppSource.Line => await _db.Users.Where(u => u.LineUserId == req.LineUserId)
                                     .Join(_db.Applicants, u => u.UserId, a => a.UserId, (u, a) => a)
                                     .FirstOrDefaultAsync(),
                _ => null
            };
            if (applicant == null) return (false, "找不到申請人", 0);

            // 2) 基本驗證
            if (req.UseStart == default || req.UseEnd == default || req.UseEnd <= req.UseStart)
                return (false, "用車時間區間無效", 0);

            // 3) 自動距離

            decimal? singleKm = null; string? singleDur = null;
            decimal? roundKm = null; string? roundDur = null;
            try
            {
                var (km, minutes) = await _distance.GetDistanceAsync(req.Origin, req.Destination);
                singleKm = km;
                singleDur = $"{(int)(minutes / 60)}小時{(int)(minutes % 60)}分";
                roundKm = km * 2;
                roundDur = $"{(int)((minutes * 2) / 60)}小時{(int)((minutes * 2) % 60)}分";
            }
            catch { }

            // 4) 容量檢查
            var maxCap = await _vehicleService.GetMaxAvailableCapacityAsync(req.UseStart, req.UseEnd);
            if (maxCap == 0) return (false, "目前時段沒有可用車輛", 0);
            if (req.PassengerCount > maxCap) return (false, $"乘客數 {req.PassengerCount} 超過可用最大載客量 {maxCap}", 0);

            using var tx = await _db.Database.BeginTransactionAsync();

            // 5) 落地申請單前先算是否長差
            var baseKm = req.TripType.Equals("single", StringComparison.OrdinalIgnoreCase)
                ? (singleKm ?? 0m)
                : (roundKm ?? 0m);
            var isLong = baseKm > 30m;

            var app = new CarApplication
            {
                ApplicantId = applicant.ApplicantId,
                ApplyFor = req.ApplyFor ?? "申請人",
                VehicleType = req.VehicleType ?? "汽車",
                PurposeType = req.PurposeType ?? "公務車(不可選車)",
                ReasonType = req.ReasonType ?? "公務用",
                PassengerCount = Math.Max(1, req.PassengerCount),
                ApplyReason = req.ApplyReason ?? "",
                Origin = req.Origin,
                Destination = req.Destination,
                UseStart = req.UseStart,
                UseEnd = req.UseEnd,
                TripType = req.TripType,
                Status = "待審核",
                SingleDistance = singleKm,
                SingleDuration = singleDur,
                RoundTripDistance = roundKm,
                RoundTripDuration = roundDur,
                IsLongTrip = isLong   // ← 用已展開的 bool
            };

            _db.CarApplications.Add(app);
            await _db.SaveChangesAsync();

            // 6) 乘客
            if (req.Passengers?.Count > 0)
            {
                foreach (var p in req.Passengers) p.ApplyId = app.ApplyId;
                _db.CarPassengers.AddRange(req.Passengers);
            }

            // 7) 建立「待指派」派工
            _db.Dispatches.Add(new Dispatch
            {
                ApplyId = app.ApplyId,
                DispatchStatus = "待指派",
                CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, null, app.ApplyId);
        }
    }

}

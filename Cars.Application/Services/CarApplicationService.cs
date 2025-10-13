using Cars.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Cars.Shared.Dtos.CarApplications;


namespace Cars.Application.Services
{
    public class CarApplicationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _http;

        public CarApplicationService(ApplicationDbContext db, IHttpContextAccessor http)
        {
            _db = db;
            _http = http;
        }

        public async Task<List<CarApplicationDto>> GetAll(
            DateTime? dateFrom,
            DateTime? dateTo,
            string? q,
            ClaimsPrincipal user)
        {
            // ===== 取出 user 資訊 =====
            var uidStr = user?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var userName = user.Identity?.Name;

            var baseQuery = _db.CarApplications
                .Include(a => a.Vehicle)
                .Include(a => a.Driver)
                .AsNoTracking()
                .AsQueryable();

            // ===== 權限過濾 (跟原本 Controller 一樣) =====
            if (user.IsInRole("Admin"))
            {
                // Admin 看全部
            }
            else if (user.IsInRole("Manager"))
            {
                if (int.TryParse(uidStr, out var userId))
                {
                    var myDept = await _db.Applicants
                        .AsNoTracking()
                        .Where(a => a.UserId == userId)
                        .Select(a => a.Dept)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrWhiteSpace(myDept))
                    {
                        baseQuery =
                            from a in baseQuery
                            join ap in _db.Applicants.AsNoTracking()
                                on a.ApplicantId equals ap.ApplicantId
                            where ap.Dept == myDept
                            select a;
                    }
                    else if (!string.IsNullOrEmpty(userName))
                    {
                        baseQuery =
                            from a in baseQuery
                            join ap in _db.Applicants.AsNoTracking()
                                on a.ApplicantId equals ap.ApplicantId
                            where ap.Name == userName
                            select a;
                    }
                }
            }
            else
            {
                if (int.TryParse(uidStr, out var userId))
                {
                    var myApplicantId = await _db.Applicants.AsNoTracking()
                        .Where(a => a.UserId == userId)
                        .Select(a => (int?)a.ApplicantId)
                        .FirstOrDefaultAsync();

                    if (myApplicantId.HasValue)
                        baseQuery = baseQuery.Where(a => a.ApplicantId == myApplicantId.Value);
                }
            }

            // ===== 日期篩選 =====
            if (dateFrom.HasValue)
                baseQuery = baseQuery.Where(a => a.UseStart >= dateFrom.Value.Date);

            if (dateTo.HasValue)
                baseQuery = baseQuery.Where(a => a.UseStart < dateTo.Value.Date.AddDays(1));

            // ===== 關鍵字 =====
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                baseQuery =
                    from a in baseQuery
                    join ap in _db.Applicants.AsNoTracking()
                        on a.ApplicantId equals ap.ApplicantId into apg
                    from ap in apg.DefaultIfEmpty()
                    where (a.Origin ?? "").Contains(k)
                       || (a.Destination ?? "").Contains(k)
                       || (a.ApplyReason ?? "").Contains(k)
                       || ap != null && (ap.Name ?? "").Contains(k)
                    select a;
            }

            // ===== 投影 DTO =====
            var list = await (
                from a in baseQuery
                join ap in _db.Applicants.AsNoTracking()
                    on a.ApplicantId equals ap.ApplicantId into apg
                from ap in apg.DefaultIfEmpty()
                select new CarApplicationDto
                {
                    ApplyId = a.ApplyId,
                    VehicleId = a.VehicleId,
                    PlateNo = a.Vehicle != null ? a.Vehicle.PlateNo : null,
                    DriverId = a.DriverId,
                    DriverName = a.Driver != null ? a.Driver.DriverName : null,
                    ApplicantId = ap != null ? ap.ApplicantId : null,
                    ApplicantName = ap != null ? ap.Name : null,
                    ApplicantDept = ap != null ? ap.Dept : null,
                    PassengerCount = a.PassengerCount,
                    UseStart = a.UseStart,
                    UseEnd = a.UseEnd,
                    Origin = a.Origin,
                    Destination = a.Destination,
                    IsLongTrip = a.TripType,
                    SingleDistance = a.SingleDistance,
                    RoundTripDistance = a.RoundTripDistance,
                    MaterialName = a.MaterialName,
                    Status = a.Status,
                    ReasonType = a.ReasonType,
                    ApplyReason = a.ApplyReason
                }
            )
            .OrderByDescending(x => x.ApplyId)
            .ToListAsync();

            return list;
        }
    }

    
}

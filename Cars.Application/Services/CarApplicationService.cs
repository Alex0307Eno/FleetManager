using Cars.Data;
using Cars.Models;
using Cars.Shared.Dtos.CarApplications;
using Cars.Shared.Line;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;


namespace Cars.Application.Services
{
    public class CarApplicationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _http;
        private readonly IDistanceService _distance;
        public CarApplicationService(ApplicationDbContext db, IHttpContextAccessor http, IDistanceService distance)
        {
            _db = db;
            _http = http;
            _distance = distance;
        }
        #region 取得申請單列表
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

            // ===== 權限過濾  =====
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
                    TripType = a.TripType,
                    PassengerCount = a.PassengerCount,
                    UseStart = a.UseStart,
                    UseEnd = a.UseEnd,
                    Origin = a.Origin,
                    Destination = a.Destination,
                    IsLongTrip = a.IsLongTrip,
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
        #endregion

        #region 建立申請單與派車單
        // 建立申請單 (Web)
        public async Task<(bool ok, string msg, CarApplication app)> CreateAsync(CarApplicationDto dto, int userId)
        {
            var applicant = await _db.Applicants.FirstOrDefaultAsync(a => a.UserId == userId);
            if (applicant == null)
                return (false, "找不到申請人", null);

            // 準備 CarApplicationDto 給訊息模板
            var msgJson = ManagerTemplate.BuildManagerReviewBubble(dto);

            

            return await CreateInternalAsync(dto, applicant);
        }



        // 建立申請單 (LINE)
        public async Task<(bool ok, string msg, CarApplication app)> CreateForLineAsync(CarApplicationDto dto, string lineUserId)
        {
            // 1️ 先確認傳進來的 dto 有沒有東西
            if (dto == null)
                throw new Exception("dto 是 null！");

            // 2️ 找 LINE 對應的使用者
            var user = await _db.Users.FirstOrDefaultAsync(u => u.LineUserId == lineUserId);
            if (user == null)
                throw new Exception($"找不到 user：{lineUserId}");

            // 3️ 找申請人 Applicant
            var applicant = await _db.Applicants.FirstOrDefaultAsync(a => a.UserId == user.UserId);
            if (applicant == null)
                throw new Exception($"找不到 applicant，userId={user.UserId}");

            

            // 4️ 確認 applicant 拿到了才填入
            dto.ApplicantId = applicant.ApplicantId;
            dto.ApplicantName = applicant.Name;

            // 5️ 建立申請與派車資料
            var (ok, msg, app) = await CreateInternalAsync(dto, applicant);
            if (!ok || app == null)
                return (ok, msg, app);

            dto.ApplyId = app.ApplyId;

            // 6️ 建立審核卡片
            var msgJson = ManagerTemplate.BuildManagerReviewBubble(dto);

            Console.WriteLine($"[DEBUG] CreateForLineAsync 成功建立申請");
            Console.WriteLine($"[DEBUG] ApplyId = {app.ApplyId}");
            Console.WriteLine($"[DEBUG] Applicant = {applicant.ApplicantId}:{applicant.Name}");
            Console.WriteLine($"[DEBUG] JSON = {msgJson}");

            return (true, msg, app);
        }



        #endregion

        #region 共用方法
        // ===== 轉換區 =====
        private CarApplication BuildEntityFromDto(CarApplicationDto dto, Applicant applicant)
        {
            return new CarApplication
            {
                ApplyId = dto.ApplyId,
                ApplicantId = applicant.ApplicantId,
                ApplyFor = "申請人",
                VehicleType = dto.VehicleType ?? "汽車",
                PurposeType = dto.PurposeType ?? "公務車",
                ReasonType = dto.ReasonType ?? "公務用",
                PassengerCount = (dto.PassengerCount ?? 1) > 0 ? dto.PassengerCount ?? 1 : 1,
                ApplyReason = dto.ApplyReason ?? "",
                MaterialName = dto.MaterialName ?? "",
                Origin = dto.Origin ?? "公司",
                Destination = dto.Destination ?? "",
                UseStart = dto.UseStart != default ? dto.UseStart : DateTime.Now,
                UseEnd = dto.UseEnd != default ? dto.UseEnd : DateTime.Now.AddMinutes(30),
                RoundTripDistance = dto.RoundTripDistance,
                SingleDistance = dto.SingleDistance,
                RoundTripDuration = dto.RoundTripDuration,
                SingleDuration = dto.SingleDuration,
                TripType = dto.TripType ?? "single",
                Status = "待審核"
            };
        }

        private Dispatch BuildDispatchFromDto(CarApplicationDto dto, CarApplication app)
        {
            return new Dispatch
            {
                ApplyId = app.ApplyId,
                VehicleId = dto.VehicleId,
                DriverId = dto.DriverId,
                DispatchStatus = "待派車",
                CreatedAt = DateTime.Now
            };
        }
        #endregion
        public async Task<(bool ok, string msg, CarApplication app)> CreateInternalAsync(CarApplicationDto dto, Applicant applicant)
        {
            // === 自動補距離 ===
            if (!string.IsNullOrWhiteSpace(dto.Origin) && !string.IsNullOrWhiteSpace(dto.Destination))
            {
                var result = await _distance.GetDistanceAsync(dto.Origin, dto.Destination);
                if (result.km > 0)
                {
                    dto.SingleDistance = result.km;
                    dto.RoundTripDistance = result.km * 2;
                    dto.SingleDuration = ToHourMinuteString(result.minutes);
                    dto.RoundTripDuration = ToHourMinuteString(result.minutes * 2);
                }
            }

            var app = BuildEntityFromDto(dto, applicant);
            _db.CarApplications.Add(app);
            await _db.SaveChangesAsync();

            var order = BuildDispatchFromDto(dto, app);
            _db.Dispatches.Add(order);
            await _db.SaveChangesAsync();

            return (true, "申請與派車單建立成功", app);
        }

        private static string ToHourMinuteString(double minutes)
        {
            var h = (int)(minutes / 60);
            var m = (int)(minutes % 60);
            if (h > 0 && m > 0) return $"{h} 小時 {m} 分";
            if (h > 0) return $"{h} 小時";
            if (m > 0) return $"{m} 分";
            return "0 分";
        }

    }


}

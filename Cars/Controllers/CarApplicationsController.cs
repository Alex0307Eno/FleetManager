using Cars.Data;
using Cars.Models;
using Cars.Services;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using static Cars.Services.AutoDispatcher;

namespace Cars.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    
    public class CarApplicationsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly AutoDispatcher _dispatcher;
        private readonly IDistanceService _distance;



        public CarApplicationsController(ApplicationDbContext db, AutoDispatcher dispatcher, IDistanceService distance)
        {
            _db = db;
            _dispatcher = dispatcher;
            _distance = distance;

        }

        #region 建立申請單
        // 建立申請單（含搭乘人員清單）
        public class CarApplyDto
        {
            public CarApplication Application { get; set; }
            public List<CarPassenger> Passengers { get; set; } = new();

        }



        [Authorize]
        [HttpPost]
        [Authorize(Roles = "Admin,Applicant,Manager")]
        public async Task<IActionResult> Create([FromBody] CarApplyDto dto)
        {
            if (dto == null || dto.Application == null)
                return BadRequest("申請資料不得為空");

            var model = dto.Application;

            // 1) 取得登入者
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(userIdStr, out var userId))
                return Unauthorized("尚未登入或 Session 遺失");

            // 2) 找申請人
            Cars.Models.Applicant applicant = null;
            if (model.ApplyFor == "self")
                applicant = await _db.Applicants.FirstOrDefaultAsync(ap => ap.UserId == userId);
            else if (model.ApplyFor == "other" && model.ApplicantId.HasValue)
                applicant = await _db.Applicants.FirstOrDefaultAsync(ap => ap.ApplicantId == model.ApplicantId.Value);

            if (applicant == null)
                return BadRequest("找不到對應的申請人資料");

            model.ApplicantId = applicant.ApplicantId;

            // 3) 基本驗證
            if (model.UseStart == default || model.UseEnd == default)
                return BadRequest("起訖時間不得為空");
            if (model.UseEnd <= model.UseStart)
                return BadRequest("結束時間必須晚於起始時間");

            // 4) 如果前端沒帶距離，就呼叫 Distance API 自動計算
            if (model.SingleDistance == null || model.SingleDistance == 0)
            {
                try
                {
                    var (km, minutes) = await _distance.GetDistanceAsync(model.Origin, model.Destination);

                    model.SingleDistance = km;
                    model.SingleDuration = $"{(int)(minutes / 60)}小時{(int)(minutes % 60)}分";
                    model.RoundTripDistance = km * 2;
                    model.RoundTripDuration = $"{(int)((minutes * 2) / 60)}小時{(int)((minutes * 2) % 60)}分";
                }
                catch (Exception ex)
                {
                    Console.WriteLine("⚠️ 距離計算失敗: " + ex.Message);
                }
            }

            // 5) 計算是否長差
            var checkKm = model.TripType == "單程" || model.TripType?.Equals("single", StringComparison.OrdinalIgnoreCase) == true
                ? (model.SingleDistance ?? 0)
                : (model.RoundTripDistance ?? 0);

            model.IsLongTrip = checkKm > 30;

            // 6) 驗證當下是否有車輛能承載這麼多人
            var maxCap = await GetMaxAvailableCapacityAsync(model.UseStart, model.UseEnd);
            if (maxCap == 0)
            {
                return BadRequest("目前時段沒有任何可用車輛");
            }
            if (model.PassengerCount > maxCap)
            {
                return BadRequest($"申請乘客數 {model.PassengerCount} 超過可用車輛最大載客量 {maxCap}");
            }

            // === 7) 先存「申請單」
            _db.CarApplications.Add(model);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return Conflict(new { message = "資料已被更新，請重新整理後再試。", detail = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new { message = "資料儲存失敗，請確認輸入是否正確。", detail = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "伺服器內部錯誤", error = ex.Message });
            }

            // 8) 乘客寫入
            if (dto.Passengers != null && dto.Passengers.Count > 0)
            {
                foreach (var p in dto.Passengers)
                {
                    p.ApplyId = model.ApplyId;
                }
                _db.CarPassengers.AddRange(dto.Passengers);
                await _db.SaveChangesAsync();
            }

            // 9) 裝載物料品名
            if (!string.IsNullOrWhiteSpace(model.MaterialName))
            {
                var app = await _db.CarApplications.FirstOrDefaultAsync(a => a.ApplyId == model.ApplyId);
                if (app != null)
                {
                    app.MaterialName = model.MaterialName;
                    await _db.SaveChangesAsync();
                }
            }

            // 10) 建立派工（待指派）
            var dispatch = new Cars.Models.Dispatch
            {
                ApplyId = model.ApplyId,
                DriverId = null,
                VehicleId = null,
                StartTime = model.UseStart,
                EndTime = model.UseEnd,
                CreatedAt = DateTime.Now,
                DispatchStatus = "待指派"
            };

            _db.Dispatches.Add(dispatch);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "申請完成，已建立派車單 (待指派)",
                id = model.ApplyId,
                isLongTrip = model.IsLongTrip ? 1 : 0
            });
        }
        // 建立派車單
        [HttpPost("{applyId}/dispatch")]
        public async Task<IActionResult> CreateDispatch(int applyId)
        {
            var app = await _db.CarApplications.FindAsync(applyId);
            if (app == null) return NotFound(new { message = "找不到申請單" });

            // 檢查是否已經有派車單
            var exists = await _db.Dispatches.AnyAsync(d => d.ApplyId == applyId);
            if (exists) return Conflict(new { message = "已經有派車單存在" });

            var dispatch = new Cars.Models.Dispatch
            {
                ApplyId = applyId,
                DriverId = null,
                VehicleId = null,
                DispatchStatus = "待指派",
                StartTime = app.UseStart,
                EndTime = app.UseEnd,
                CreatedAt = DateTime.Now
            };

            _db.Dispatches.Add(dispatch);
            await _db.SaveChangesAsync();

            return Ok(dispatch);
        }

        [HttpGet("/api/vehicles/max-capacity")]
        public async Task<IActionResult> GetMaxCapacity([FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            if (from == default || to == default || from >= to)
                return BadRequest(new { message = "時間參數錯誤" });

            var max = await GetMaxAvailableCapacityAsync(from, to);
            return Ok(new { max });
        }

        // 計算最大載客量
        private async Task<int> GetMaxAvailableCapacityAsync(DateTime from, DateTime to)
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

        #endregion


        #region dispatches頁面功能

        #region 取得全部申請人
        // 取得全部申請人
        [HttpGet("applicants")]
        public async Task<IActionResult> GetApplicants()
        {
            var list = await _db.Applicants.AsNoTracking()
                .OrderBy(a => a.Name)
                .Select(a => new {
                    applicantId = a.ApplicantId,
                    name = a.Name,
                    dept = a.Dept
                })
                .ToListAsync();
            return Ok(list);
        }
        // 取得全部申請單
        [Authorize(Roles = "Admin,Applicant,Manager")]
        [HttpGet]
        public async Task<IActionResult> GetAll(
    [FromQuery] DateTime? dateFrom,
    [FromQuery] DateTime? dateTo,
    [FromQuery] string? q)
        {
            // 基礎查詢（含導航屬性）
            var baseQuery = _db.CarApplications
                .Include(a => a.Vehicle)
                .Include(a => a.Driver)
                .AsNoTracking()
                .AsQueryable();

            // 目前登入者
            var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.Identity?.Name;


            // ===== 可視範圍：Admin=全部；Manager=本部門；其他=自己 =====
            if (User.IsInRole("Admin"))
            {
                // Admin 看全部
            }
            else if (User.IsInRole("Manager"))
            {
                if (int.TryParse(uidStr, out var userId))
                {
                    // 取自己的部門
                    var myDept = await _db.Applicants
                        .Where(a => a.UserId == userId)
                        .Select(a => a.Dept)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrWhiteSpace(myDept))
                    {
                        // 只看同部門的申請
                        baseQuery =
                            from a in baseQuery
                            join ap in _db.Applicants.AsNoTracking()
                                on a.ApplicantId equals ap.ApplicantId
                            where ap.Dept == myDept
                            select a;
                    }
                    else if (!string.IsNullOrEmpty(userName))
                    {
                        // 找不到部門 → 退回只看自己（比申請人姓名）
                        baseQuery =
                            from a in baseQuery
                            join ap in _db.Applicants.AsNoTracking()
                                on a.ApplicantId equals ap.ApplicantId
                            where ap.Name == userName
                            select a;
                    }
                    else
                    {
                        return Ok(Array.Empty<object>());
                    }
                }
                else if (!string.IsNullOrEmpty(userName))
                {
                    // 沒有 userId 但有帳號名稱 → 退回只看自己
                    baseQuery =
                        from a in baseQuery
                        join ap in _db.Applicants.AsNoTracking()
                            on a.ApplicantId equals ap.ApplicantId
                        where ap.Name == userName
                        select a;
                }
                else
                {
                    return Ok(Array.Empty<object>());
                }
            }
            else
            {
                // 一般使用者：只看自己
                if (int.TryParse(uidStr, out var userId))
                {
                    var myApplicantId = await _db.Applicants
                        .Where(a => a.UserId == userId)
                        .Select(a => (int?)a.ApplicantId)
                        .FirstOrDefaultAsync();

                    if (myApplicantId.HasValue)
                    {
                        baseQuery = baseQuery.Where(a => a.ApplicantId == myApplicantId.Value);
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
                    else
                    {
                        return Ok(Array.Empty<object>());
                    }
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
                else
                {
                    return Ok(Array.Empty<object>());
                }
            }

            // ===== 日期篩選 =====
            if (dateFrom.HasValue)
                baseQuery = baseQuery.Where(a => a.UseStart >= dateFrom.Value.Date);

            if (dateTo.HasValue)
                baseQuery = baseQuery.Where(a => a.UseStart < dateTo.Value.Date.AddDays(1));

            // ===== 關鍵字（可選）=====
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
                       || (ap != null && (ap.Name ?? "").Contains(k))
                    select a;
            }

            // ===== 投影成前端需要的欄位 =====
            var list = await (
                from a in baseQuery
                join ap in _db.Applicants.AsNoTracking()
                    on a.ApplicantId equals ap.ApplicantId into apg
                from ap in apg.DefaultIfEmpty()
                select new
                {
                    applyId = a.ApplyId,
                    vehicleId = a.VehicleId,
                    plateNo = a.Vehicle != null ? a.Vehicle.PlateNo : null,
                    driverId = a.DriverId,
                    driverName = a.Driver != null ? a.Driver.DriverName : null,

                    applicantId = ap != null ? (int?)ap.ApplicantId : null,
                    applicantName = ap != null ? ap.Name : null,
                    applicantDept = ap != null ? ap.Dept : null,

                    passengerCount = a.PassengerCount,
                    useStart = a.UseStart,
                    useEnd = a.UseEnd,
                    origin = a.Origin,
                    destination = a.Destination,

                    tripType = a.TripType,            
                    singleDistance = a.SingleDistance,      
                    roundTripDistance = a.RoundTripDistance,
                    materialName = a.MaterialName,
                    status = a.Status,
                    reasonType = a.ReasonType,
                    applyReason = a.ApplyReason
                }
            )
            .OrderByDescending(x => x.applyId)
            .ToListAsync();

            return Ok(list);
        }

        #endregion

        #region 檢視單筆申請單
        // 取得單筆申請單 + 搭乘人員
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var app = await _db.CarApplications
                .Include(a => a.Vehicle)
                .Include(a => a.Driver)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ApplyId == id);
            if (app == null) return NotFound();

            var passengers = await _db.CarPassengers
                .Where(p => p.ApplyId == id)
                .ToListAsync();

            // 🔗 找對應的 Applicant
            var applicant = await _db.Applicants
                .AsNoTracking()
                .FirstOrDefaultAsync(ap => ap.ApplicantId == app.ApplicantId);

            return Ok(new
            {
                app.ApplyId,
                app.UseStart,
                app.UseEnd,
                app.Origin,
                app.Destination,
                app.PassengerCount,
                app.TripType,
                app.SingleDistance,
                app.RoundTripDistance,
                app.Status,
                app.ReasonType,
                app.ApplyReason,
                materialName = app.MaterialName,

                // 車輛/駕駛
                driverName = app.Driver != null ? app.Driver.DriverName : null,
                driverId = app.DriverId,
                plateNo = app.Vehicle != null ? app.Vehicle.PlateNo : null,
                capacity = app.Vehicle?.Capacity,
                vehicleId = app.VehicleId,

                // 🔑 申請者資料：只從 Applicants 取
                applicant = applicant == null ? null : new
                {
                    applicant.ApplicantId,
                    applicant.Name,
                    applicant.Dept,
                    applicant.Email,
                    applicant.Ext,
                    applicant.Birth
                },

                passengers
            });

        }
        #endregion

        #region 刪除申請單
        // 刪除申請單（連同搭乘人員）
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var app = await _db.CarApplications.FindAsync(id);
            if (app == null) return NotFound();

            // 刪掉子表（派工單）
            var dispatches = _db.Dispatches.Where(d => d.ApplyId == id);
            _db.Dispatches.RemoveRange(dispatches);

            // 刪掉子表（乘客）
            var passengers = _db.CarPassengers.Where(p => p.ApplyId == id);
            _db.CarPassengers.RemoveRange(passengers);

            // 最後刪掉申請單
            _db.CarApplications.Remove(app);
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // 資料被別人改過 → 可以提示用戶重試
                return Conflict(new { message = "資料已被更新，請重新整理後再試。", detail = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                // 一般資料庫錯誤
                return BadRequest(new { message = "資料儲存失敗，請確認輸入是否正確。", detail = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                // 500 錯誤
                return StatusCode(500, new { message = "伺服器內部錯誤", error = ex.Message });
            }
            try
            {
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    // 資料被別人改過 → 可以提示用戶重試
                    return Conflict(new { message = "資料已被更新，請重新整理後再試。", detail = ex.Message });
                }
                catch (DbUpdateException ex)
                {
                    // 一般資料庫錯誤
                    return BadRequest(new { message = "資料儲存失敗，請確認輸入是否正確。", detail = ex.InnerException?.Message ?? ex.Message });
                }
                catch (Exception ex)
                {
                    // 500 錯誤
                    return StatusCode(500, new { message = "伺服器內部錯誤", error = ex.Message });
                }
                return Ok(new { message = "刪除成功" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "刪除失敗", detail = ex.Message });
            }
        }
        #endregion


        #region 更新審核狀態
        public class StatusDto { public string? Status { get; set; } }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Status))
                return BadRequest("Status 不可為空");

            var app = await _db.CarApplications.FirstOrDefaultAsync(x => x.ApplyId == id);
            if (app == null) return NotFound();

            var newStatus = dto.Status.Trim();

            if (newStatus == "完成審核")
            {
                // 找這張申請單最新一筆派工（無論有沒有駕駛）
                var dispatch = await _db.Dispatches
                    .Where(d => d.ApplyId == app.ApplyId)
                    .OrderByDescending(d => d.DispatchId)
                    .FirstOrDefaultAsync();

                // 如果有派工 & 已指派駕駛，就把駕駛/車輛帶進來
                if (dispatch != null && dispatch.DriverId != null)
                {
                    app.DriverId = dispatch.DriverId;
                    app.VehicleId = dispatch.VehicleId;
                }

                // 直接更新狀態
                app.Status = "完成審核";

                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    return Conflict(new { message = "資料已被更新，請重新整理後再試。", detail = ex.Message });
                }
                catch (DbUpdateException ex)
                {
                    return BadRequest(new { message = "資料儲存失敗，請確認輸入是否正確。", detail = ex.InnerException?.Message ?? ex.Message });
                }
                catch (Exception ex)
                {
                    return StatusCode(500, new { message = "伺服器內部錯誤", error = ex.Message });
                }

                return Ok(new
                {
                    message = "已完成審核" + (dispatch == null ? "（尚未派駕駛）" : ""),
                    status = app.Status,
                    driverId = app.DriverId,
                    vehicleId = app.VehicleId
                });
            }

            // 其他狀態直接更新
            app.Status = newStatus;
            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                return Conflict(new { message = "資料已被更新，請重新整理後再試。", detail = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                return BadRequest(new { message = "資料儲存失敗，請確認輸入是否正確。", detail = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "伺服器內部錯誤", error = ex.Message });
            }

            return Ok(new { message = "狀態已更新", status = app.Status });
        }
        #endregion

        #region 過濾可用車輛與司機
        // 過濾可用車輛（排除 使用中 / 維修中 / 該時段已被派工）
        [HttpGet("/api/vehicles/available")]
        public async Task<IActionResult> GetAvailableVehicles(DateTime from, DateTime to, int? capacity = null)
        {
            if (from == default || to == default || to <= from)
                return BadRequest("時間區間不正確");

            var q = _db.Vehicles.AsQueryable();

            // 只要「可用」的車
            q = q.Where(v => (v.Status ?? "") == "可用");

            // 載客量限制
            if (capacity.HasValue)
                q = q.Where(v => v.Capacity >= capacity.Value);

            //  避開該時段已被派工的車 (Dispatches)
            q = q.Where(v => !_db.Dispatches.Any(d =>
                d.VehicleId == v.VehicleId &&
                from < d.EndTime &&
                d.StartTime < to));

            //  避開該時段已經有申請單的車 (CarApplications)
            q = q.Where(v => !_db.CarApplications.Any(a =>
                a.VehicleId == v.VehicleId &&
                from < a.UseEnd &&
                a.UseStart < to));

            var list = await q
                .OrderBy(v => v.PlateNo)
                .Select(v => new {
                    v.VehicleId,
                    v.PlateNo,
                    v.Brand,
                    v.Model,
                    v.Capacity,
                    v.Status
                })
                .ToListAsync();

            return Ok(list);
        }
        //過濾可用司機
        [HttpGet("/api/drivers/available")]
        public async Task<IActionResult> GetAvailableDrivers(DateTime useStart, DateTime useEnd)
        {
            var today = DateTime.Today;

            // 找出在該時段已經有派工的駕駛
            var busyDrivers = await _db.Dispatches
                .Where(d => d.StartTime < useEnd && d.EndTime > useStart) // 有重疊的時間
                .Select(d => d.DriverId)
                .ToListAsync();

            // 1. 正常有出勤的司機
            var drivers = await _db.Drivers
                .Where(d => _db.Schedules.Any(s =>
                    s.DriverId == d.DriverId &&
                    s.WorkDate == today &&
                    s.IsPresent == true) &&
                    !busyDrivers.Contains(d.DriverId))  // 過濾掉已派工
                .Select(d => new {
                    d.DriverId,
                    d.DriverName
                })
                .ToListAsync();

            // 2. 今日有效的代理人
            var agents = await _db.DriverDelegations
                .Include(d => d.Agent)
                .Where(d => d.StartDate.Date <= today && today <= d.EndDate.Date &&
                            !busyDrivers.Contains(d.AgentDriverId)) // 過濾掉已派工
                .Select(d => new {
                    DriverId = d.AgentDriverId,
                    DriverName = d.Agent.DriverName + " (代)"
                })
                .ToListAsync();

            // 3. 合併 + 去重
            var all = drivers
                .Concat(agents)
                .GroupBy(x => x.DriverId)
                .Select(g => g.First())
                .ToList();

            return Ok(all);
        }
        // CarApplicationsController 內
        public class AssignDto { public int? DriverId { get; set; } public int? VehicleId { get; set; } }

        [HttpPatch("{id}/assignment")]
        public async Task<IActionResult> UpdateAssignment(int id, [FromBody] AssignDto dto)
        {
            var app = await _db.CarApplications.FindAsync(id);
            if (app == null) return NotFound();

            var from = app.UseStart; var to = app.UseEnd;

            // 驗證車在區間內可用（避免撞單）
            if (dto.VehicleId.HasValue)
            {
                var vUsed = await _db.Dispatches.AnyAsync(d =>
                    d.VehicleId == dto.VehicleId &&
                    d.ApplyId != id &&
                    from < d.EndTime && d.StartTime < to);
                if (vUsed) return BadRequest("該車於此時段已被派用");
            }

            // 驗證駕駛在區間內可用
            if (dto.DriverId.HasValue)
            {
                var dUsed = await _db.Dispatches.AnyAsync(d =>
                    d.DriverId == dto.DriverId &&
                    d.ApplyId != id &&
                    from < d.EndTime && d.StartTime < to);
                if (dUsed) return BadRequest("該駕駛於此時段已有派工");
            }

            app.DriverId = dto.DriverId;
            app.VehicleId = dto.VehicleId;

            // 同步 Dispatch（有就改，沒有就建）
            var disp = await _db.Dispatches.FirstOrDefaultAsync(d => d.ApplyId == id);
            if (disp == null && (dto.DriverId.HasValue || dto.VehicleId.HasValue))
            {
                _db.Dispatches.Add(new Cars.Models.Dispatch
                {
                    ApplyId = id,
                    DriverId = dto.DriverId ?? 0,
                    VehicleId = dto.VehicleId ?? 0,
                    DispatchStatus = "執勤中",
                    StartTime = from,
                    EndTime = to,
                    CreatedAt = DateTime.Now
                });
            }
            else if (disp != null)
            {
                if (dto.DriverId.HasValue) disp.DriverId = dto.DriverId.Value;
                if (dto.VehicleId.HasValue) disp.VehicleId = dto.VehicleId.Value;
            }

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // 資料被別人改過 → 可以提示用戶重試
                return Conflict(new { message = "資料已被更新，請重新整理後再試。", detail = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                // 一般資料庫錯誤
                return BadRequest(new { message = "資料儲存失敗，請確認輸入是否正確。", detail = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                // 500 錯誤
                return StatusCode(500, new { message = "伺服器內部錯誤", error = ex.Message });
            }
            return Ok(new { message = "指派已更新" });
        }
        #endregion

        #region 審核後自動派車
        //完成審核自動派車
        [HttpPost("applications/{applyId:int}/approve-assign")]
        public async Task<IActionResult> ApproveAndAssign(
    int applyId,
    [FromQuery] int passengerCount,
    [FromQuery] int? preferredVehicleId = null)
        {
            // 1) 找此申請單對應、未派車的派工
            var dispatch = await _db.Dispatches
            .Where(d => d.ApplyId == applyId
            && d.DriverId != null)                 
            .OrderByDescending(d => d.DispatchId)
            .FirstOrDefaultAsync();

            //if (dispatch == null)
            //    return NotFound(new { message = "找不到待派車的派工（可能已派車或尚未指派駕駛）。" });

            // 2) 自動派車
            //var result = await _dispatcher.ApproveAndAssignVehicleAsync(dispatch.DispatchId, passengerCount, preferredVehicleId);
            //if (!result.Success)
            //    return BadRequest(new { message = result.Message });

            // 3) 更新申請單狀態為「審核完成」
            var app = await _db.CarApplications.FirstOrDefaultAsync(a => a.ApplyId == applyId);
            if (app != null)
            {
                app.Status = "完成審核";
                try
                {
                    await _db.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    // 資料被別人改過 → 可以提示用戶重試
                    return Conflict(new { message = "資料已被更新，請重新整理後再試。", detail = ex.Message });
                }
                catch (DbUpdateException ex)
                {
                    // 一般資料庫錯誤
                    return BadRequest(new { message = "資料儲存失敗，請確認輸入是否正確。", detail = ex.InnerException?.Message ?? ex.Message });
                }
                catch (Exception ex)
                {
                    // 500 錯誤
                    return StatusCode(500, new { message = "伺服器內部錯誤", error = ex.Message });
                }
            }

            return Ok(new
            {
                message = "已完成審核（自動派工已停用，請手動指派駕駛與車輛）",
                status = app?.Status
            });
        }

        #endregion




        #endregion

        #region 更新申請單(無功能)
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CarApplication model)
        {
            var app = await _db.CarApplications.FindAsync(id);
            if (app == null) return NotFound();

            // 基本驗證
            if (model.UseStart == default(DateTime) || model.UseEnd == default(DateTime))
                return BadRequest("起訖時間不得為空");
            if (model.UseEnd <= model.UseStart)
                return BadRequest("結束時間必須晚於起始時間");

            // === 申請單欄位更新 ===
            app.ApplyFor = model.ApplyFor;
            app.VehicleType = model.VehicleType;
            app.PurposeType = model.PurposeType;
            app.VehicleId = model.VehicleId; // 可為 null
            app.PassengerCount = model.PassengerCount;
            app.UseStart = model.UseStart;
            app.UseEnd = model.UseEnd;
            app.DriverId = model.DriverId; // 可為 null
            app.ReasonType = model.ReasonType;
            app.ApplyReason = model.ApplyReason;
            app.Origin = model.Origin;
            app.Destination = model.Destination;
            app.TripType = model.TripType;
            app.SingleDistance = model.SingleDistance;
            app.SingleDuration = model.SingleDuration;
            app.RoundTripDistance = model.RoundTripDistance;
            app.RoundTripDuration = model.RoundTripDuration;
            app.Status = string.IsNullOrWhiteSpace(model.Status) ? app.Status : model.Status;

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // 資料被別人改過 → 可以提示用戶重試
                return Conflict(new { message = "資料已被更新，請重新整理後再試。", detail = ex.Message });
            }
            catch (DbUpdateException ex)
            {
                // 一般資料庫錯誤
                return BadRequest(new { message = "資料儲存失敗，請確認輸入是否正確。", detail = ex.InnerException?.Message ?? ex.Message });
            }
            catch (Exception ex)
            {
                // 500 錯誤
                return StatusCode(500, new { message = "伺服器內部錯誤", error = ex.Message });
            }
            return Ok(new { message = "更新成功" });
        }
        #endregion
        //LINE專用新增申請單
        [HttpPost("auto-create")]
        public async Task<IActionResult> AutoCreate([FromQuery] string lineUserId, [FromBody] CarApplication input)
        {
            if (string.IsNullOrEmpty(lineUserId))
                return BadRequest("缺少 lineUserId");

            var user = await _db.Users.FirstOrDefaultAsync(u => u.LineUserId == lineUserId);
            if (user == null) return BadRequest("找不到使用者");

            var applicant = await _db.Applicants.FirstOrDefaultAsync(a => a.UserId == user.UserId);
            if (applicant == null) return BadRequest("找不到對應的申請人");

            // 用 input 的值，如果沒傳就 fallback 預設
            var app = new CarApplication
            {
                ApplyFor = input.ApplyFor ?? "申請人",
                VehicleType = input.VehicleType ?? "汽車",
                PurposeType = input.PurposeType ?? "公務車(不可選車)",
                ReasonType = input.ReasonType ?? "公務用",
                PassengerCount = input.PassengerCount > 0 ? input.PassengerCount : 1,
                ApplyReason = input.ApplyReason ?? "",
                Origin = input.Origin ?? "公司",
                Destination = input.Destination ?? "",
                UseStart = input.UseStart != default ? input.UseStart : DateTime.Now,
                UseEnd = input.UseEnd != default ? input.UseEnd : DateTime.Now.AddMinutes(30),
                TripType = input.TripType ?? "single",
                ApplicantId = applicant.ApplicantId,
                Status = "待審核",
                SingleDistance = input.SingleDistance,
                SingleDuration = input.SingleDuration,
                RoundTripDistance = input.RoundTripDistance,
                RoundTripDuration = input.RoundTripDuration,
            };

            _db.CarApplications.Add(app);
            await _db.SaveChangesAsync();

            return Ok(app);
        }





    }


}

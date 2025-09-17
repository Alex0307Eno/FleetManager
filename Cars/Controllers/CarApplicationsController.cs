using Cars.Data;
using Cars.Models;
using Cars.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using static Cars.Services.AutoDispatcher;

namespace Cars.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin,Applicant,Manager")]
    public class CarApplicationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly AutoDispatcher _dispatcher;


        public CarApplicationsController(ApplicationDbContext context, AutoDispatcher dispatcher)
        {
            _context = context;
            _dispatcher = dispatcher;
        }

        #region 建立申請單
        // 建立申請單（含搭乘人員清單）
        public class CarApplyDto
        {
            public CarApplication Application { get; set; }
            public List<CarPassenger> Passengers { get; set; } = new();

        }
      



        [HttpPost]
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
                applicant = await _context.Applicants.FirstOrDefaultAsync(ap => ap.UserId == userId);
            else if (model.ApplyFor == "other" && model.ApplicantId.HasValue)
                applicant = await _context.Applicants.FirstOrDefaultAsync(ap => ap.ApplicantId == model.ApplicantId.Value);

            if (applicant == null)
                return BadRequest("找不到對應的申請人資料");

            model.ApplicantId = applicant.ApplicantId;

            // 3) 基本驗證
            if (model.UseStart == default || model.UseEnd == default)
                return BadRequest("起訖時間不得為空");
            if (model.UseEnd <= model.UseStart)
                return BadRequest("結束時間必須晚於起始時間");

            // 4) 計算是否長差
            bool isSingle = model.TripType == "單程"
              || model.TripType?.Equals("single", StringComparison.OrdinalIgnoreCase) == true;

            decimal km = isSingle ? (model.SingleDistance ?? 0) : (model.RoundTripDistance ?? 0);
            if (km <= 0) km = model.SingleDistance ?? 0;
            model.isLongTrip = km > 30;
            // 4-b) 驗證當下是否有車輛能承載這麼多人
            var maxCap = await GetMaxAvailableCapacityAsync(model.UseStart, model.UseEnd);
            if (maxCap == 0)
            {
                return BadRequest("目前時段沒有任何可用車輛");
            }
            if (model.PassengerCount > maxCap)
            {
                return BadRequest($"申請乘客數 {model.PassengerCount} 超過可用車輛最大容量 {maxCap}");
            }


            // === 5) 先存「申請單」，取得真正的 ApplyId ===

            _context.CarApplications.Add(model);                
            await _context.SaveChangesAsync();                  

            // 6) 乘客：若有就一起寫入（ApplyId 要用新產生的）
            if (dto.Passengers != null && dto.Passengers.Count > 0)
            {
                foreach (var p in dto.Passengers)
                {
                    p.ApplyId = model.ApplyId;
                }
                _context.CarPassengers.AddRange(dto.Passengers);
                await _context.SaveChangesAsync();
            }

           

            // 8) 自動派工（用「已存在」的 ApplyId）
            var result = await _dispatcher.AssignAsync(          
                model.ApplyId,
                model.UseStart,
                model.UseEnd,
                model.PassengerCount,
                model.VehicleType,
                new AutoDispatcher.AssignOptions { DriverOnly = true }
            );

            if (!result.Success)
            {
                return Ok(new
                {
                    message = "申請成功，但派工失敗：" + result.Message,
                    id = model.ApplyId,
                    isLongTrip = model.isLongTrip ? 1 : 0
                });
            }

            // 9) 派工成功 → 回寫申請單的 DriverId（必要時也可寫 VehicleId）
            model.DriverId = result.DriverId;
            await _context.SaveChangesAsync();


            var msg = $"申請完成，司機：{result.DriverName}，待管理員派車";

            return Ok(new
            {
                message = msg,
                id = model.ApplyId,
                driverId = result.DriverId,
                vehicleId = result.VehicleId,
                plateNo = result.PlateNo,
                isLongTrip = model.isLongTrip ? 1 : 0
            });
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
            var q = _context.Vehicles.AsQueryable();

            // 只取可用車
            q = q.Where(v => (v.Status ?? "") == "可用");

            // 避開該時段被派工(Dispatches)
            q = q.Where(v => !_context.Dispatches.Any(d =>
                d.VehicleId == v.VehicleId && from < d.EndTime && d.StartTime < to));

            // 避開該時段已被申請(CarApplications)的車
            q = q.Where(v => !_context.CarApplications.Any(a =>
                a.VehicleId == v.VehicleId && from < a.UseEnd && a.UseStart < to));

            // 回傳最大容量（沒有車則回 0）
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
            var list = await _context.Applicants.AsNoTracking()
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
            var baseQuery = _context.CarApplications
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
                    var myDept = await _context.Applicants
                        .Where(a => a.UserId == userId)
                        .Select(a => a.Dept)
                        .FirstOrDefaultAsync();

                    if (!string.IsNullOrWhiteSpace(myDept))
                    {
                        // 只看同部門的申請
                        baseQuery =
                            from a in baseQuery
                            join ap in _context.Applicants.AsNoTracking()
                                on a.ApplicantId equals ap.ApplicantId
                            where ap.Dept == myDept
                            select a;
                    }
                    else if (!string.IsNullOrEmpty(userName))
                    {
                        // 找不到部門 → 退回只看自己（比申請人姓名）
                        baseQuery =
                            from a in baseQuery
                            join ap in _context.Applicants.AsNoTracking()
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
                        join ap in _context.Applicants.AsNoTracking()
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
                    var myApplicantId = await _context.Applicants
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
                            join ap in _context.Applicants.AsNoTracking()
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
                        join ap in _context.Applicants.AsNoTracking()
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
                    join ap in _context.Applicants.AsNoTracking()
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
                join ap in _context.Applicants.AsNoTracking()
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
            var app = await _context.CarApplications
                .Include(a => a.Vehicle)
                .Include(a => a.Driver)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ApplyId == id);
            if (app == null) return NotFound();

            var passengers = await _context.CarPassengers
                .Where(p => p.ApplyId == id)
                .ToListAsync();

            // 🔗 找對應的 Applicant
            var applicant = await _context.Applicants
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
            var app = await _context.CarApplications.FindAsync(id);
            if (app == null) return NotFound();

            // 刪掉子表（派工單）
            var dispatches = _context.Dispatches.Where(d => d.ApplyId == id);
            _context.Dispatches.RemoveRange(dispatches);

            // 刪掉子表（乘客）
            var passengers = _context.CarPassengers.Where(p => p.ApplyId == id);
            _context.CarPassengers.RemoveRange(passengers);

            // 最後刪掉申請單
            _context.CarApplications.Remove(app);
            await _context.SaveChangesAsync();
            try
            {
                await _context.SaveChangesAsync();
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

            var app = await _context.CarApplications.FirstOrDefaultAsync(x => x.ApplyId == id);
            if (app == null) return NotFound();

            // 小工具：把「12.3 公里」→ 12.3；其它非數字/小數點去掉
            decimal ParseKm(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return 0m;
                var raw = new string(s.Where(ch => char.IsDigit(ch) || ch == '.' || ch == '-').ToArray());
                return decimal.TryParse(raw, out var km) ? km : 0m;
            }

            var newStatus = dto.Status.Trim();

            if (newStatus == "完成審核")
            {
                // 找這張申請單最新一筆已派駕駛的派工
                var dispatch = await _context.Dispatches
                    .Where(d => d.ApplyId == app.ApplyId && d.DriverId != null)
                    .OrderByDescending(d => d.DispatchId)
                    .FirstOrDefaultAsync();

                if (dispatch == null)
                    return Conflict(new { message = "此申請單尚未派駕駛，不能完成審核。" });

                // 交給 AutoDispatcher 只補車
                var result = await _dispatcher.ApproveAndAssignVehicleAsync(
                    dispatch.DispatchId,
                    app.PassengerCount,
                    null  // 有需要可傳 preferredVehicleId
                );

                if (!result.Success)
                    return Conflict(new { message = $"派車失敗：{result.Message}" });

                // 更新申請單
                app.Status = "完成審核";
                app.VehicleId = result.VehicleId;
                app.DriverId = result.DriverId;
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = result.Message ?? "已完成審核並派車",
                    status = app.Status,
                    driverId = result.DriverId,
                    vehicleId = result.VehicleId,
                    plateNo = result.PlateNo
                });
            }


            // 其他狀態直接更新
            app.Status = newStatus;
            await _context.SaveChangesAsync();
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

            var q = _context.Vehicles.AsQueryable();

            // 只要「可用」的車
            q = q.Where(v => (v.Status ?? "") == "可用");

            // 容量限制
            if (capacity.HasValue)
                q = q.Where(v => v.Capacity >= capacity.Value);

            //  避開該時段已被派工的車 (Dispatches)
            q = q.Where(v => !_context.Dispatches.Any(d =>
                d.VehicleId == v.VehicleId &&
                from < d.EndTime &&
                d.StartTime < to));

            //  避開該時段已經有申請單的車 (CarApplications)
            q = q.Where(v => !_context.CarApplications.Any(a =>
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
        public async Task<IActionResult> GetAvailableDrivers()
        {
            var today = DateTime.Today;

            // 1. 正常有出勤的司機
            var drivers = await _context.Drivers
                .Where(d => _context.Schedules.Any(s =>
                    s.DriverId == d.DriverId &&
                    s.WorkDate == today &&
                    s.IsPresent == true))
                .Select(d => new {
                    d.DriverId,
                    d.DriverName
                })
                .ToListAsync();

            // 2. 今日有效的代理人
            var agents = await _context.DriverDelegations
                .Include(d => d.Agent)
                .Where(d => d.StartDate.Date <= today && today <= d.EndDate.Date)
                .Select(d => new {
                    DriverId = d.AgentDriverId,                  // 代理人 ID 當作 DriverId
                    DriverName = d.Agent.DriverName + " (代)" // 名稱後面加 (代)
                })
                .ToListAsync();

            // 3. 合併 + 去重（避免代理人同時也是司機重複出現）
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
            var app = await _context.CarApplications.FindAsync(id);
            if (app == null) return NotFound();

            var from = app.UseStart; var to = app.UseEnd;

            // 驗證車在區間內可用（避免撞單）
            if (dto.VehicleId.HasValue)
            {
                var vUsed = await _context.Dispatches.AnyAsync(d =>
                    d.VehicleId == dto.VehicleId &&
                    d.ApplyId != id &&
                    from < d.EndTime && d.StartTime < to);
                if (vUsed) return BadRequest("該車於此時段已被派用");
            }

            // 驗證駕駛在區間內可用
            if (dto.DriverId.HasValue)
            {
                var dUsed = await _context.Dispatches.AnyAsync(d =>
                    d.DriverId == dto.DriverId &&
                    d.ApplyId != id &&
                    from < d.EndTime && d.StartTime < to);
                if (dUsed) return BadRequest("該駕駛於此時段已有派工");
            }

            app.DriverId = dto.DriverId;
            app.VehicleId = dto.VehicleId;

            // 同步 Dispatch（有就改，沒有就建）
            var disp = await _context.Dispatches.FirstOrDefaultAsync(d => d.ApplyId == id);
            if (disp == null && (dto.DriverId.HasValue || dto.VehicleId.HasValue))
            {
                _context.Dispatches.Add(new Cars.Models.Dispatch
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

            await _context.SaveChangesAsync();
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
            var dispatch = await _context.Dispatches
            .Where(d => d.ApplyId == applyId
            && d.DriverId != null)                 
            .OrderByDescending(d => d.DispatchId)
            .FirstOrDefaultAsync();

            if (dispatch == null)
                return NotFound(new { message = "找不到待派車的派工（可能已派車或尚未指派駕駛）。" });

            // 2) 自動派車（可選擇指定車輛）
            var result = await _dispatcher.ApproveAndAssignVehicleAsync(dispatch.DispatchId, passengerCount, preferredVehicleId);
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            // 3) 更新申請單狀態為「審核完成」
            var app = await _context.CarApplications.FirstOrDefaultAsync(a => a.ApplyId == applyId);
            if (app != null)
            {
                app.Status = "完成審核";
                app.VehicleId = result.VehicleId; 
                app.DriverId = result.DriverId;   
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                message = result.Message ?? "完成審核，已派車",
                driverId = result.DriverId,
                vehicleId = result.VehicleId,
                plateNo = result.PlateNo
            });
        }

        #endregion




        #endregion

        #region 更新申請單(無功能)
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] CarApplication model)
        {
            var app = await _context.CarApplications.FindAsync(id);
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

            await _context.SaveChangesAsync();
            return Ok(new { message = "更新成功" });
        }
        #endregion
    }
}

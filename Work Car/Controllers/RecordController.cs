using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Cars.Controllers.Api
{
    [Authorize]
    [ApiController]
    [Route("api/dispatch")]
    public class RecordController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public RecordController(ApplicationDbContext db) => _db = db;


        [HttpGet("records")]
        public async Task<ActionResult<IEnumerable<Record>>> GetRecords(
             
            [FromQuery] DateTime? dateFrom,
            [FromQuery] DateTime? dateTo,
            [FromQuery] string? driver,
            [FromQuery] string? applicant,
            [FromQuery] string? plateNo,
            [FromQuery] string? order)
        {


            var q =
    from d in _db.Dispatches.AsNoTracking()
    join v in _db.Vehicles.AsNoTracking() on d.VehicleId equals v.VehicleId into vg
    from v in vg.DefaultIfEmpty()   // 🚗 車輛可空
    join r in _db.Drivers.AsNoTracking() on d.DriverId equals r.DriverId into rg
    from r in rg.DefaultIfEmpty()   // 👨‍✈️ 駕駛可空
    join a in _db.CarApplications.AsNoTracking() on d.ApplyId equals a.ApplyId
    join p in _db.Applicants.AsNoTracking() on a.ApplicantId equals p.ApplicantId
    select new
      {
          d.DispatchId,
          a.ApplyId,
          a.UseStart,
          a.UseEnd,
          a.Origin,
          a.Destination,
          a.ReasonType,
          a.ApplyReason,
          ApplicantName = p.Name,   
          a.PassengerCount,
          a.TripType,
          a.SingleDistance,
          a.RoundTripDistance,
          a.Status,
          DriverId = r != null ? r.DriverId : (int?)null,
          DriverName = r != null ? r.DriverName : null,
          VehicleId = v != null ? v.VehicleId : (int?)null,
          PlateNo = v != null ? v.PlateNo : null
    };

            // 🔒 若為司機角色，只看自己的派工
            if (User.IsInRole("Driver"))
            {
                var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier); // 登入時放的 userId
                if (!int.TryParse(uidStr, out var userId))
                    return Forbid();

                // 假設 Drivers 有 UserId 欄位可對應登入帳號
                var myDriverId = await _db.Drivers
                    .AsNoTracking()
                    .Where(d => d.UserId == userId)
                    .Select(d => d.DriverId)
                    .FirstOrDefaultAsync();

                if (myDriverId == 0)
                    return Forbid(); // 帳號未綁定司機

                q = q.Where(x => x.DriverId == myDriverId);
            }
            // 篩選
            if (dateFrom.HasValue)
            {
                var from = dateFrom.Value.Date;
                q = q.Where(x => x.UseStart >= from);
            }
            if (dateTo.HasValue)
            {
                var to = dateTo.Value.Date.AddDays(1).AddTicks(-1);
                q = q.Where(x => x.UseStart <= to);
            }
            if (!string.IsNullOrWhiteSpace(driver))
            {
                q = q.Where(x => x.DriverName == driver);
            }
            if (!string.IsNullOrWhiteSpace(applicant))
            {
                q = q.Where(x => x.ApplicantName == applicant);
            }
            if (!string.IsNullOrWhiteSpace(plateNo))
            {
                q = q.Where(x => x.PlateNo == plateNo);
            }

            // 排序

            var today = DateTime.Today;

            q = (order ?? "id_desc").ToLower() switch
            {
                // 今天 & 未來先排 → 然後依 UseStart 排
                "today_first" or "future_first"
                    => q.OrderBy(x => x.UseStart >= today ? 0 : 1)
                         .ThenBy(x => x.UseStart),

                // 最新 DispatchId 在最上
                "id_desc" or "latest"
                    => q.OrderByDescending(x => x.DispatchId),

                // UseStart 降冪
                "start_desc"
                    => q.OrderByDescending(x => x.UseStart),

                // 預設：UseStart 升冪
                _ => q.OrderBy(x => x.UseStart)
            };

            // 先抓 rawRows（字串都保留）
            var rawRows = await q.ToListAsync();

            // 取出本批要顯示的申請編號
            var applyIds = rawRows.Select(x => x.ApplyId).Distinct().ToList();
            // 這批派車單的 id
            var dispatchIds = rawRows.Select(x => x.DispatchId).Distinct().ToList();

            // 取出每張派車單的併單匯總（總座位、筆數）
            var linkAgg = await _db.DispatchApplications
                .Where(l => dispatchIds.Contains(l.DispatchId))
                .GroupBy(l => l.DispatchId)
                .Select(g => new
                {
                    DispatchId = g.Key,
                    LinkSeats = g.Sum(x => (int?)x.Seats) ?? 0,
                    LinkCount = g.Count()
                })
                .ToListAsync();
            var linkAggMap = linkAgg.ToDictionary(k => k.DispatchId, v => new { v.LinkSeats, v.LinkCount });

            // 取出每張派車單的子列（併入的申請）
            var linkDetails = await
                (from l in _db.DispatchApplications.AsNoTracking()
                 join a in _db.CarApplications.AsNoTracking() on l.ApplyId equals a.ApplyId
                 join p in _db.Applicants.AsNoTracking() on a.ApplicantId equals p.ApplicantId
                 where dispatchIds.Contains(l.DispatchId)
                 select new
                 {
                     l.DispatchId,
                     a.ApplyId,
                     a.UseStart,
                     a.UseEnd,
                     a.Origin,
                     a.Destination,
                     a.ReasonType,
                     a.ApplyReason,
                     ApplicantName = p.Name,
                     Seats = l.Seats,                 // 這筆併單實際佔用座位
                     a.TripType,
                     a.SingleDistance,
                     a.RoundTripDistance,
                     a.Status
                 }).ToListAsync();

            // === 組最終 rows：主列 + 子列（子列緊貼主列之後） ===
            var rows = new List<Record>();

            foreach (var x in rawRows)
            {
                // 主列公里
                decimal km = 0;
                if (x.TripType == "single") km = x.SingleDistance ?? 0;
                else if (x.TripType == "round") km = x.RoundTripDistance ?? 0;
                var longShort = km > 30 ? "長差" : "短差";

                // 主列加總座位（主申請 + 已併入）
                var agg = linkAggMap.ContainsKey(x.DispatchId) ? linkAggMap[x.DispatchId] : new { LinkSeats = 0, LinkCount = 0 };
                var totalSeats = (x.PassengerCount) + agg.LinkSeats;

                // ① 主列
                rows.Add(new Record
                {
                    Id = x.DispatchId,
                    ApplyId = x.ApplyId,
                    UseStart = x.UseStart,
                    UseEnd = x.UseEnd,
                    Route = string.Join(" - ", new[] { x.Origin, x.Destination }.Where(s => !string.IsNullOrWhiteSpace(s))),
                    TripType = x.TripType,
                    ReasonType = x.ReasonType,
                    Reason = x.ApplyReason,
                    Applicant = x.ApplicantName,
                    Seats = totalSeats,   // 顯示總座位（含併單）
                    Km = km,
                    Status = x.Status,
                    Driver = x.DriverName,
                    DriverId = x.DriverId,
                    Plate = x.PlateNo,
                    VehicleId = x.VehicleId,
                    LongShort = longShort
                    // 如果你的 Record 有 MergeCount 欄位，也可補：MergeCount = agg.LinkCount
                });

                // ② 子列（按時間排序，緊貼在主列之後）
                var children = linkDetails
                    .Where(ld => ld.DispatchId == x.DispatchId)
                    .OrderBy(ld => ld.UseStart)
                    .ToList();

                foreach (var c in children)
                {
                    decimal km2 = 0;
                    if (c.TripType == "single") km2 = c.SingleDistance ?? 0;
                    else if (c.TripType == "round") km2 = c.RoundTripDistance ?? 0;
                    var ls2 = km2 > 30 ? "長差" : "短差";

                    rows.Add(new Record
                    {
                        Id = x.DispatchId,          // 與主列相同，方便前端判斷同一車次
                        ApplyId = c.ApplyId,
                        UseStart = c.UseStart,
                        UseEnd = c.UseEnd,
                        Route = string.Join(" - ", new[] { c.Origin, c.Destination }.Where(s => !string.IsNullOrWhiteSpace(s))),
                        TripType = c.TripType,
                        ReasonType = c.ReasonType,
                        Reason = c.ApplyReason,
                        Applicant = c.ApplicantName,
                        Seats = c.Seats,           // 子列顯示這筆併單的座位數
                        Km = km2,
                        Status = c.Status,
                        // 子列通常不需要再顯示駕駛/車輛（可留空或沿用主列）
                        Driver = null,
                        DriverId = null,
                        Plate = null,
                        VehicleId = null,
                        LongShort = ls2
                    });
                }
            }

            return Ok(rows);
        }
        // 查詢單筆
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOne(int id)
        {
            var d = await _db.Dispatches
            .AsNoTracking()
            .Include(x => x.Vehicle)
            .Include(x => x.Driver)
            .Include(x => x.CarApply)
                .ThenInclude(ca => ca.Applicant)   
            .FirstOrDefaultAsync(x => x.DispatchId == id);

            if (d == null) return NotFound();
            return Ok(new
            {
                d.DispatchId,
                d.DispatchStatus,
                d.StartTime,
                d.EndTime,
                d.CreatedAt,
                Driver = d.Driver?.DriverName,
                PlateNo = d.Vehicle?.PlateNo,
                Applicant = d.CarApply?.Applicant?.Name,   
                ReasonType = d.CarApply?.ReasonType,
                Reason = d.CarApply?.ApplyReason,
                Origin = d.CarApply?.Origin,
                Destination = d.CarApply?.Destination,
                Seats = d.CarApply?.PassengerCount,
                Status = d.CarApply?.Status
            });
        }


        
        // 🔹 更新 (Update)
        public class UpdateDispatchDto
        {
            public int? DriverId { get; set; }
            public int? VehicleId { get; set; }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDispatch(int id, [FromBody] UpdateDispatchDto dto)
        {
            var dispatch = await _db.Dispatches.FindAsync(id);
            if (dispatch == null) return NotFound();

            dispatch.DriverId = dto.DriverId;
            dispatch.VehicleId = dto.VehicleId;

            await _db.SaveChangesAsync();
            return Ok(new { message = "更新成功", dispatch.DispatchId, dispatch.DriverId, dispatch.VehicleId });
        }


       




        //  刪除 (Delete)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var row = await _db.Dispatches.FindAsync(id);
            if (row == null) return NotFound();

            _db.Dispatches.Remove(row);
            await _db.SaveChangesAsync();
            return Ok(new { message = "刪除成功" });
        }


        // 併單請求 DTO
        public class MergeLinkDto
        {
            public int DispatchId { get; set; }
            public int? Seats { get; set; } // 若沒帶，預設用該申請的 PassengerCount
        }

        // ➊ 併單：把某個申請併入指定派車單
        [HttpPost("{dispatchId}/links")]
        public async Task<IActionResult> AddLink(int dispatchId, [FromBody] MergeLinkDto dto)
        {
            // dto 裡面要傳另一張 DispatchId（不是 ApplyId）
            var targetDispatch = await _db.Dispatches
                .Include(d => d.Applications)
                .FirstOrDefaultAsync(d => d.DispatchId == dto.DispatchId);

            if (targetDispatch == null)
                return NotFound("找不到要併入的派車單");

            // 檢查不能跟自己併
            if (dispatchId == dto.DispatchId)
                return BadRequest("不能將同一張派車單併入自己");

            // 檢查是否已經併入過
            var exists = await _db.DispatchApplications
                .FindAsync(dispatchId, targetDispatch.ApplyId);
            if (exists != null)
                return BadRequest("此派車單已經併入過");

            // 核心寫入
            var link = new DispatchApplication
            {
                DispatchId = dispatchId,
                Seats = dto.Seats??0,
            };

            _db.DispatchApplications.Add(link);
            await _db.SaveChangesAsync();

            return Ok(new { message = "併單成功", dispatchId, targetDispatchId = dto.DispatchId });
        }

        // ➋ 取消併單
        [HttpDelete("{dispatchId}/links/{applyId}")]
        public async Task<IActionResult> RemoveLink(int dispatchId, int applyId)
        {
            var link = await _db.DispatchApplications.FindAsync(dispatchId, applyId);
            if (link == null) return NotFound();

            _db.DispatchApplications.Remove(link);
            await _db.SaveChangesAsync();
            return Ok(new { message = "已取消併單" });
        }

        // ➌ 查看某派車單的併單列表
        [HttpGet("{dispatchId}/links")]
        public async Task<IActionResult> ListLinks(int dispatchId)
        {
            var rows = await _db.DispatchApplications
                .Where(x => x.DispatchId == dispatchId)
                .Join(_db.CarApplications,
                      x => x.ApplyId,
                      a => a.ApplyId,
                      (x, a) => new {
                          a.ApplyId,
                          a.Origin,
                          a.Destination,
                          a.UseStart,
                          a.UseEnd,
                          a.PassengerCount,
                          Seats = x.Seats
                      })
                .ToListAsync();

            return Ok(rows);
        }

        // 查詢可併入的申請單
        [HttpGet("{dispatchId}/available-apps")]
        public async Task<IActionResult> GetAvailableAppsForDispatch(int dispatchId)
        {
            // 找到派車單
            var dispatch = await _db.Dispatches.FindAsync(dispatchId);
            if (dispatch == null) return NotFound("派車單不存在");

            // 找到主申請
            var mainApp = await _db.CarApplications.FindAsync(dispatch.ApplyId);
            if (mainApp == null) return BadRequest("主申請不存在");

            // 已經併入的申請 Id
            var linkedIds = await _db.DispatchApplications
                .Where(l => l.DispatchId == dispatchId)
                .Select(l => l.ApplyId)
                .ToListAsync();

            // 要排除的：主申請 + 已併入
            var excludeIds = new HashSet<int>(linkedIds);
            excludeIds.Add(dispatch.ApplyId);

            // 篩選候選：完成審核 or 待派車，時間需與主申請重疊
            var apps = await _db.CarApplications
                .Where(a =>
                    (a.Status == "完成審核" || a.Status == "待派車") &&
                    !(a.UseEnd < mainApp.UseStart || a.UseStart > mainApp.UseEnd) &&
                    !excludeIds.Contains(a.ApplyId))
                .OrderBy(a => a.UseStart)
                .Select(a => new {
                    
                    a.Origin,
                    a.Destination,
                    a.UseStart,
                    a.UseEnd,
                    a.PassengerCount,
                    DispatchId = _db.Dispatches
                  .Where(d => d.ApplyId == a.ApplyId)
                  .Select(d => d.DispatchId)
                  .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(apps);
        }


    }
}

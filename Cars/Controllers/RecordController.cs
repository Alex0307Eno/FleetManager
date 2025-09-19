using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<RecordController> _logger;

        public RecordController(ApplicationDbContext db, ILogger<RecordController> logger)
        {
            _db = db;
            _logger = logger;
        }

        # region 派車單列表
        //派車單列表
        [HttpGet("records")]
        public async Task<ActionResult<IEnumerable<Record>>> GetRecords(
            [FromQuery] DateTime? dateFrom,
            [FromQuery] DateTime? dateTo,
            [FromQuery] string? driver,
            [FromQuery] string? applicant,
            [FromQuery] string? plateNo,
            [FromQuery] string? order)
        {
            Console.WriteLine($"[Console] GetRecords called: dateFrom={dateFrom}, dateTo={dateTo}, driver={driver}, applicant={applicant}, plateNo={plateNo}, order={order}");
            _logger.LogInformation("GetRecords called {@Params}", new { dateFrom, dateTo, driver, applicant, plateNo, order });

            //抓出被選取的子單
            var childIds = await _db.DispatchLinks
            .Select(l => l.ChildDispatchId)
            .ToListAsync();


            var q =
                from d in _db.Dispatches.AsNoTracking()
                where !childIds.Contains(d.DispatchId) //  過濾掉子單
                join v in _db.Vehicles.AsNoTracking() on d.VehicleId equals v.VehicleId into vg
                from v in vg.DefaultIfEmpty()
                join r in _db.Drivers.AsNoTracking() on d.DriverId equals r.DriverId into rg
                from r in rg.DefaultIfEmpty()
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

            if (User.IsInRole("Driver"))
            {
                var uidStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                Console.WriteLine($"[Console] Driver role detected, uid={uidStr}");
                _logger.LogDebug("Driver role detected, uid={Uid}", uidStr);

                if (!int.TryParse(uidStr, out var userId))
                {
                    Console.WriteLine("[Console] Driver uid parse failed");
                    _logger.LogWarning("Driver uid parse failed");
                    return Forbid();
                }

                var myDriverId = await _db.Drivers
                    .AsNoTracking()
                    .Where(d => d.UserId == userId)
                    .Select(d => d.DriverId)
                    .FirstOrDefaultAsync();

                Console.WriteLine($"[Console] myDriverId={myDriverId}");
                _logger.LogDebug("myDriverId={Id}", myDriverId);

                if (myDriverId == 0)
                {
                    Console.WriteLine("[Console] 帳號未綁定司機");
                    _logger.LogWarning("帳號未綁定司機");
                    return Forbid();
                }

                q = q.Where(x => x.DriverId == myDriverId);
            }

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

            var today = DateTime.Today;
            q = (order ?? "id_desc").ToLower() switch
            {
                "today_first" or "future_first"
                    => q.OrderBy(x => x.UseStart >= today ? 0 : 1).ThenBy(x => x.UseStart),
                "id_desc" or "latest"
                    => q.OrderByDescending(x => x.DispatchId),
                "start_desc"
                    => q.OrderByDescending(x => x.UseStart),
                _ => q.OrderBy(x => x.UseStart)
            };

            var rawRows = await q.ToListAsync();
            Console.WriteLine($"[Console] rawRows count={rawRows.Count}");
            _logger.LogInformation("GetRecords rawRows count={Count}", rawRows.Count);

            var applyIds = rawRows.Select(x => x.ApplyId).Distinct().ToList();
            var dispatchIds = rawRows.Select(x => x.DispatchId).Distinct().ToList();

            var linkAgg = await _db.DispatchLinks
                .Where(l => dispatchIds.Contains(l.ParentDispatchId))
                .Join(_db.Dispatches,
                      l => l.ChildDispatchId,
                      d => d.DispatchId,
                      (l, d) => new { l.ParentDispatchId, d.CarApply.PassengerCount })
                .GroupBy(x => x.ParentDispatchId)
                .Select(g => new
                {
                    DispatchId = g.Key,
                    LinkSeats = g.Sum(x => (int?)x.PassengerCount) ?? 0, 
                    LinkCount = g.Count()
                })
                .ToListAsync();

            _logger.LogDebug("linkAgg count={Count}", linkAgg.Count);


            var linkDetails = await
                (from l in _db.DispatchLinks.AsNoTracking()
                 join d in _db.Dispatches.AsNoTracking() on l.ChildDispatchId equals d.DispatchId
                 join a in _db.CarApplications.AsNoTracking() on d.ApplyId equals a.ApplyId
                 join p in _db.Applicants.AsNoTracking() on a.ApplicantId equals p.ApplicantId
                 where dispatchIds.Contains(l.ParentDispatchId)
                 select new
                 {
                     ParentDispatchId = l.ParentDispatchId,
                     ChildDispatchId = d.DispatchId,  
                     ApplyId = a.ApplyId,
                     a.UseStart,
                     a.UseEnd,
                     a.Origin,
                     a.Destination,
                     a.ReasonType,
                     a.ApplyReason,
                     ApplicantName = p.Name,
                     Seats = a.PassengerCount,
                     a.TripType,
                     a.SingleDistance,
                     a.RoundTripDistance,
                     a.Status
                 }).ToListAsync();
            _logger.LogDebug("linkDetails count={Count}", linkDetails.Count);
            //母單
               

            var rows = new List<Record>();
            foreach (var x in rawRows)
            {
                // 找出這個母單底下的子單
                var children = linkDetails
                  .Where(ld => ld.ParentDispatchId == x.DispatchId)
                  .OrderBy(ld => ld.UseStart)
                  .ToList();
                //  計算合併後的時間範圍
                var allStarts = new List<DateTime>();
                var allEnds = new List<DateTime>();

                if (x.UseStart != null) allStarts.Add(x.UseStart);
                if (x.UseEnd != null) allEnds.Add(x.UseEnd);

                allStarts.AddRange(children.Where(c => c.UseStart != null).Select(c => c.UseStart));
                allEnds.AddRange(children.Where(c => c.UseEnd != null).Select(c => c.UseEnd));

                var minStart = allStarts.Any() ? allStarts.Min() : x.UseStart;
                var maxEnd = allEnds.Any() ? allEnds.Max() : x.UseEnd;
                //母單里程判斷
                decimal km = 0;
                if (x.TripType == "single") km = x.SingleDistance ?? 0;
                else if (x.TripType == "round") km = x.RoundTripDistance ?? 0;
                var longShort = km > 30 ? "長差" : "短差";

                var agg = linkAgg.FirstOrDefault(g => g.DispatchId == x.DispatchId)
                          ?? new { DispatchId = x.DispatchId, LinkSeats = 0, LinkCount = 0 };
                var totalSeats = (x.PassengerCount) + agg.LinkSeats;

                rows.Add(new Record
                {
                    DispatchId = x.DispatchId,
                    ApplyId = x.ApplyId,
                    UseStart = minStart,
                    UseEnd = maxEnd,
                    Route = string.Join(" - ", new[] { x.Origin, x.Destination }.Where(s => !string.IsNullOrWhiteSpace(s))),
                    TripType = x.TripType,
                    ReasonType = x.ReasonType,
                    Reason = x.ApplyReason,
                    Applicant = x.ApplicantName,
                    Seats = totalSeats,
                    Km = km,
                    Status = x.Status,
                    Driver = x.DriverName,
                    DriverId = x.DriverId,
                    Plate = x.PlateNo,
                    VehicleId = x.VehicleId,
                    LongShort = longShort,
                    ChildDispatchId = null, // 主單沒有 ChildDispatchId
                });


                //子單
               
                foreach (var c in children)
                {
                    decimal km2 = 0;
                    if (c.TripType == "single") km2 = c.SingleDistance ?? 0;
                    else if (c.TripType == "round") km2 = c.RoundTripDistance ?? 0;
                    var ls2 = km2 > 30 ? "長差" : "短差";
                
                    rows.Add(new Record
                    {
                        DispatchId = x.DispatchId,
                        ApplyId = c.ApplyId,
                        ChildDispatchId = c.ChildDispatchId,
                        UseStart = c.UseStart,
                        UseEnd = c.UseEnd,
                        Route = string.Join(" - ", new[] { c.Origin, c.Destination }.Where(s => !string.IsNullOrWhiteSpace(s))),
                        TripType = c.TripType,
                        ReasonType = c.ReasonType,
                        Reason = c.ApplyReason,
                        Applicant = c.ApplicantName,
                        Seats = c.Seats,
                        Km = km2,
                        Status = c.Status,
                        Driver = x.DriverName,
                        DriverId = x.DriverId,
                        Plate = x.PlateNo,
                        VehicleId = x.VehicleId,
                        LongShort = ls2
                    });
                }
            }

            Console.WriteLine($"[Console] rows final count={rows.Count}");
            _logger.LogInformation("GetRecords final rows count={Count}", rows.Count);

            return Ok(rows);
        }
        #endregion

        #region 檢視單筆派車單
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOne(int id)
        {
            Console.WriteLine($"[Console] GetOne called id={id}");
            _logger.LogInformation("GetOne called id={Id}", id);

            var d = await _db.Dispatches
                .AsNoTracking()
                .Include(x => x.Vehicle)
                .Include(x => x.Driver)
                .Include(x => x.CarApply)
                .ThenInclude(ca => ca.Applicant)
                .FirstOrDefaultAsync(x => x.DispatchId == id);

            if (d == null)
            {
                Console.WriteLine($"[Console] GetOne not found: {id}");
                _logger.LogWarning("GetOne: dispatch {Id} not found", id);
                return NotFound();
            }

            Console.WriteLine($"[Console] GetOne found DispatchId={d.DispatchId}, ApplyId={d.ApplyId}, DriverId={d.DriverId}");
            _logger.LogDebug("GetOne found DispatchId={DispatchId}, ApplyId={ApplyId}, DriverId={DriverId}", d.DispatchId, d.ApplyId, d.DriverId);

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
        #endregion

        #region 更新派車單狀態
        public class UpdateDispatchDto
        {
            public int? DriverId { get; set; }
            public int? VehicleId { get; set; }
        }
        
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDispatch(int id, [FromBody] UpdateDispatchDto dto)
        {
            Console.WriteLine($"[Console] UpdateDispatch id={id}, body={JsonSerializer.Serialize(dto)}");
            _logger.LogInformation("UpdateDispatch id={Id}, body={@Dto}", id, dto);

            var dispatch = await _db.Dispatches.FindAsync(id);
            if (dispatch == null)
            {
                Console.WriteLine($"[Console] UpdateDispatch not found: {id}");
                _logger.LogWarning("UpdateDispatch: dispatch {Id} not found", id);
                return NotFound();
            }

            dispatch.DriverId = dto.DriverId;
            dispatch.VehicleId = dto.VehicleId;
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
            Console.WriteLine($"[Console] UpdateDispatch OK: {dispatch.DispatchId}");
            _logger.LogInformation("UpdateDispatch OK: {@Dispatch}", dispatch);

            return Ok(new { message = "更新成功", dispatch.DispatchId, dispatch.DriverId, dispatch.VehicleId });
        }
        #endregion

        #region 刪除派車單
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            Console.WriteLine($"[Console] Delete called id={id}");
            _logger.LogInformation("Delete called id={Id}", id);

            var row = await _db.Dispatches.FindAsync(id);
            if (row == null)
            {
                Console.WriteLine($"[Console] Delete not found: {id}");
                _logger.LogWarning("Delete: dispatch {Id} not found", id);
                return NotFound();
            }

            // 1️找出子單連結
            var childLinks = await _db.DispatchLinks
                .Where(l => l.ParentDispatchId == id)
                .ToListAsync();

            if (childLinks.Any())
            {
                // 2️先移除連結
                _db.DispatchLinks.RemoveRange(childLinks);

                // 3️ 找出子單，清空駕駛與車輛（回復獨立狀態）
                var childIds = childLinks.Select(l => l.ChildDispatchId).ToList();
                var children = await _db.Dispatches
                    .Where(d => childIds.Contains(d.DispatchId))
                    .ToListAsync();

                foreach (var c in children)
                {
                    c.DriverId = null;
                    c.VehicleId = null;
                }

                Console.WriteLine($"[Console] Delete: detached {children.Count} children from parent {id}");
                _logger.LogInformation("Delete: detached {Count} children from parent {Id}", children.Count, id);
            }

            // 4️刪掉母單本身
            _db.Dispatches.Remove(row);
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
            Console.WriteLine($"[Console] Delete OK: {id}");
            _logger.LogInformation("Delete OK: {Id}", id);

            return Ok(new { message = "刪除成功" });
        }

        #endregion

        //共乘功能

        #region 派車單併單功能
     


        [HttpPost("{parentId}/links")]
        public async Task<IActionResult> AddLink(int parentId, [FromBody] int childDispatchId)
        {
            var parent = await _db.Dispatches
                .Include(d => d.Vehicle)
                .Include(d => d.CarApply)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DispatchId == parentId);

            if (parent == null) return NotFound("母單不存在");
            if (parent.Vehicle == null) return BadRequest("母單尚未指派車輛，無法併單");

            var capacity = parent.Vehicle.Capacity ?? 0;
            var mainSeats = parent.CarApply?.PassengerCount ?? 0;

            var usedByLinks = await (
                from l in _db.DispatchLinks
                join d in _db.Dispatches on l.ChildDispatchId equals d.DispatchId
                join a in _db.CarApplications on d.ApplyId equals a.ApplyId
                where l.ParentDispatchId == parentId
                select (int?)a.PassengerCount
            ).SumAsync() ?? 0;

            var remaining = capacity - (mainSeats + usedByLinks);
            if (remaining < 0) remaining = 0;

            var childApp = await (
                from d in _db.Dispatches
                join a in _db.CarApplications on d.ApplyId equals a.ApplyId
                where d.DispatchId == childDispatchId
                select new { d.DispatchId, a.ApplyId, a.PassengerCount }
            ).FirstOrDefaultAsync();

            if (childApp == null) return BadRequest("子單不存在");

            var seatsWanted = childApp.PassengerCount;
            if (seatsWanted > remaining)
            {
                return BadRequest($"剩餘座位 {remaining}，不足以併入 {seatsWanted} 人");
            }

            var link = new DispatchLink
            {
                ParentDispatchId = parentId,
                ChildDispatchId = childApp.DispatchId
            };
            _db.DispatchLinks.Add(link);
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
            return Ok(new { message = "併入成功", remainingAfter = remaining - seatsWanted });
        }



        #endregion

        #region 取消併單

        [HttpDelete("{dispatchId}/links/{childDispatchId}")]
        public async Task<IActionResult> RemoveLink(int dispatchId, int childDispatchId)
        {
            var link = await _db.DispatchLinks.FindAsync(dispatchId, childDispatchId);
            if (link == null)
                return NotFound();

            _db.DispatchLinks.Remove(link);

            // 移除關聯時，子單駕駛與車輛清空
            var child = await _db.Dispatches.FindAsync(childDispatchId);
            if (child != null)
            {
                child.DriverId = null;
                child.VehicleId = null;
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
            return Ok(new { message = "已取消併單" });
        }
        #endregion

        #region 列出併單清單
        [HttpGet("{dispatchId}/links")]
        public async Task<IActionResult> ListLinks(int dispatchId)
        {
            var rows = await (
                from l in _db.DispatchLinks
                join d in _db.Dispatches on l.ChildDispatchId equals d.DispatchId
                join a in _db.CarApplications on d.ApplyId equals a.ApplyId
                where l.ParentDispatchId == dispatchId
                select new
                {
                    d.DispatchId,
                    d.ApplyId,
                    a.Origin,
                    a.Destination,
                    a.UseStart,
                    a.UseEnd,
                    Seats = a.PassengerCount  
                }
            ).ToListAsync();

            return Ok(rows);
        }

        #endregion

        #region 取得可併入的申請單列表

        [HttpGet("{dispatchId}/available-apps")]
        public async Task<IActionResult> GetAvailableAppsForDispatch(int dispatchId)
        {
            var parent = await _db.Dispatches
                .Include(d => d.Vehicle)
                .Include(d => d.CarApply)
                .FirstOrDefaultAsync(d => d.DispatchId == dispatchId);

            if (parent == null)
                return NotFound("派車單不存在");

            var capacity = parent.Vehicle?.Capacity ?? 0;
            var mainSeats = parent.CarApply?.PassengerCount ?? 0;

            // 已併入子單的人數
            var usedByLinks = await (
                from l in _db.DispatchLinks
                join d in _db.Dispatches on l.ChildDispatchId equals d.DispatchId
                join a in _db.CarApplications on d.ApplyId equals a.ApplyId
                where l.ParentDispatchId == dispatchId
                select (int?)a.PassengerCount
            ).SumAsync() ?? 0;

            var remaining = capacity - (mainSeats + usedByLinks);
            if (remaining < 0) remaining = 0;

            // 排除：自己、已併入的、已經是母單的
            var linkedIds = await _db.DispatchLinks
                .Where(l => l.ParentDispatchId == dispatchId)
                .Select(l => l.ChildDispatchId)
                .ToListAsync();

            var motherIds = await _db.DispatchLinks
                .Select(l => l.ParentDispatchId)
                .Distinct()
                .ToListAsync();

            var exclude = new HashSet<int>(linkedIds);
            exclude.Add(dispatchId);
            foreach (var m in motherIds) exclude.Add(m);

            var now = DateTime.Now;

            // ✅ 不再用 (a.PassengerCount <= remaining) 過濾
            var apps = await (
                from d in _db.Dispatches
                join a in _db.CarApplications on d.ApplyId equals a.ApplyId
                where a.Status == "完成審核"
                      && a.UseEnd > now
                      && !exclude.Contains(d.DispatchId)
                orderby a.UseStart
                select new
                {
                    d.DispatchId,
                    a.ApplyId,
                    a.Origin,
                    a.Destination,
                    a.UseStart,
                    a.UseEnd,
                    Seats = a.PassengerCount
                }
            ).ToListAsync();

            return Ok(new { remainingSeats = remaining, apps });
        }




        #endregion
    }
}

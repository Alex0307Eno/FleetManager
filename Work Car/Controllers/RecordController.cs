using Cars.Data;
using Cars.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Logging;

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

            var q =
                from d in _db.Dispatches.AsNoTracking()
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
                .GroupBy(l => l.ParentDispatchId)
                .Select(g => new
                {
                    DispatchId = g.Key,
                    LinkSeats = g.Sum(x => (int?)x.Seats) ?? 0,
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
                     Seats = l.Seats,
                     a.TripType,
                     a.SingleDistance,
                     a.RoundTripDistance,
                     a.Status
                 }).ToListAsync();
            _logger.LogDebug("linkDetails count={Count}", linkDetails.Count);

            var rows = new List<Record>();
            foreach (var x in rawRows)
            {
                decimal km = 0;
                if (x.TripType == "single") km = x.SingleDistance ?? 0;
                else if (x.TripType == "round") km = x.RoundTripDistance ?? 0;
                var longShort = km > 30 ? "長差" : "短差";

                var agg = linkAgg.FirstOrDefault(g => g.DispatchId == x.DispatchId)
                          ?? new { DispatchId = x.DispatchId, LinkSeats = 0, LinkCount = 0 };
                var totalSeats = (x.PassengerCount) + agg.LinkSeats;

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

                var children = linkDetails.Where(ld => ld.ParentDispatchId == x.DispatchId).OrderBy(ld => ld.UseStart).ToList();
                foreach (var c in children)
                {
                    decimal km2 = 0;
                    if (c.TripType == "single") km2 = c.SingleDistance ?? 0;
                    else if (c.TripType == "round") km2 = c.RoundTripDistance ?? 0;
                    var ls2 = km2 > 30 ? "長差" : "短差";

                    rows.Add(new Record
                    {
                        Id = x.DispatchId,
                        ApplyId = c.ApplyId,
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
                        Driver = null,
                        DriverId = null,
                        Plate = null,
                        VehicleId = null,
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
            await _db.SaveChangesAsync();

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

            _db.Dispatches.Remove(row);
            await _db.SaveChangesAsync();

            Console.WriteLine($"[Console] Delete OK: {id}");
            _logger.LogInformation("Delete OK: {Id}", id);

            return Ok(new { message = "刪除成功" });
        }
        #endregion



        #region 派車單併單功能
        public class MergeLinkDto
        {
            public int DispatchId { get; set; }
            public int? Seats { get; set; }
        }

        [HttpPost("{dispatchId}/links")]
        public async Task<IActionResult> AddLink(int dispatchId, [FromBody] MergeLinkDto dto)
        {
            if (dispatchId == dto.DispatchId)
                return BadRequest("不能將同一張派車單併入自己");

            var exists = await _db.DispatchLinks.FindAsync(dispatchId, dto.DispatchId);
            if (exists != null)
                return BadRequest("此派車單已經併入過");

            var link = new DispatchLink
            {
                ParentDispatchId = dispatchId,
                ChildDispatchId = dto.DispatchId,
                Seats = dto.Seats ?? 0
            };

            _db.DispatchLinks.Add(link);
            await _db.SaveChangesAsync();

            return Ok(new { message = "併單成功", parent = dispatchId, child = dto.DispatchId });
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
            await _db.SaveChangesAsync();

            return Ok(new { message = "已取消併單" });
        }
        #endregion

        #region 列出併單清單

        [HttpGet("{dispatchId}/links")]
        public async Task<IActionResult> ListLinks(int dispatchId)
        {
            var rows = await _db.DispatchLinks
                .Where(x => x.ParentDispatchId == dispatchId)
                .Join(_db.Dispatches,
                      dl => dl.ChildDispatchId,
                      d => d.DispatchId,
                      (dl, d) => new {
                          d.DispatchId,
                          d.ApplyId,
                          d.CarApply.Origin,
                          d.CarApply.Destination,
                          d.CarApply.UseStart,
                          d.CarApply.UseEnd,
                          dl.Seats
                      })
                .ToListAsync();

            return Ok(rows);
        }
        #endregion

        #region 取得可併入的申請單列表

        [HttpGet("{dispatchId}/available-apps")]
        public async Task<IActionResult> GetAvailableAppsForDispatch(int dispatchId)
        {
            Console.WriteLine($"[Console] AvailApps host={dispatchId}");
            _logger.LogInformation("AvailApps host={Host}", dispatchId);

            var dispatch = await _db.Dispatches.FindAsync(dispatchId);
            if (dispatch == null)
            {
                Console.WriteLine($"[Console] AvailApps: dispatch {dispatchId} not found");
                _logger.LogWarning("AvailApps: dispatch {Host} not found", dispatchId);
                return NotFound("派車單不存在");
            }

            var mainApp = await _db.Dispatches.FindAsync(dispatch.DispatchId);
            if (mainApp == null)
            {
                Console.WriteLine($"[Console] AvailApps: mainApp not found, applyId={dispatch.DispatchId}");
                _logger.LogWarning("AvailApps: mainApp not found Host={Host}, ApplyId={ApplyId}", dispatchId, dispatch.ApplyId);
                return BadRequest("主申請不存在");
            }

            var linkedIds = await _db.DispatchLinks
                .Where(l => l.ParentDispatchId == dispatchId)
                .Select(l => l.ChildDispatchId)
                .ToListAsync();

            var excludeIds = new HashSet<int>(linkedIds);
            excludeIds.Add(dispatch.DispatchId);

            Console.WriteLine($"[Console] AvailApps excludeIds count={excludeIds.Count}");
            _logger.LogDebug("AvailApps excludeIds={@Ids}", excludeIds);

            var apps = await (
            from d in _db.Dispatches
            join a in _db.CarApplications on d.ApplyId equals a.ApplyId
            where
                (a.Status == "完成審核" || a.Status == "待派車") &&
                !(a.UseEnd < mainApp.StartTime || a.UseStart > mainApp.EndTime) &&
                !excludeIds.Contains(d.DispatchId)    
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


            Console.WriteLine($"[Console] AvailApps return count={apps.Count}");
            _logger.LogInformation("AvailApps return {Count} items host={Host}", apps.Count, dispatchId);

            return Ok(apps);
            #endregion
        }
    }
}

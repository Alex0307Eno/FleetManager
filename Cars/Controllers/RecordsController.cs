using Cars.Data;
using Cars.Features.CarApplications;
using Cars.Migrations;
using Cars.Models;
using Cars.Services;
using Cars.Services.Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace Cars.ApiControllers
{
    [Authorize]
    [ApiController]
    [Route("api/dispatch")]
    public class RecordsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<RecordsController> _logger;
        private readonly DispatchService _dispatchService;


        public RecordsController(ApplicationDbContext db, ILogger<RecordsController> logger, DispatchService dispatchService)
        {
            _db = db;
            _logger = logger;
            _dispatchService = dispatchService;
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
                join v in _db.Vehicles.AsNoTracking() on d.VehicleId equals v.VehicleId into vg
                from v in vg.DefaultIfEmpty()
                join r in _db.Drivers.AsNoTracking() on d.DriverId equals r.DriverId into rg
                from r in rg.DefaultIfEmpty()
                join a in _db.CarApplications.AsNoTracking() on d.ApplyId equals a.ApplyId
                join p in _db.Applicants.AsNoTracking() on a.ApplicantId equals p.ApplicantId
                where !childIds.Contains(d.DispatchId)
                && a.Status != "駁回"  // 只看有效申請單 
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
                    d.DispatchStatus,
                    DriverId = r != null ? r.DriverId : (int?)null,
                    DriverName = r != null ? r.DriverName : null,
                    VehicleId = v != null ? v.VehicleId : (int?)null,
                    PlateNo = v != null ? v.PlateNo : null,
                    VehicleCapacity = v != null ? (v.Capacity ?? 0) : 0,
                    d.OdometerStart,
                    d.OdometerEnd
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
                      (l, d) => new { l.ParentDispatchId, d.CarApplication.PassengerCount })
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
                     d.DispatchStatus,
                     d.OdometerStart,
                     d.OdometerEnd
                 }).ToListAsync();
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
                    Status = x.DispatchStatus,
                    Driver = x.DriverName,
                    DriverId = x.DriverId,
                    Plate = x.PlateNo,
                    VehicleId = x.VehicleId,
                    LongShort = longShort,
                    ChildDispatchId = null,
                    OdometerStart = x.OdometerStart,
                    OdometerEnd = x.OdometerEnd
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
                        Status = x.DispatchStatus,
                        Driver = x.DriverName,
                        DriverId = x.DriverId,
                        Plate = x.PlateNo,
                        VehicleId = x.VehicleId,
                        LongShort = ls2,
                        OdometerStart = x.OdometerStart,
                        OdometerEnd = x.OdometerEnd
                    });
                }
            }

            Console.WriteLine($"[Console] rows final count={rows.Count}");
            _logger.LogInformation("GetRecords final rows count={Count}", rows.Count);

            return Ok(rows);
        }
        #endregion

        #region 更新派車單狀態
        
        
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateDispatch(int id, [FromBody] AssignDto dto)
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

            // 1. 更新 Dispatch
            dispatch.DriverId = dto.DriverId;
            dispatch.VehicleId = dto.VehicleId;
            dispatch.DispatchStatus = "已派車";

            // 2. 更新 CarApplication 對應的 DriverId/VehicleId
            var app = await _db.CarApplications.FindAsync(dispatch.ApplyId);
            if (app != null)
            {
                app.DriverId = dto.DriverId;
                app.VehicleId = dto.VehicleId;
                app.Status = "完成審核"; // 這邊可以依照你的流程改，若只要改派車狀態可省略
            }

            var (ok1,err1) = await _db.TrySaveChangesAsync(this);
            if (!ok1) return err1!;
            // 重新排程提醒
            DispatchJobScheduler.ScheduleRideReminders(dispatch);
            // 3. 紀錄異動
            _db.DispatchAudits.Add(new Cars.Models.DispatchAudit
            {
                DispatchId = dispatch.DispatchId,
                Action = "Assign",
                OldValue = JsonSerializer.Serialize(new
                {
                    OldDriverId = app?.DriverId,
                    OldVehicleId = app?.VehicleId
                }),
                NewValue = JsonSerializer.Serialize(new
                {
                    dispatch.DriverId,
                    dispatch.VehicleId
                }),
                ByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                ByUserName = User.Identity?.Name
            });
            var (ok2, err2) = await _db.TrySaveChangesAsync(this);
            if (!ok2) return err2!;
            Console.WriteLine($"[Console] UpdateDispatch OK: {dispatch.DispatchId}");
            _logger.LogInformation("UpdateDispatch OK: {@Dispatch}", dispatch);

            return Ok(new
            {
                message = "更新成功",
                dispatch.DispatchId,
                dispatch.DriverId,
                dispatch.VehicleId,
                appId = dispatch.ApplyId
            });
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
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!; 
            Console.WriteLine($"[Console] Delete OK: {id}");
            _logger.LogInformation("Delete OK: {Id}", id);

            return Ok(new { message = "刪除成功" });
        }

        #endregion

        #region 異動紀錄
        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetHistory(int id)
        {
            var rows = await _db.DispatchAudits
                .AsNoTracking()
                .Where(x => x.DispatchId == id)
                .OrderByDescending(x => x.At)
                .ToListAsync();

            var drivers = await _db.Drivers.ToDictionaryAsync(d => d.DriverId, d => d.DriverName);
            var vehicles = await _db.Vehicles.ToDictionaryAsync(v => v.VehicleId, v => v.PlateNo);

            var taiwanTz = TimeZoneInfo.FindSystemTimeZoneById("Taipei Standard Time");

            var result = rows.Select(r => new {
                At = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(r.At, DateTimeKind.Utc), taiwanTz)
                                  .ToString("yyyy/MM/dd HH:mm:ss"), // 直接轉字串
                Action = r.Action switch
                {
                    "Assign" => "指派",
                    "Create" => "建立",
                    "Update" => "更新",
                    "Delete" => "刪除",
                    _ => r.Action
                },
                r.ByUserName,
                OldValue = TranslateValues(r.OldValue, drivers, vehicles),
                NewValue = TranslateValues(r.NewValue, drivers, vehicles)
            });

            return Ok(result);
        }


        private string TranslateValues(string raw,
            Dictionary<int, string> drivers,
            Dictionary<int, string> vehicles)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "—";

            try
            {
                var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(raw);
                if (dict == null || dict.Count == 0) return "—";

                var parts = new List<string>();
                foreach (var kv in dict)
                {
                    string label = kv.Key switch
                    {
                        "DriverId" or "OldDriverId" => "駕駛",
                        "VehicleId" or "OldVehicleId" => "車輛",
                        _ => kv.Key
                    };

                    string value = "";
                    if (kv.Key.Contains("Driver") && int.TryParse(kv.Value?.ToString(), out int did))
                        value = drivers.ContainsKey(did) ? drivers[did] : $"ID={did}";
                    else if (kv.Key.Contains("Vehicle") && int.TryParse(kv.Value?.ToString(), out int vid))
                        value = vehicles.ContainsKey(vid) ? vehicles[vid] : $"ID={vid}";
                    else
                        value = kv.Value?.ToString() ?? "";

                    parts.Add($"{label}={value}");
                }
                return string.Join("，", parts);
            }
            catch
            {
                return raw;
            }
        }



        #endregion




        //共乘功能

        #region 派車單併單功能



        [HttpPost("{parentId}/links")]
        public async Task<IActionResult> AddLink(int parentId, [FromBody] int childDispatchId)
        {
            var parent = await _db.Dispatches
                .Include(d => d.Vehicle)
                .Include(d => d.CarApplication)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DispatchId == parentId);

            if (parent == null) return NotFound("母單不存在");
            if (parent.Vehicle == null) return BadRequest("母單尚未指派車輛，無法併單");

            var capacity = parent.Vehicle.Capacity ?? 0;
            var mainSeats = parent.CarApplication?.PassengerCount ?? 0;

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
            // 找子單的 Dispatch + CarApplication
            var childDispatch = await _db.Dispatches
                .Include(d => d.CarApplication)
                .FirstOrDefaultAsync(d => d.DispatchId == childDispatchId);

            if (childDispatch != null)
            {
                // 🚗 更新子單 Dispatch
                childDispatch.DriverId = parent.DriverId;
                childDispatch.VehicleId = parent.VehicleId;

                // 📄 同步更新 CarApplication
                if (childDispatch.CarApplication != null)
                {
                    childDispatch.CarApplication.DriverId = parent.DriverId;
                    childDispatch.CarApplication.VehicleId = parent.VehicleId;
                }
            }
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;
            _db.DispatchAudits.Add(new Cars.Models.DispatchAudit
            {
                DispatchId = parentId,            // AddLink：母單記一筆
                Action = "LinkAdd",
                NewValue = JsonSerializer.Serialize(new { ChildDispatchId = childDispatchId }),
                ByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                ByUserName = User.Identity?.Name
            });
            var (ok2, err2) = await _db.TrySaveChangesAsync(this);
            if (!ok2) return err2!;

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
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;
            _db.DispatchAudits.Add(new Cars.Models.DispatchAudit
            {
                DispatchId = dispatchId,          // RemoveLink：母單記一筆
                Action = "LinkRemove",
                OldValue = JsonSerializer.Serialize(new { ChildDispatchId = childDispatchId }),
                ByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                ByUserName = User.Identity?.Name
            });
            var (ok2, err2) = await _db.TrySaveChangesAsync(this);
            if (!ok2) return err2!;

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
                .Include(d => d.CarApplication)
                .FirstOrDefaultAsync(d => d.DispatchId == dispatchId);

            if (parent == null)
                return NotFound("派車單不存在");

            var capacity = parent.Vehicle?.Capacity ?? 0;
            var mainSeats = parent.CarApplication?.PassengerCount ?? 0;

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

            
            var apps = await (
                from d in _db.Dispatches
                join a in _db.CarApplications on d.ApplyId equals a.ApplyId
                where d.DispatchStatus == "已派車"
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

        #region 車輛里程更新

        public class TripStartDto { public int? OdometerStart { get; set; } }
        public class TripEndDto { public int? OdometerEnd { get; set; } }

        [HttpPost("{id}/start")]
        public async Task<IActionResult> StartTrip(int id, [FromBody] TripStartDto dto)
        {
            if (dto == null || dto.OdometerStart <= 0)
                return BadRequest(new { message = "請輸入有效的起始里程數" });

            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(uid, out var userId)) return Forbid();

            var driverId = await _db.Drivers.Where(d => d.UserId == userId)
                                            .Select(d => d.DriverId)
                                            .FirstOrDefaultAsync();
            if (driverId == 0) return Forbid();

            var msg = await _dispatchService.SaveStartOdometerAsync(id, driverId, dto.OdometerStart.Value);
            return msg.StartsWith("⚠️") ? BadRequest(new { message = msg }) : Ok(new { message = msg });
        }


        [HttpPost("{id}/end")]
        public async Task<IActionResult> EndTrip(int id, [FromBody] TripEndDto dto)
        {
            if (dto == null || dto.OdometerEnd <= 0)
                return BadRequest(new { message = "請輸入有效的結束里程數" });

            var uid = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(uid, out var userId)) return Forbid();

            var driverId = await _db.Drivers.Where(d => d.UserId == userId)
                                            .Select(d => d.DriverId)
                                            .FirstOrDefaultAsync();
            if (driverId == 0) return Forbid();

            var msg = await _dispatchService.SaveEndOdometerAsync(id, driverId, dto.OdometerEnd.Value);
            return msg.StartsWith("⚠️") ? BadRequest(new { message = msg }) : Ok(new { message = msg });
        }


        // 取得車輛目前里程
        [HttpGet("vehicles/{id}/odometer")]
        public async Task<IActionResult> GetVehicleOdometer(int id)
        {
            var v = await _db.Vehicles
                .Where(v => v.VehicleId == id)
                .Select(v => new { v.VehicleId, v.PlateNo, v.Odometer })
                .FirstOrDefaultAsync();

            if (v == null)
                return NotFound(new { message = "找不到車輛" });

            return Ok(v);
        }
        #endregion

    }
}

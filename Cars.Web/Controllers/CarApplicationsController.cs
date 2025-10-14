using Cars.Application.Services;
using Cars.Data;
using Cars.Models;
using Cars.Web.Services;
using Cars.Shared.Dtos.CarApplications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Cars.ApiControllers
{
    [ApiController]
    [Route("api/[controller]")]
    
    public class CarApplicationsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IDistanceService _distance;
        private readonly CarApplicationService _carApplicationService;
        private readonly VehicleService _vehicleService;
        private readonly AutoDispatcher _dispatcher;
        private readonly CarApplicationUseCase _usecase;




        public CarApplicationsController(ApplicationDbContext db,  IDistanceService distance, CarApplicationService carApplicationService, VehicleService vehicleService, AutoDispatcher dispatcher, CarApplicationUseCase usecase)
        {
            _db = db;
            _distance = distance;
            _carApplicationService = carApplicationService;
            _vehicleService = vehicleService;
            _dispatcher = dispatcher;
            _usecase = usecase;
        }
        // 取得申請單列表
        [HttpGet("all")]
        public async Task<IActionResult> GetApplications(DateTime? from, DateTime? to, string? q)
        {
            
            var list = await _carApplicationService.GetAll(from, to, q, User);
            return Ok(list);
        }

        #region 建立申請單
        // 建立申請單（含搭乘人員清單）

        [HttpPost("create")]
        [Authorize(Roles = "Admin,Applicant,Manager")]
        public async Task<IActionResult> Create([FromBody] CarApplicationDto dto)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uid, out var userId))
                return Unauthorized("尚未登入");

            var (ok, msg, app) = await _carApplicationService.CreateAsync(dto, userId);
            if (!ok)
                return BadRequest(new { success = false, message = msg });

            return Ok(new { success = true, message = msg, data = ToResponseData(app) });

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
                .Select(a => new ApplicantDto
                {
                    ApplicantId = a.ApplicantId,
                    Name = a.Name,
                    Dept = a.Dept,
                    Email = a.Email,
                    Ext = a.Ext,
                    Birth = a.Birth
                }).ToListAsync();

            return Ok(ApiResponse<List<ApplicantDto>>.Ok(list, "申請人清單取得成功"));
        }

        // 取得全部申請單
        [Authorize(Roles = "Admin,Applicant,Manager")]
        [HttpGet]
        public async Task<IActionResult> GetAll(DateTime? dateFrom, DateTime? dateTo, string? q)
        {
            var list = await _carApplicationService.GetAll(dateFrom, dateTo, q, User);
                return Ok(ApiResponse<List<CarApplicationDto>>.Ok(list, "查詢成功"));
        }


        #endregion

        #region 刪除申請單
        // 刪除申請單（連同搭乘人員）
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var app = await _db.CarApplications
                .Include(c => c.Applicant)
                .FirstOrDefaultAsync(x => x.ApplyId == id);

            if (app == null)
                return NotFound(ApiResponse.Fail<object>("找不到申請單"));

            // 先組成紀錄用的 JSON（避免循環引用）
            var oldData = new
            {
                申請單號 = app.ApplyId,
                申請人 = app.Applicant?.Name,
                部門 = app.Applicant?.Dept,
                狀態 = app.Status,
                駕駛 = app.DriverId,
                車輛 = app.VehicleId
            };
            var oldJson = JsonSerializer.Serialize(oldData);

            // 紀錄異動
            await LogAppAuditAsync(
                app.ApplyId,
                "刪除",
                User.Identity?.Name ?? "系統",
                oldJson,
                null
            );

            // 先刪子表（派工單）
            var dispatches = _db.Dispatches.Where(d => d.ApplyId == id);
            _db.Dispatches.RemoveRange(dispatches);

            // 再刪子表（乘客）
            var passengers = _db.CarPassengers.Where(p => p.ApplyId == id);
            _db.CarPassengers.RemoveRange(passengers);

            // 最後刪掉主表（申請單）
            _db.CarApplications.Remove(app);

            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;

            return Ok(ApiResponse.Ok<object>(null, "刪除成功，已記錄異動"));
        }


        #endregion

        #region 取得申請單明細
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var app = await _db.CarApplications
                .Include(a => a.Applicant)
                .Include(a => a.Driver)
                .Include(a => a.Vehicle)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.ApplyId == id);

            if (app == null)
                return NotFound(ApiResponse.Fail<object>("找不到申請單"));

            return Ok(ApiResponse.Ok(new
            {
                app.ApplyId,
                app.Status,
                app.PassengerCount,
                app.UseStart,
                app.UseEnd,
                DriverId = app.DriverId,
                DriverName = app.Driver?.DriverName,
                VehicleId = app.VehicleId,
                PlateNo = app.Vehicle?.PlateNo,
                Capacity = app.Vehicle?.Capacity,
                Applicant = app.Applicant?.Name
            }, "查詢成功"));
        }
        #endregion

        #region 更新審核狀態

        [AllowAnonymous]
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Status))
                return BadRequest(ApiResponse.Fail<object>("Status 不可為空"));

            var app = await _db.CarApplications
                .Include(a => a.Applicant)
                .FirstOrDefaultAsync(x => x.ApplyId == id);
            if (app == null)
                return NotFound(ApiResponse.Fail<object>("找不到申請單"));

            var oldStatus = app.Status;
            var newStatus = dto.Status.Trim();   // ★ 在這裡宣告 newStatus

            // 若變更為「完成審核」，嘗試帶入派工人車
            if (newStatus == "完成審核")
            {
                var dispatch = await _db.Dispatches
                    .Where(d => d.ApplyId == app.ApplyId)
                    .OrderByDescending(d => d.DispatchId)
                    .FirstOrDefaultAsync();

                if (dispatch != null && dispatch.DriverId.HasValue)
                {
                    app.DriverId = dispatch.DriverId;
                    app.VehicleId = dispatch.VehicleId;
                }
            }

            app.Status = newStatus;

            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;

            // 異動紀錄
            await LogAppAuditAsync(
                app.ApplyId, "狀態更新", User.Identity?.Name ?? "系統",
                new { 舊狀態 = oldStatus },
                new { 新狀態 = newStatus }
            );

            return Ok(ApiResponse.Ok(new
            {
                status = app.Status,
                driverId = app.DriverId,
                vehicleId = app.VehicleId
            }, "狀態已更新"));
        }

        #endregion




        [Obsolete]
        #region 過濾可用車輛與司機 (可能用不到)


        //找出可用司機和車輛
        [HttpPatch("{id}/assignment")]
        public async Task<IActionResult> UpdateAssignment(int id, [FromBody] AssignDto dto)
        {
            var app = await _db.CarApplications.FindAsync(id);
            if (app == null) return NotFound(ApiResponse<string>.Fail("找不到申請單"));

            var from = app.UseStart;
            var to = app.UseEnd;

            // 驗證車在區間內可用
            if (dto.VehicleId.HasValue)
            {
                var vUsed = await _db.Dispatches.AnyAsync(d =>
                    d.VehicleId == dto.VehicleId &&
                    d.ApplyId != id &&
                    from < d.EndTime && d.StartTime < to);
                if (vUsed) return BadRequest(ApiResponse<string>.Fail("該車於此時段已被派用"));
            }

            // 驗證駕駛在區間內可用
            if (dto.DriverId.HasValue)
            {
                var dUsed = await _db.Dispatches.AnyAsync(d =>
                    d.DriverId == dto.DriverId &&
                    d.ApplyId != id &&
                    from < d.EndTime && d.StartTime < to);
                if (dUsed) return BadRequest(ApiResponse<string>.Fail("該駕駛於此時段已有派工"));
            }

            // 更新 Application
            app.DriverId = dto.DriverId;
            app.VehicleId = dto.VehicleId;

            // 同步 Dispatch
            var disp = await _db.Dispatches.FirstOrDefaultAsync(d => d.ApplyId == id);
            if (disp == null && (dto.DriverId.HasValue || dto.VehicleId.HasValue))
            {
                _db.Dispatches.Add(new Cars.Models.Dispatch
                {
                    ApplyId = id,
                    DriverId = dto.DriverId,
                    VehicleId = dto.VehicleId,
                    DispatchStatus = "待指派", 
                    StartTime = from,
                    EndTime = to,
                    CreatedAt = DateTime.Now
                });
            }
            else if (disp != null && (dto.DriverId.HasValue || dto.VehicleId.HasValue))
            {
                disp.DispatchStatus = "已派車";
            }


            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;

            return Ok(ApiResponse<string>.Ok("指派已更新"));
        }
        #endregion

        #region 審核後自動派車 
        //完成審核自動派車
        [HttpPost("{applyId:int}/approve-assign")]
        public async Task<IActionResult> ApproveAndAssign(
    int applyId,
    [FromQuery] int passengerCount,
    [FromQuery] int? vehicleId = null)
        {
            // 1) 找此申請單對應、未派車的派工
            var dispatch = await _db.Dispatches
            .Where(d => d.ApplyId == applyId
            && d.VehicleId == null)                 
            .OrderByDescending(d => d.DispatchId)
            .FirstOrDefaultAsync();

            if (dispatch == null)
                return NotFound(new { message = "找不到待派車的派工（可能已派車或尚未指派駕駛）。" });

            //2) 自動派車
           var result = await _dispatcher.ApproveAndAssignVehicleAsync(dispatch.DispatchId, passengerCount, vehicleId);
            if (!result.Success)
                return BadRequest(new { message = result.Message });

            // 3) 更新申請單狀態為「審核完成」
            var app = await _db.CarApplications.FirstOrDefaultAsync(a => a.ApplyId == applyId);
            if (app != null)
            {
                app.Status = "完成審核";
                app.VehicleId = vehicleId ?? result.VehicleId;

                var (ok, err) = await _db.TrySaveChangesAsync(this);
                if (!ok) return err!;
            }

            return Ok(new
            {
                message = "已完成審核（請指定駕駛）",
                vehicle = app?.VehicleId,
                status = app?.Status,
            });
        }

        #endregion




        #endregion

        #region 異動紀錄
       

        private async Task LogAppAuditAsync(
    int applyId, string action, string byUser, object oldValue, object newValue)
        {
            string ToReadable(object o)
            {
                if (o == null) return "";
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(
                        JsonSerializer.Serialize(o)
                    );

                    // 轉成多行字串
                    var sb = new StringBuilder();
                    foreach (var kv in dict)
                    {
                        var val = string.IsNullOrEmpty(kv.Value?.ToString()) ? "(空白)" : kv.Value;
                        sb.AppendLine($"{kv.Key}：{val}");
                    }
                    return sb.ToString().Trim();
                }
                catch
                {
                    return o.ToString();
                }
            }

            var audit = new CarApplicationAudit
            {
                ApplyId = applyId,
                Action = action,
                ByUserName = byUser,
                OldValue = oldValue != null ? ToReadable(oldValue) : null,
                NewValue = newValue != null ? ToReadable(newValue) : null,
                At = DateTime.Now
            };

            _db.CarApplicationAudits.Add(audit);
            await _db.SaveChangesAsync();
        }




        // 端點：取得申請單異動紀錄
        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetApplicationHistory(int id)
        {
            // 預先載入駕駛/車輛對照表，之後把 JSON 裡的 Id 轉成名稱
            var drivers = await _db.Drivers.AsNoTracking().ToDictionaryAsync(d => d.DriverId, d => d.DriverName);
            var vehicles = await _db.Vehicles.AsNoTracking().ToDictionaryAsync(v => v.VehicleId, v => v.PlateNo);

            var rows = await _db.CarApplicationAudits
                .AsNoTracking()
                .Where(x => x.ApplyId == id)
                .OrderByDescending(x => x.At)
                .ToListAsync();

            var result = rows.Select(r => new {
                at = r.At,                                      // 前端用 toLocaleString 顯示
                action = r.Action switch
                {
                    "Create" => "建立申請",
                    "Update" => "更新內容",
                    "Status" => "狀態變更",
                    "Delete" => "刪除申請",
                    _ => r.Action
                },
                byUserName = r.ByUserName,
                oldValue = Humanize(r.OldValue, drivers, vehicles),
                newValue = Humanize(r.NewValue, drivers, vehicles)
            });

            return Ok(result);

            // 將 JSON 內的 DriverId/VehicleId/Status 等欄位轉成好讀中文
            static object? Humanize(string? json, IDictionary<int, string> dmap, IDictionary<int, string> vmap)
            {
                if (string.IsNullOrWhiteSpace(json)) return null;
                try
                {
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(json!)!;
                    var obj = new Dictionary<string, object?>();

                    foreach (var (k, val) in dict)
                    {
                        switch (k)
                        {
                            case "DriverId":
                                obj["駕駛"] = TryInt(val, out var did) && dmap.TryGetValue(did, out var dname) ? dname : val;
                                break;
                            case "VehicleId":
                                obj["車牌"] = TryInt(val, out var vid) && vmap.TryGetValue(vid, out var plate) ? plate : val;
                                break;
                            case "Status":
                                obj["狀態"] = ToStatusText(val?.ToString());
                                break;
                            case "PassengerCount":
                                obj["乘客數"] = val;
                                break;
                            case "UseStart":
                            case "UseEnd":
                                obj[k == "UseStart" ? "起始時間" : "結束時間"] = val;
                                break;
                            default:
                                obj[k] = val;
                                break;
                        }
                    }
                    return obj;
                }
                catch { return json; }

                static bool TryInt(object? x, out int v)
                {
                    if (x == null) { v = 0; return false; }
                    return int.TryParse(x.ToString(), out v);
                }
                static string ToStatusText(string? s)
                {
                    return s switch
                    {
                        "待審核" => "待審核",
                        "完成審核" => "完成審核",
                        "退回" => "退回",
                        _ => s ?? ""
                    };
                }
            }
        }
        #endregion

        [Obsolete]
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

        #region LINE專用申請單
        //LINE專用新增申請單
        [AllowAnonymous]
        [HttpPost("line-create")]
        public async Task<IActionResult> LineCreate([FromQuery] string lineUserId, [FromBody] CarApplicationDto dto)
        {
            if (string.IsNullOrWhiteSpace(lineUserId))
                return BadRequest(new { success = false, message = "缺少 lineUserId" });

            var (ok, msg, app) = await _carApplicationService.CreateForLineAsync(dto, lineUserId);

            if (!ok)
                return BadRequest(new { success = false, message = msg });

            return Ok(new
            {
                success = true,
                message = msg,
                data = ToResponseData(app)
            });
        }





        #endregion
        //共用申請單回傳格式
        private object ToResponseData(CarApplication app)
        {
            return new
            {
                app.ApplyId,
                app.Origin,
                app.Destination,
                app.ApplyFor,
                app.MaterialName,
                app.UseStart,
                app.UseEnd,
                app.TripType,
                app.PassengerCount,
                app.RoundTripDistance,
                app.SingleDistance,
                app.RoundTripDuration,
                app.SingleDuration,
                app.Status
            };
        }



    }


}

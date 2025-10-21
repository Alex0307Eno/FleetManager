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
using Cars.Shared.Line;
using LineBotService.Core.Services;
using Cars.Application.Services.Line;
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
        private readonly NotificationService _notificationService;
        




        public CarApplicationsController(ApplicationDbContext db,  IDistanceService distance, CarApplicationService carApplicationService, VehicleService vehicleService, AutoDispatcher dispatcher, CarApplicationUseCase usecase, NotificationService notificationService)
        {
            _db = db;
            _distance = distance;
            _carApplicationService = carApplicationService;
            _vehicleService = vehicleService;
            _dispatcher = dispatcher;
            _usecase = usecase;
            _notificationService = notificationService;
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
            {
                Console.WriteLine("⚠️ 未登入，無法建立申請。");
                return Unauthorized("尚未登入");
            }

            Console.WriteLine($"🟢 開始建立派車申請：UserId={userId}, 申請時間={dto.UseStart:MM/dd HH:mm}-{dto.UseEnd:HH:mm}");

            var (ok, msg, app) = await _carApplicationService.CreateAsync(dto, userId);
            if (!ok)
            {
                Console.WriteLine($"❌ 建立申請失敗：{msg}");
                return BadRequest(new { success = false, message = msg });
            }

            var notifyDto = new CarApplicationDto
            {
                ApplyId = app.ApplyId,
                ApplicantName = app.Applicant?.Name,
                ApplicantDept = app.Applicant?.Dept,
                UseStart = app.UseStart,
                UseEnd = app.UseEnd,
                Origin = app.Origin,
                Destination = app.Destination,
                PassengerCount = app.PassengerCount,
                TripType = app.TripType,
                ApplyReason = app.ApplyReason
            };

            Console.WriteLine($"✅ 申請建立成功：ApplyId={app.ApplyId}, Applicant={notifyDto.ApplicantName}");

            var adminIds = await _db.Users
                .Where(u => (u.Role == "Admin" || u.Role == "Manager") && !string.IsNullOrEmpty(u.LineUserId))
                .Select(u => u.LineUserId)
                .ToListAsync();

            Console.WriteLine($"👀 找到 {adminIds.Count} 位管理員準備通知。");

            var flexJson = ManagerTemplate.BuildManagerReviewBubble(notifyDto);
            Console.WriteLine($"🧱 Flex JSON 組成完成：{flexJson.Substring(0, Math.Min(flexJson.Length, 200))}...");

            foreach (var lineId in adminIds)
            {
                Console.WriteLine($"📤 推送通知給 {lineId}...");
                await _notificationService.PushAsync(lineId, flexJson);
            }

            Console.WriteLine("🎉 所有通知已發送完畢。");

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
            _db.CarApplicationAudits.Add(new CarApplicationAudit
            {
                ApplyId = app.ApplyId,
                Action = "Delete",
                OldValue = oldJson,
                NewValue = null,
                ByUserName = User.Identity?.Name ?? "系統",
                At = DateTime.Now
            });

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
            var (ok, msg, result) = await _carApplicationService.UpdateStatusAsync(id, dto.Status, User.Identity?.Name ?? "系統");
            if (!ok) return BadRequest(ApiResponse.Fail<object>(msg));
            return Ok(ApiResponse.Ok(result, msg));
        }

        #endregion

        // 取得受影響的派工清單
        [HttpGet("Affected")]
        public async Task<IActionResult> GetAffectedList()
        {
            var list = await _db.AffectedDispatches
                .Include(x => x.Dispatch).ThenInclude(d => d.CarApplication)
                .Include(x => x.Dispatch).ThenInclude(d => d.Driver)
                .Where(x => !x.IsResolved)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    x.AffectedId,
                    x.DispatchId,
                    DriverName = x.Dispatch.Driver.DriverName,
                    Origin = x.Dispatch.CarApplication.Origin,
                    Destination = x.Dispatch.CarApplication.Destination,
                    Start = x.Dispatch.CarApplication.UseStart,
                    End = x.Dispatch.CarApplication.UseEnd,
                    x.Dispatch.DispatchStatus
                })
                .ToListAsync();

            return Ok(list);
        }

        




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
                at = r.At,                                      
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


        #region LINE專用申請單(測試用)
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

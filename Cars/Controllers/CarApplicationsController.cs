using Cars.Data;
using Cars.Features.CarApplications;
using Cars.Features.Vehicles;
using Cars.Models;
using Cars.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Cars.ApiControllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    
    public class CarApplicationsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly AutoDispatcher _dispatcher;
        private readonly IDistanceService _distance;
        private readonly CarApplicationService _carApplicationService;
        private readonly VehicleService _vehicleService;


        public CarApplicationsController(ApplicationDbContext db, AutoDispatcher dispatcher, IDistanceService distance, CarApplicationService carApplicationService, VehicleService vehicleService)
        {
            _db = db;
            _dispatcher = dispatcher;
            _distance = distance;
            _carApplicationService = carApplicationService;
            _vehicleService = vehicleService;
        }

        #region 建立申請單
        // 建立申請單（含搭乘人員清單）



        [HttpPost]
        [Authorize(Roles = "Admin,Applicant,Manager")]
        public async Task<IActionResult> Create([FromBody] CarApplyDto dto)
        {
            if (dto == null || dto.Application == null)
                return BadRequest("申請資料不得為空");

            var model = dto.Application;

            // 1) 取得登入者
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(uid, out var userId))
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
            var maxCap = await _vehicleService.GetMaxAvailableCapacityAsync(model.UseStart, model.UseEnd);
            if (maxCap == 0)
            {
                return BadRequest("目前時段沒有任何可用車輛");
            }
            if (model.PassengerCount > maxCap)
            {
                return BadRequest($"申請乘客數 {model.PassengerCount} 超過可用車輛最大載客量 {maxCap}");
            }
            using var tx = await _db.Database.BeginTransactionAsync();

            // === 7) 先存「申請單」
            _db.CarApplications.Add(model);
            var (ok,err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;

            // 8) 乘客寫入
            if (dto.Passengers?.Any() == true)
            {
                dto.Passengers.ForEach(p => p.ApplyId = model.ApplyId);
                _db.CarPassengers.AddRange(dto.Passengers);
            }

           

            // 10) 建立派工（待指派）
            var dispatch = new Cars.Models.Dispatch
            {
                ApplyId = model.ApplyId,
                DriverId = null,
                VehicleId = null,
                CreatedAt = DateTime.Now,
                DispatchStatus = "待指派"
            };

            _db.Dispatches.Add(dispatch);
            var (ok2, err2) = await _db.TrySaveChangesAsync(this);
            if (!ok2)
            {
                await tx.RollbackAsync();
                return err2!;
            }
            await tx.CommitAsync();

            return Ok(ApiResponse<CarApplicationResultDto>.Ok(
             new CarApplicationResultDto(model.ApplyId, model.IsLongTrip),
             "申請完成，已建立派車單 (待指派)"
            ));


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

            if (app == null)
                return NotFound(ApiResponse.Fail<object>("找不到申請單"));

            var passengers = await _db.CarPassengers
                .Where(p => p.ApplyId == id)
                .Select(p => new CarPassengerDto
                {
                    PassengerId = p.PassengerId,
                    ApplyId = p.ApplyId,
                    Name = p.Name,
                    DeptTitle = p.DeptTitle,
                })
                .ToListAsync();

            var applicant = await _db.Applicants
                .AsNoTracking()
                .FirstOrDefaultAsync(ap => ap.ApplicantId == app.ApplicantId);

            var result = new CarApplicationDetailDto
            {
                ApplyId = app.ApplyId,
                UseStart = app.UseStart,
                UseEnd = app.UseEnd,
                Origin = app.Origin,
                Destination = app.Destination,
                PassengerCount = app.PassengerCount,
                TripType = app.TripType,
                SingleDistance = app.SingleDistance,
                RoundTripDistance = app.RoundTripDistance,
                Status = app.Status,
                ReasonType = app.ReasonType,
                ApplyReason = app.ApplyReason,
                MaterialName = app.MaterialName,

                DriverId = app.DriverId,
                DriverName = app.Driver?.DriverName,
                VehicleId = app.VehicleId,
                PlateNo = app.Vehicle?.PlateNo,
                Capacity = app.Vehicle?.Capacity,

                Applicant = applicant == null ? null : new ApplicantDto
                {
                    ApplicantId = applicant.ApplicantId,
                    Name = applicant.Name,
                    Dept = applicant.Dept,
                    Email = applicant.Email,
                    Ext = applicant.Ext,
                    Birth = applicant.Birth
                },

                Passengers = passengers
            };

            return Ok(ApiResponse.Ok(result, "查詢成功"));
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
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;

            return Ok(ApiResponse.Ok<object>(null, "刪除成功"));
        }
        #endregion


        #region 更新審核狀態

        [AllowAnonymous]
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Status))
                return BadRequest(ApiResponse.Fail<object>("Status 不可為空"));

            var app = await _db.CarApplications.FirstOrDefaultAsync(x => x.ApplyId == id);
            if (app == null)
                return NotFound(ApiResponse.Fail<object>("找不到申請單"));

            var newStatus = dto.Status.Trim();

            if (newStatus == "完成審核")
            {
                // 找最新派工單
                var dispatch = await _db.Dispatches
                    .Where(d => d.ApplyId == app.ApplyId)
                    .OrderByDescending(d => d.DispatchId)
                    .FirstOrDefaultAsync();

                // 如果有派工且已有駕駛，帶入駕駛/車輛
                if (dispatch != null && dispatch.DriverId != null)
                {
                    app.DriverId = dispatch.DriverId;
                    app.VehicleId = dispatch.VehicleId;
                }

                app.Status = "完成審核";

                var (ok, err) = await _db.TrySaveChangesAsync(this);
                if (!ok) return err!;

                return Ok(ApiResponse.Ok(new
                {
                    status = app.Status,
                    driverId = app.DriverId,
                    vehicleId = app.VehicleId
                }, "已完成審核" + (dispatch == null ? "（尚未派駕駛）" : "")));
            }

            // 其他狀態
            app.Status = newStatus;
            var (ok2, err2) = await _db.TrySaveChangesAsync(this);
            if (!ok2) return err2!;

            return Ok(ApiResponse.Ok(new { status = app.Status }, "狀態已更新"));
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
        [Obsolete]
        #region 審核後自動派車 (暫不使用)
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
                var (ok, err) = await _db.TrySaveChangesAsync(this);
                if (!ok) return err!;
            }

            return Ok(new
            {
                message = "已完成審核（自動派工已停用，請手動指派駕駛與車輛）",
                status = app?.Status
            });
        }

        #endregion




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
                UseStart = input.UseStart != default ? input.UseStart : DateTime.UtcNow,
                UseEnd = input.UseEnd != default ? input.UseEnd : DateTime.UtcNow.AddMinutes(30),
                TripType = input.TripType ?? "single",
                ApplicantId = applicant.ApplicantId,
                Status = "待審核",
                SingleDistance = input.SingleDistance,
                SingleDuration = input.SingleDuration,
                RoundTripDistance = input.RoundTripDistance,
                RoundTripDuration = input.RoundTripDuration,
            };

            _db.CarApplications.Add(app);
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;

            return Ok(new { message = "新增成功", app.ApplyId });
        }

        // 建立派車單
        [AllowAnonymous]
        [HttpPost("{applyId}/dispatch")]

        public async Task<IActionResult> CreateDispatch(int applyId)
        {
            var app = await _db.CarApplications.FindAsync(applyId);
            if (app == null)
                return NotFound(new { success = false, message = "找不到申請單" });

            if (app.UseStart == default || app.UseEnd == default)
                return BadRequest(new { success = false, message = "申請單時間無效，無法建立派工" });

            // 檢查是否已經有派車單
            var exists = await _db.Dispatches.AnyAsync(d => d.ApplyId == applyId);
            if (exists)
                return Conflict(new { success = false, message = "已經有派車單存在" });

            var dispatch = new Cars.Models.Dispatch
            {
                ApplyId = applyId,
                DriverId = null,
                VehicleId = null,
                DispatchStatus = "待指派",
                CreatedAt = DateTime.Now
            };

            _db.Dispatches.Add(dispatch);
            var (ok, err) = await _db.TrySaveChangesAsync(this);
            if (!ok) return err!;
            var dto = new DispatchDto(
                dispatch.DispatchId,
                dispatch.ApplyId,
                dispatch.DispatchStatus
            );

            return Ok(ApiResponse<DispatchDto>.Ok(dto, "派車單建立成功"));

        }
        #endregion




    }


}

using Microsoft.AspNetCore.Mvc;
using Cars.Data;
using Cars.Models;

namespace Cars.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CarApplicationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CarApplicationsController(ApplicationDbContext context)
        {
            _context = context;
        }


        // POST: api/CarApplications
        [HttpPost]
        public IActionResult Create([FromBody] CarApply model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _context.CarApplications.Add(model);
            _context.SaveChanges();

            return Ok(new { message = "申請成功", id = model.ApplyId });
        }

        // GET: api/CarApplications
        [HttpGet]
        public IActionResult GetAll()
        {
            return Ok(_context.CarApplications.ToList());
        }

        // GET: api/CarApplications/{id}
        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            var app = _context.CarApplications.Find(id);
            if (app == null) return NotFound();
            return Ok(app);
        }

        // PUT: api/CarApplications/{id}
        [HttpPut("{id}")]
        public IActionResult Update(int id, [FromBody] CarApply model)
        {
            var app = _context.CarApplications.Find(id);
            if (app == null) return NotFound();

            // 更新欄位
            // 更新申請人資訊
            app.ApplicantName = model.ApplicantName;
            app.ApplicantEmpId = model.ApplicantEmpId;
            app.ApplicantDept = model.ApplicantDept;
            app.ApplicantExt = model.ApplicantExt;
            app.ApplicantEmail = model.ApplicantEmail;

            // 更新用車需求
            app.ApplyFor = model.ApplyFor;
            app.VehicleType = model.VehicleType;
            app.PurposeType = model.PurposeType;
            app.PassengerCount = model.PassengerCount;
            app.UseStart = model.UseStart;
            app.UseEnd = model.UseEnd;
            app.ReasonType = model.ReasonType;
            app.ApplyReason = model.ApplyReason;
            app.Origin = model.Origin;
            app.Destination = model.Destination;
            app.TripType = model.TripType;

            // 更新計算結果
            app.SingleDistance = model.SingleDistance;
            app.SingleDuration = model.SingleDuration;
            app.RoundTripDistance = model.RoundTripDistance;
            app.RoundTripDuration = model.RoundTripDuration;

            _context.SaveChanges();

            return Ok(new { message = "更新成功" });
        }

        // DELETE: api/CarApplications/{id}
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var app = _context.CarApplications.Find(id);
            if (app == null) return NotFound();

            _context.CarApplications.Remove(app);
            _context.SaveChanges();

            return Ok(new { message = "刪除成功" });
        }
    }
}

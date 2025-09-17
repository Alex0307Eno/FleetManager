using System.Collections.Generic;
using System.Linq;
using Cars.Data;
using Cars.Models;

namespace LineBotDemo.Services
{
    public class LineUserService
    {
        private readonly ApplicationDbContext _db;

        public LineUserService(ApplicationDbContext db)
        {
            _db = db;
        }

        // 取得所有管理員
        public List<LineUser> GetAdmins()
        {
            return _db.LineUsers.Where(u => u.Role == "Admin").ToList();
        }

        // 取得所有駕駛
        public List<LineUser> GetDrivers()
        {
            return _db.LineUsers.Where(u => u.Role == "Driver").ToList();
        }

        // 取得所有申請人
        public List<LineUser> GetApplicants()
        {
            return _db.LineUsers.Where(u => u.Role == "Applicant").ToList();
        }

        // 依照 RelatedId 找駕駛
        public LineUser GetDriverById(int driverId)
        {
            return _db.LineUsers.FirstOrDefault(u => u.Role == "Driver" && u.RelatedId == driverId);
        }

        // 依照 RelatedId 找申請人
        public LineUser GetApplicantById(int applicantId)
        {
            return _db.LineUsers.FirstOrDefault(u => u.Role == "Applicant" && u.RelatedId == applicantId);
        }
    }
}

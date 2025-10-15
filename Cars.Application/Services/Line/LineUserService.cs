using Cars.Data;

namespace Cars.Application.Services.Line
{
    public class LineUserService
    {
        private readonly ApplicationDbContext _db;
        public LineUserService(ApplicationDbContext db) { _db = db; }

        // 取得某角色所有人的 LineUserId（權限以 Users.Role 為準）
        public List<string> GetLineUserIdsByRole(string role)
        {
            return _db.Users
                      .Where(u => u.Role == role && u.LineUserId != null && u.LineUserId != "")
                      .Select(u => u.LineUserId)
                      .ToList();
        }

        // 需要顯示名稱時（JOIN 取 LineUsers.DisplayName）
        public List<(string LineUserId, string DisplayName)> GetLineUsersByRole(string role)
        {
            var query = from u in _db.Users
                        join lu in _db.LineUsers on u.LineUserId equals lu.LineUserId
                        where u.Role == role && u.LineUserId != null && u.LineUserId != ""
                        select new { lu.LineUserId, lu.DisplayName };

            var list = query.ToList();
            var result = new List<(string, string)>();
            foreach (var x in list)
                result.Add((x.LineUserId, x.DisplayName));
            return result;
        }

        // （選擇性）如果你有「司機 = Drivers 表」→ 需要能從 DriverId 找到對應的使用者 LineUserId
        // 這段需要你資料表有「Drivers ↔ Users」的關聯（例如 Drivers.UserId）
        public string GetDriverLineUserIdByDriverId(int driverId)
        {
            // 假設 Drivers 有 UserId 欄位
            var lineId = (from d in _db.Drivers
                          join u in _db.Users on d.UserId equals u.UserId
                          where d.DriverId == driverId
                          select u.LineUserId).FirstOrDefault();
            return lineId;
        }
    }
}

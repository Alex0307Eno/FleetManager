using Cars.Data;
using Cars.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Cars.Services
{
    public class CarApplicationService
    {
        private readonly ApplicationDbContext _db;

        public CarApplicationService(ApplicationDbContext db)
        {
            _db = db;
        }

        
        // 更新狀態
        public async Task<bool> UpdateStatusAsync(int appId, string status)
        {
            var app = await _db.CarApplications.FirstOrDefaultAsync(c => c.ApplyId == appId);
            if (app == null) return false;

            app.Status = status;
            await _db.SaveChangesAsync();
            return true;
        }
    }
}

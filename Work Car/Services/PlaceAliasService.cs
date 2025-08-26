using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Cars.Data;
using Cars.Models;

namespace Cars.Services
{
    public class PlaceAliasService
    {
        private readonly ApplicationDbContext _db;

        public PlaceAliasService(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// 雙向轉換：
        /// 1. 如果輸入是「簡稱」，回傳完整地址（Keyword 或 FullAddress）
        /// 2. 如果輸入是「完整地址」，回傳替換過的簡稱
        /// </summary>
        public async Task<string> ResolveAsync(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            // 1) 檢查是否剛好輸入簡稱
            var aliasMatch = await _db.PlaceAliases
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Alias == input);

            if (aliasMatch != null)
            {
                // 如果有 FullAddress 欄位，優先回傳完整地址，否則用 Keyword
                return !string.IsNullOrWhiteSpace(aliasMatch.FullAddress)
                    ? aliasMatch.FullAddress
                    : aliasMatch.Keyword;
            }

            // 2) 檢查是否包含關鍵字（完整地址情境）
            var keywordMatch = await _db.PlaceAliases
                .AsNoTracking()
                .FirstOrDefaultAsync(a => input.Contains(a.Keyword));

            if (keywordMatch != null)
            {
                return input.Replace(keywordMatch.Keyword, keywordMatch.Alias);
            }

            // 3) 沒找到就原樣回傳
            return input;
        }
    }
}

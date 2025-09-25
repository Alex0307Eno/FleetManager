using System.Text.RegularExpressions;

namespace LineBotService.Services
{
    public class InputValidator
    {
        #region 阻擋惡意攻擊

        // ====== 用車事由驗證 ======
        public static bool IsValidReason(string input, out string normalized, out string err)
        {
            normalized = (input ?? "").Trim();
            err = "";

            // 長度：2~30
            if (normalized.Length < 2 || normalized.Length > 30)
            {
                err = "⚠️ 用車事由需為 2–30 字。";
                return false;
            }

            // 禁止網址/腳本片段
            if (Regex.IsMatch(normalized, @"https?://|www\.|<script|</script|javascript:", RegexOptions.IgnoreCase))
            {
                err = "⚠️ 用車事由不得包含網址或腳本字樣。";
                return false;
            }

            // 僅允許：中英數、空白，以及常見標點（含全形）
            if (!Regex.IsMatch(normalized, @"^[\p{L}\p{N}\p{Zs}\-—–_,.:;!?\(\)\[\]{}，。；：「」『』！？、（）【】]+$"))
            {
                err = "⚠️ 僅允許中英數與常用標點符號。";
                return false;
            }

            return true;
        }
        // ====== 地點驗證 ======
        public static bool IsValidLocation(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            if (input.Length < 3 || input.Length > 50) return false;

            // 黑名單關鍵字
            string[] badWords = { "select", "insert", "update", "delete", "drop", "truncate", "exec", "union",
                          "<script", "javascript:", "--", ";--", "/*", "*/" };
            var lower = input.ToLowerInvariant();
            if (badWords.Any(w => lower.Contains(w))) return false;

            // 全數字 / 全英文
            if (Regex.IsMatch(input, @"^\d+$")) return false;
            if (Regex.IsMatch(input, @"^[a-zA-Z]+$")) return false;

            // 必須包含至少一個中文（避免純亂碼）
            if (!Regex.IsMatch(input, @"\p{IsCJKUnifiedIdeographs}")) return false;

            // 允許字元：中英數 + 空白 + 常見標點
            if (!Regex.IsMatch(input, @"^[\p{L}\p{N}\p{Zs},，.。\-]+$"))
                return false;

            return true;
        }


        // 允許的文字（白名單）＋長度限制
        public static bool IsValidUserText(string s, int maxLen = 300)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            if (s.Length > maxLen) return false;

            // 允許：中英數、空白、URL 可能用到的安全分隔（: / . - _），以及常見中/英標點（含全形）
            var pattern = @"^[\p{L}\p{N}\p{Zs}\-—–_.,:;/\(\)\[\]{}，。；：「」『』！？、（）【】@#\+&]+$";
            return Regex.IsMatch(s, pattern);
        }


        // 粗略攔截疑似 SQL 關鍵片段（第二道防線：記錄/阻擋）
        public static bool ContainsSqlMeta(string s)
        {
            var lower = (s ?? "").ToLowerInvariant();

            // 只攔明顯惡意（保留你原本的關鍵字，並避免把一般字誤殺）
            string[] hits = { "--", ";--", "/*", "*/", " xp_", " drop ", " truncate ", " insert ", " delete ", " update ", " exec ", " sp_" };
            foreach (var h in hits)
            {
                if (lower.Contains(h)) return true;
            }
            return false;
        }




        #endregion
    }
}

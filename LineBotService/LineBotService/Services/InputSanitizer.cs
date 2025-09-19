using System.Text.RegularExpressions;
using System.Web;
using System.Linq;

namespace LineBotService.Services
{
   
    public static class InputSanitizer
    {
        // 基本白名單：允許的 unicode 類別（中英數、空白、常見標點）
        private static readonly Regex AllowedTextPattern = new Regex(
            @"^[\p{L}\p{N}\p{Zs}\-—–_.,:;\/\\\(\)\[\]\{\}，。；：！？、％#@+&]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // 明顯惡意的黑名單/關鍵字（SQL & script）——可擴充
        private static readonly string[] DangerousKeywords = new[]
        {
        "select","insert","update","delete","drop","truncate","exec","union","alter","create",
        "<script", "</script", "javascript:", "onerror=", "onload=", "document.cookie", "window.location",
        "--", ";--", "/*", "*/", "xp_", "sp_"
    };

        // URL-like pattern
        private static readonly Regex UrlLike = new Regex(@"https?://|www\.", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // 控制字元
        private static readonly Regex ControlChars = new Regex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", RegexOptions.Compiled);

        // 最外層檢查：長度 + 黑白名單 + 控制字元
        public static bool IsSafeText(string input, int maxLen = 1000)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            if (input.Length > maxLen) return false;

            // 禁止過多連續相同字（避免 flood/base64）
            if (Regex.IsMatch(input, @"(.)\1{40,}")) return false;

            if (ControlChars.IsMatch(input)) return false;
            if (UrlLike.IsMatch(input)) return false; // 若你允許 URL，拿掉這行或做特殊處理

            var lower = input.ToLowerInvariant();
            foreach (var k in DangerousKeywords)
                if (lower.Contains(k)) return false;

            // 簡單白名單檢查（若你需要更自由，這行可以改成更寬鬆）
            if (!AllowedTextPattern.IsMatch(input))
                return false;

            return true;
        }

        // 防止SQL Enjection
        public static bool LooksLikeSqlInjection(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return false;
            var low = s.ToLowerInvariant();
            // 若同時有 SQL 關鍵詞 + 分號 或 comment，則疑似
            if ((low.Contains("select ") || low.Contains("union ") || low.Contains("drop ") || low.Contains("insert ")
                 || low.Contains("update ") || low.Contains("delete ")) &&
                (low.Contains(";") || low.Contains("--") || low.Contains("/*")))
                return true;
            return false;
        }

        // 地點檢查：嚴格模式（強制含中文）或寬鬆模式（允許英文/數字）
        public static bool IsValidLocation(string input, bool requireChinese = true)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var s = input.Trim();
            if (s.Length < 2 || s.Length > 120) return false; // 可調整

            if (ControlChars.IsMatch(s)) return false;

            // 黑名單字詞
            var lower = s.ToLowerInvariant();
            var badWords = new[] { "select", "insert", "update", "delete", "drop", "<script", "javascript:" };
            if (badWords.Any(b => lower.Contains(b))) return false;

            if (requireChinese)
            {
                // 必須包含至少一個中文漢字
                if (!Regex.IsMatch(s, @"\p{IsCJKUnifiedIdeographs}")) return false;
            }
            // 必要時可檢查是否全英文或全數字
            if (Regex.IsMatch(s, @"^\d+$")) return false;
            if (Regex.IsMatch(s, @"^[a-zA-Z]+$") && requireChinese) return false;

            // 結尾不要是奇怪符號
            if (Regex.IsMatch(s, @"[<>]")) return false;

            return true;
        }

        // 當要放到 HTML 或 Flex JSON 時，務必 encode
        public static string HtmlEncode(string input) => HttpUtility.HtmlEncode(input ?? "");
        public static string JsonEscape(string input) => Newtonsoft.Json.JsonConvert.ToString(input ?? "").Trim('"'); // 你的 Safe()
    }

}

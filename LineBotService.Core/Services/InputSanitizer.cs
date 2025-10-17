using System.Text.RegularExpressions;
using System.Linq;

namespace LineBotService.Core.Services
{
    public static class InputSanitizer
    {
        // 白名單（中英數、空白、常見標點）
        private static readonly Regex AllowedTextPattern = new Regex(
            @"^[\p{L}\p{N}\p{Zs}\-—–_.,:;\/\\\(\)\[\]\{\}，。；：！？、％#@+&]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        // 黑名單（SQL / script）
        private static readonly string[] DangerousKeywords = new[]
        {
            "select","insert","update","delete","drop","truncate","exec","union","alter","create",
            "<script", "</script", "javascript:", "onerror=", "onload=", "document.cookie", "window.location",
            "--", ";--", "/*", "*/", "xp_", "sp_"
        };

        // URL、控制字元檢查
        private static readonly Regex UrlLike = new Regex(@"https?://|www\.", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex ControlChars = new Regex(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", RegexOptions.Compiled);

        /// <summary>
        /// 通用輸入安全檢查
        /// </summary>
        public static bool IsSafeText(string input, int maxLen = 1000)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            if (input.Length > maxLen) return false;
            if (Regex.IsMatch(input, @"(.)\1{40,}")) return false;
            if (ControlChars.IsMatch(input)) return false;
            if (UrlLike.IsMatch(input)) return false;

            var lower = input.ToLowerInvariant();
            foreach (var k in DangerousKeywords)
                if (lower.Contains(k)) return false;

            return AllowedTextPattern.IsMatch(input);
        }

        /// <summary>
        /// 驗證地點是否合法
        /// </summary>
        public static bool IsValidLocation(string input, bool requireChinese = true)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var s = input.Trim();
            if (s.Length < 2 || s.Length > 120) return false;
            if (ControlChars.IsMatch(s)) return false;

            var lower = s.ToLowerInvariant();
            var badWords = new[] { "select", "insert", "update", "delete", "drop", "<script", "javascript:" };
            if (badWords.Any(b => lower.Contains(b))) return false;

            if (requireChinese && !Regex.IsMatch(s, @"\p{IsCJKUnifiedIdeographs}")) return false;
            if (Regex.IsMatch(s, @"^\d+$")) return false;
            if (Regex.IsMatch(s, @"^[a-zA-Z]+$") && requireChinese) return false;
            if (Regex.IsMatch(s, @"[<>]")) return false;

            return true;
        }

        /// <summary>
        /// 清除惡意內容與多餘符號
        /// </summary>
        public static string CleanText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            string text = input;

            // 移除控制字元
            text = new string(text.Where(c => !char.IsControl(c)).ToArray());

            // 移除 HTML 標籤
            text = Regex.Replace(text, "<.*?>", string.Empty);

            // 移除 SQL 關鍵字
            string[] sqlWords = { "select", "insert", "update", "delete", "drop", "exec", "union", "truncate" };
            foreach (var w in sqlWords)
                text = Regex.Replace(text, $"\\b{w}\\b", "", RegexOptions.IgnoreCase);

            // 清空多餘空白
            text = text.Replace("\r", " ").Replace("\n", " ");
            while (text.Contains("  "))
                text = text.Replace("  ", " ");

            return text.Trim();
        }
    }
}

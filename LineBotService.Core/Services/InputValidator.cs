using System.Text.RegularExpressions;
using System.Linq;

namespace LineBotService.Core.Services
{
    public static class InputValidator
    {
        /// <summary>
        /// 驗證「用車事由」
        /// </summary>
        public static bool IsValidReason(string input, out string normalized, out string err)
        {
            normalized = (input ?? "").Trim();
            err = "";

            if (normalized.Length < 2 || normalized.Length > 30)
            {
                err = "⚠️ 用車事由需為 2–30 字。";
                return false;
            }

            // 禁止網址與腳本
            if (Regex.IsMatch(normalized, @"https?://|www\.|<script|javascript:", RegexOptions.IgnoreCase))
            {
                err = "⚠️ 用車事由不得包含網址或腳本字樣。";
                return false;
            }

            // 允許：中英數、空白、常見標點
            if (!Regex.IsMatch(normalized, @"^[\p{L}\p{N}\p{Zs}\-—–_,.:;!?\(\)\[\]{}，。；：「」『』！？、（）【】]+$"))
            {
                err = "⚠️ 僅允許中英數與常用標點符號。";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 驗證地點（出發地 / 目的地）
        /// </summary>
        public static bool IsValidLocation(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            var s = input.Trim();
            if (s.Length < 3 || s.Length > 50) return false;

            var lower = s.ToLowerInvariant();
            string[] badWords = { "select", "insert", "update", "delete", "drop", "truncate", "<script", "javascript:" };
            if (badWords.Any(b => lower.Contains(b))) return false;

            if (Regex.IsMatch(s, @"^\d+$")) return false;  // 全數字
            if (Regex.IsMatch(s, @"^[a-zA-Z]+$")) return false;  // 全英文
            if (!Regex.IsMatch(s, @"\p{IsCJKUnifiedIdeographs}")) return false;  // 至少一個中文

            // 合法字元：中英數 + 空白 + 標點
            return Regex.IsMatch(s, @"^[\p{L}\p{N}\p{Zs},，.。\-]+$");
        }
        /// <summary>
        /// 驗證「載運物品名稱」
        /// </summary>
        public static bool IsValidMaterial(string input, out string normalized, out string err)
        {
            normalized = (input ?? "").Trim();
            err = "";

            if (normalized.Length < 1 || normalized.Length > 40)
            {
                err = "⚠️ 載運物品名稱需為 1–40 字。";
                return false;
            }

            // 禁止危險關鍵字與 HTML 片段
            if (Regex.IsMatch(normalized, @"https?://|www\.|<script|javascript:", RegexOptions.IgnoreCase))
            {
                err = "⚠️ 載運物品名稱不得包含網址或腳本字樣。";
                return false;
            }

            // 白名單字元：中英數、空白、常用標點（含括號與破折號）
            if (!Regex.IsMatch(normalized, @"^[\p{L}\p{N}\p{Zs}\-—–_.,:;!?、／\(\)\[\]{}，。；：「」『』！？（）【】]+$"))
            {
                err = "⚠️ 僅允許中英數與常用標點符號。";
                return false;
            }

            return true;
        }
    }
}

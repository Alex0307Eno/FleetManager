using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Text;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System;
using System.Collections.Generic;

namespace LineBotService.Middleware
{
    public class SecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _channelAccessToken;
        private static readonly HttpClient _http = new HttpClient();

        // SQL/script 關鍵字，使用 word-boundary 避免誤判
        private static readonly string[] KeywordPatterns = new[]
        {
            @"\bselect\b", @"\binsert\b", @"\bupdate\b", @"\bdelete\b",
            @"\bdrop\b", @"\btruncate\b", @"\bexec\b", @"\bunion\b",
            @"<script", @"javascript:", "onerror=", "onload="
        };

        // 測試或例外白名單（開發時可加入 TEST_MARKER 或特定 userId）
        private static readonly string[] BodyWhiteListPhrases = new[] { "抵達時間", "出發時間", "TEST_MARKER" };

        public SecurityMiddleware(RequestDelegate next, IConfiguration cfg)
        {
            _next = next;
            _channelAccessToken = cfg["LineBot:ChannelAccessToken"] ?? "";
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                context.Request.EnableBuffering();

                if (context.Request.ContentLength > 0 &&
                    context.Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
                {
                    using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
                    var body = await reader.ReadToEndAsync();
                    context.Request.Body.Position = 0;

                    // 嘗試解析 JSON 並只檢查 events[].message.text / postback.data
                    var suspiciousItems = new List<(string text, string eventType)>();
                    try
                    {
                        using var doc = JsonDocument.Parse(body);
                        if (doc.RootElement.TryGetProperty("events", out var events))
                        {
                            foreach (var ev in events.EnumerateArray())
                            {
                                // 取出 message.text
                                if (ev.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.Object)
                                {
                                    if (msgEl.TryGetProperty("text", out var txtEl) && txtEl.ValueKind == JsonValueKind.String)
                                    {
                                        suspiciousItems.Add((txtEl.GetString() ?? "", "message.text"));
                                    }
                                }

                                // 取出 postback data（如果有）
                                if (ev.TryGetProperty("postback", out var pbEl) && pbEl.ValueKind == JsonValueKind.Object)
                                {
                                    if (pbEl.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.String)
                                    {
                                        suspiciousItems.Add((dataEl.GetString() ?? "", "postback.data"));
                                    }
                                }
                            }
                        }
                        else
                        {
                            // 無 events，保守處理：不攔整體 body（避免把非 user 輸入誤判）
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SecurityMiddleware] JSON parse failed: {ex.Message}");
                        // 若 JSON 解析失敗，不自動攔截（避免誤殺）；交給下一層處理
                        await _next(context);
                        return;
                    }

                    // 檢查每個被擷取出的使用者文字
                    foreach (var (text, eventType) in suspiciousItems)
                    {
                        var cleaned = (text ?? "").Trim();

                        // 白名單短語直接跳過（例如 "抵達時間" 之類的正常前綴）
                        if (BodyWhiteListPhrases.Any(p => cleaned.Contains(p, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        // 若含控制字元或超長 base64-like 字串也視為可疑
                        if (Regex.IsMatch(cleaned, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]") ||
                            Regex.IsMatch(cleaned, @"[A-Za-z0-9+/]{80,}={0,2}"))
                        {
                            await HandleSuspiciousAsync(cleaned, body);
                            return; // 攔截並回覆
                        }

                        // 用 pattern 做 whole-word 檢查
                        foreach (var pat in KeywordPatterns)
                        {
                            if (Regex.IsMatch(cleaned, pat, RegexOptions.IgnoreCase))
                            {
                                await HandleSuspiciousAsync(cleaned, body);
                                return; // 攔截並回覆
                            }
                        }
                    }
                }

                await _next(context);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecurityMiddleware ERROR] {ex.Message}");
                await _next(context);
            }
        }

        // 當偵測到可疑輸入時的處理：用 replyToken 回 LINE（若能解析）
        private async Task HandleSuspiciousAsync(string detectedText, string fullBody)
        {
            try
            {
                // 簡潔記錄（避免存 raw payload）
                var excerpt = detectedText.Length <= 300 ? detectedText : detectedText.Substring(0, 300);
                excerpt = Regex.Replace(excerpt, @"[\r\n\t]+", " ");
                Console.WriteLine($"[SUSPICIOUS INPUT] {DateTime.UtcNow:o} => {excerpt}");

                // 解析 replyToken 並回覆使用者警告（若有）
                try
                {
                    using var doc = JsonDocument.Parse(fullBody);
                    if (doc.RootElement.TryGetProperty("events", out var events))
                    {
                        foreach (var ev in events.EnumerateArray())
                        {
                            if (ev.TryGetProperty("replyToken", out var rtEl) && rtEl.ValueKind == JsonValueKind.String)
                            {
                                var replyToken = rtEl.GetString();
                                if (!string.IsNullOrWhiteSpace(replyToken))
                                {
                                    await ReplyWarningToLineAsync(replyToken);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SecurityMiddleware] parse replyToken failed: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecurityMiddleware.HandleSuspiciousAsync] {ex.Message}");
            }
        }

        private async Task ReplyWarningToLineAsync(string replyToken)
        {
            if (string.IsNullOrEmpty(_channelAccessToken)) return;

            var payload = new
            {
                replyToken = replyToken,
                messages = new[]
                {
                    new { type = "text", text = "⚠️ 偵測到輸入包含可疑內容（例如程式碼或指令），請改用自然語句或選單操作輸入。" }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.line.me/v2/bot/message/reply");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _channelAccessToken);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var resp = await _http.SendAsync(req);
                var respBody = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"[SecurityMiddleware] Reply API status {resp.StatusCode} body: {respBody}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SecurityMiddleware] Reply failed: {ex.Message}");
            }
        }
    }
}

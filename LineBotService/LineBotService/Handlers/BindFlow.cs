using Cars.Data;
using Cars.Shared.Dtos.Line;
using Cars.Shared.Line;
using isRock.LineBot;
using LineBotService.Helpers;
using Microsoft.EntityFrameworkCore;

namespace LineBotService.Handlers
{
    public static class BindFlow
    {
        /// <summary>
        /// 嘗試處理綁定帳號與解除綁定的文字訊息
        /// </summary>
        public static async Task<bool> TryHandleAsync(Bot bot, ApplicationDbContext db, string replyToken, string userId, string msg)
        {
            // Step 999：解除綁定確認
            if (msg.Contains("解除綁定"))
            {
                LineBotUtils.Conversations[userId] = new BookingStateDto { Step = 999 };
                bot.ReplyMessage(replyToken, "⚠️ 您確定要解除帳號綁定嗎？\n回覆「確認解除」以繼續，或「取消」放棄操作。");
                return true;
            }

            if (LineBotUtils.Conversations.ContainsKey(userId))
            {
                var state = LineBotUtils.Conversations[userId];
                // 處理解除綁定流程
                if (state.Step == 999)
                {
                    if (msg.Contains("確認解除"))
                    {
                        var user = await db.Users.FirstOrDefaultAsync(u => u.LineUserId == userId);
                        if (user != null)
                        {
                            user.LineUserId = null;
                            await db.SaveChangesAsync();
                            bot.ReplyMessage(replyToken, "✅ 您的帳號已解除綁定。");
                        }
                        else bot.ReplyMessage(replyToken, "⚠️ 找不到綁定資料。");

                        LineBotUtils.Conversations.Remove(userId);
                        return true;
                    }

                    if (msg.Contains("取消"))
                    {
                        bot.ReplyMessage(replyToken, "❎ 已取消解除綁定操作。");
                        LineBotUtils.Conversations.Remove(userId);
                        return true;
                    }
                }
            }

            // Step 900：啟動綁定
            if (msg.Contains("綁定帳號"))
            {
                LineBotUtils.Conversations[userId] = new BookingStateDto { Step = 900 };
                bot.ReplyMessage(replyToken, "請輸入您的帳號：");
                return true;
            }

            // Step 901：帳密驗證
            if (LineBotUtils.Conversations.ContainsKey(userId))
            {
                var state = LineBotUtils.Conversations[userId];

                if (state.Step == 900)
                {
                    state.BindAccount = msg.Trim();
                    state.Step = 901;
                    bot.ReplyMessage(replyToken, "請輸入您的密碼：");
                    return true;
                }

                if (state.Step == 901)
                {
                    var account = state.BindAccount;
                    var password = msg.Trim();

                    var user = await db.Users.FirstOrDefaultAsync(u => u.Account == account);
                    if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                    {
                        user.LineUserId = userId;
                        await db.SaveChangesAsync();
                        bot.ReplyMessage(replyToken, $"✅ 帳號綁定成功！{user.DisplayName} 您好～");

                        LineBotUtils.Conversations[userId] = new BookingStateDto { Step = 1 };
                        LineBotUtils.SafeReply(bot, replyToken, ApplicantTemplate.BuildStep1());
                    }
                    else
                    {
                        bot.ReplyMessage(replyToken, "❌ 帳號或密碼錯誤，請輸入「綁定帳號」重新開始。");
                        LineBotUtils.Conversations.Remove(userId);
                    }

                    return true;
                }
            }

            return false;
        }
    }
}

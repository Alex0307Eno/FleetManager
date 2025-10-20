using Cars.Models;
using Hangfire;

namespace Cars.Services.Hangfire
{
    public static class DispatchJobScheduler
    {
        public static void ScheduleRideReminders(Models.Dispatch dispatch)
        {

            // 前一天提醒
            BackgroundJob.Schedule<LineBotNotificationService>(
                s => s.SendRideReminderAsync(dispatch.DispatchId, "D1"),
                dispatch.CarApplication.UseStart.AddDays(-1));

            // 前15分鐘提醒
            BackgroundJob.Schedule<LineBotNotificationService>(
                s => s.SendRideReminderAsync(dispatch.DispatchId, "M15"),
                dispatch.CarApplication.UseStart.AddMinutes(-15));


        }
        public static void RegisterJobs()
        {
            
            // 新增未完成派車提醒（每天 17:30）
            RecurringJob.AddOrUpdate<LineBotNotificationService>(
                "pending-dispatch-reminder",
                s => s.SendPendingDispatchReminderAsync(),
                "30 17 * * *" // 每天下午5:30
            );
        }
    }
}

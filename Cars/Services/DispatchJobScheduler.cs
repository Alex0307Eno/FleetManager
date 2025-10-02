using Cars.Models;
using Cars.Services;
using Hangfire;

namespace Cars.Services
{
    public static class DispatchJobScheduler
    {
        public static void ScheduleRideReminders(Cars.Models.Dispatch dispatch)
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
    }
}

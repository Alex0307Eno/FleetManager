using Hangfire.Dashboard;
using Microsoft.AspNetCore.Http;

namespace Cars.Web.Security
{
    public sealed class MyHangfireAuthFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize(DashboardContext context)
        {
            var http = context.GetHttpContext();

            // 只允許：已登入 + Admin 角色
            return http.User.Identity?.IsAuthenticated == true
                   && http.User.IsInRole("Admin");
        }
    }
}

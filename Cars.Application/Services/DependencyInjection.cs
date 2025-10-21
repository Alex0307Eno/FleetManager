using Cars.Application.Services.Line;
using Cars.Services.Hangfire;
using LineBotService.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Cars.Application.Services
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddScoped<AutoDispatcher>();
            services.AddScoped<VehicleService>();
            services.AddScoped<DriverService>();
            services.AddScoped<CarApplicationService>();
            services.AddScoped<LineBotNotificationService>();
            services.AddScoped<Odometer>();
            services.AddScoped<CarApplicationUseCase>();
            services.AddScoped<ILinePush, LinePush>();
            services.AddScoped<NotificationService>();
            services.AddScoped<DispatchService>();
            services.AddScoped<LeaveService>();



            return services;
        }
    }
}

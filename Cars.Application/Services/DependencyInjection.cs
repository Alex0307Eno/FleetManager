using Cars.Services.Hangfire;
using Microsoft.Extensions.DependencyInjection;
using LineBotService.Core.Services;
using Cars.Application.Services.Line;

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
            services.AddScoped<DispatchService>();
            services.AddScoped<CarApplicationUseCase>();
            services.AddScoped<ILinePush, LinePush>();
            services.AddScoped<NotificationService>();

            return services;
        }
    }
}

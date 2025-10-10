using Cars.Services;
using Cars.Services.Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace Cars.Application
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
            return services;
        }
    }
}

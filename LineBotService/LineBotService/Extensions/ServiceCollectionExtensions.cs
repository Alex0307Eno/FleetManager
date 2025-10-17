using Cars.Application;
using Cars.Application.Services;
using Cars.Application.Services.Line;
using Cars.Data;
using Cars.Models;
using isRock.LineBot;
using LineBotService.Core.Services;
using LineBotService.Handlers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LineBotService
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddLineBotServices(this IServiceCollection services, IConfiguration config)
        {
            // ===== LineBot 核心 =====
            services.AddSingleton<Bot>(_ => new Bot(config["LineBot:ChannelAccessToken"]));

            // ===== 業務服務 =====
            services.AddSingleton<RichMenuService>();
            services.AddScoped<LineUserService>();
            services.AddApplication(); 

            services.Configure<RichMenuOptions>(config.GetSection("RichMenus"));

            // ===== 處理服務 =====
            services.AddScoped<ILinePush, LinePush>();
            services.AddScoped<NotificationService>();
            services.AddScoped<ApplicantPostbackHandler>();
            services.AddScoped<ManagerReviewHandler>();
            services.AddScoped<PostbackHandler>();
            services.AddHttpClient<IDistanceService, GoogleDistanceService>();




            return services;
        }
    }
}

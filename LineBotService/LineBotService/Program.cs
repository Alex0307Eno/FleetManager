using Cars.Application.Services;
using Cars.Application.Services.Line;
using Cars.Data;
using Cars.Models;
using Hangfire;
using Hangfire.SqlServer;
using isRock.LineBot;
using LineBotService.Core.Services;
using LineBotService.Handlers;
using LineBotService.Middleware;
using Microsoft.EntityFrameworkCore;

namespace LineBotService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // 註冊 Line Bot SDK
            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddSingleton<isRock.LineBot.Bot>(provider =>
            {
                var cfg = provider.GetRequiredService<IConfiguration>();
                return new isRock.LineBot.Bot(cfg["LineBot:ChannelAccessToken"]);
            });

            // 註冊應用程式服務
            builder.Services.AddApplication();
            // 註冊 LineBot 相關服務
            builder.Services.AddLineBotServices(builder.Configuration);

            // 註冊 GoogleMapsSettings
            builder.Services.Configure<GoogleMapsSettings>(
                builder.Configuration.GetSection("GoogleMaps"));

            // 註冊 HttpClient + DistanceService
            builder.Services.AddHttpClient<IDistanceService, GoogleDistanceService>();
            builder.Services.AddHttpContextAccessor();

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddControllersWithViews().AddJsonOptions(opt =>
            {
                opt.JsonSerializerOptions.ReferenceHandler =
                    System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            });
            // Hangfire 設定
            builder.Services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"),
            new SqlServerStorageOptions { PrepareSchemaIfNecessary = true }));

            builder.Services.AddHangfireServer();   // 啟動背景工作

            // Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
                {
                    Title = "LineBotService API",
                    Version = "v1",
                    Description = "Line Bot 與公務車派車系統整合服務"
                });
            });
            // 靜態檔案服務與目錄瀏覽
            builder.Services.AddDirectoryBrowser();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "LineBotService API v1");
                });
            }
            app.UseHangfireDashboard("/hangfire");
            app.UseStaticFiles();
            // 安全中介軟體
            app.UseMiddleware<SecurityMiddleware>(); 
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=LineRichMenu}/{action=Index}/{id?}"
            );

            app.MapControllers();

            app.Run();
        }
    }
}

using Cars.Application.Services;
using Cars.Data;
using Cars.Models;
using Cars.Services;
using Hangfire;
using Hangfire.SqlServer;
using LineBotDemo.Services;
using LineBotService.Services;
using Microsoft.EntityFrameworkCore;

namespace LineBotService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddSingleton<RichMenuService>();
            builder.Services.AddScoped<LineUserService>();
            //builder.Services.AddScoped<AutoDispatcher>();
            //builder.Services.AddScoped<CarApplicationService>();
            //builder.Services.AddScoped<VehicleService>();
            //builder.Services.AddScoped<DriverService>();
            //builder.Services.AddScoped<DispatchService>();
            builder.Services.AddApplication();

            builder.Services.Configure<RichMenuOptions>(builder.Configuration.GetSection("RichMenus"));
            builder.Services.AddScoped<ILinePush, LinePush>();
            builder.Services.AddScoped<NotificationService>();
    




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

            builder.Services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"),
            new SqlServerStorageOptions { PrepareSchemaIfNecessary = true }));

            builder.Services.AddHangfireServer();   // 啟動背景工作者

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

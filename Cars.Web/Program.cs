using Cars.Application.Services;
using Cars.Data;
using Cars.Models;
using Cars.Services;
using Cars.Services.GPS;
using Cars.Services.Hangfire;
using Hangfire;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Cars
{
    public class Program
    {
        public static void Main(string[] args)
        {

            var builder = WebApplication.CreateBuilder(args);

            // === Services ===
            // DbContext

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
            builder.Services.AddHttpContextAccessor();

            // 加入 Hangfire
            builder.Services.AddHangfire(config =>
            {
                config.UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"));
            });

            // 啟動 Hangfire Server
            builder.Services.AddHangfireServer();

            // Google Maps 設定
            builder.Services.Configure<GoogleMapsSettings>(builder.Configuration.GetSection("GoogleMaps"));
            // Google Maps 距離服務
            builder.Services.AddHttpClient();
            builder.Services.AddScoped<IDistanceService, GoogleDistanceService>();

            // 其他服務
            builder.Services.AddScoped<AutoDispatcher>();
            builder.Services.AddScoped<VehicleService>();
            builder.Services.AddScoped<DriverService>();
            builder.Services.AddScoped<CarApplicationService>();
            builder.Services.AddScoped<LineBotNotificationService>();
            builder.Services.AddScoped<DispatchService>();
            builder.Services.AddScoped<CarApplicationUseCase>();

            //GPS 服務
            builder.Services.AddHostedService<VehicleLocationSimulator>();

            // === GPS 服務設定 ===
            var gpsMode = builder.Configuration["GpsMode"];

            if (string.Equals(gpsMode, "Fake", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddSingleton<IGpsProvider, FakeGpsProvider>();
                Console.WriteLine(">>> 使用 FakeGpsProvider 模擬車機");
            }
            else if (string.Equals(gpsMode, "Serial", StringComparison.OrdinalIgnoreCase))
            {
                var port = builder.Configuration["SerialPort"] ?? "COM3";
                builder.Services.AddSingleton<IGpsProvider>(new SerialGpsProvider(port));
                Console.WriteLine($">>> 使用 SerialGpsProvider 串口讀取 ({port})");
            }
            else if (string.Equals(gpsMode, "Http", StringComparison.OrdinalIgnoreCase))
            {
                var url = builder.Configuration["HttpGpsUrl"];
                builder.Services.AddHttpClient<IGpsProvider>(c => c.BaseAddress = new Uri(url));
                builder.Services.AddScoped<IGpsProvider, HttpGpsProvider>();
                Console.WriteLine($">>> 使用 HttpGpsProvider ({url})");
            }
            else
            {
                Console.WriteLine("⚠️ 未設定 GpsMode，預設使用 Fake 模式");
            }



            //模擬車機
            builder.Services.AddSingleton<IGpsProvider, FakeGpsProvider>();

            // MVC
            builder.Services.AddControllersWithViews();
            builder.Services.AddDistributedMemoryCache(); // Session 的暫存
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromHours(8); // session 有效時間
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            Console.WriteLine("Connection string = " + builder.Configuration.GetConnectionString("DefaultConnection"));
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "CarsAuth";
                options.LoginPath = "/Account/Login";   // 沒登入時導向
                options.LogoutPath = "/Auth/Logout"; // 登出路徑
            });

            // JSON 序列化設定
            builder.Services.AddControllers().AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
            });

            builder.Services.AddAuthorization();
            var app = builder.Build();
            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    db.Database.EnsureCreated();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Database initialization failed: {ex.Message}");
                }
            }



            // 注意：不要在這裡額外放一條「無條件」CSP，避免覆蓋與衝突
            if (app.Environment.IsDevelopment())
            {
                // 開發環境：放寬 connect-src 讓 Browser Link / 熱更新正常；放行 Google Maps 與常見 CDN
                app.Use(async (ctx, next) =>
                {
                    ctx.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; " +

                    // JS 腳本（多加 maps.googleapis.com）
                    "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://unpkg.com https://cdn.jsdelivr.net https://*.google.com https://*.gstatic.com https://maps.googleapis.com; " +

                    // Google Maps iframe（多加 maps.googleapis.com）
                    "frame-src 'self' https://www.google.com https://www.google.com/maps https://maps.google.com https://maps.googleapis.com; " +

                    // 樣式/字體
                    "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://fonts.googleapis.com; " +
                    "font-src  'self' https://cdnjs.cloudflare.com https://fonts.gstatic.com; " +

                    // 允許本機端口與 ws/wss
                    "connect-src 'self' http: https: ws: wss: http://localhost:* https://localhost:* ws://localhost:* wss://localhost:*; " +

                    // 圖片來源（多加 gstatic）
                    "img-src 'self' data: https://*.google.com https://*.ggpht.com https://*.googleapis.com https://*.gstatic.com; " +

                    "object-src 'none';";
                    app.UseDeveloperExceptionPage();

                    await next();
                });
            }
            else
            {
                // 正式環境：收斂來源、拿掉 unsafe-eval
                app.Use(async (ctx, next) =>
                {
                    ctx.Response.Headers["Content-Security-Policy"] =
                    "default-src 'self'; " +
                    "script-src 'self' 'unsafe-inline' https://*.google.com https://*.gstatic.com https://maps.googleapis.com; " +
                    "frame-src 'self' https://www.google.com https://www.google.com/maps https://maps.google.com https://maps.googleapis.com; " +
                    "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                    "font-src  'self' https://fonts.gstatic.com; " +
                    "img-src   'self' data: https:; " +
                    "object-src 'none';";

                    await next();
                });
            }

            // === Pipeline ===
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }
            app.UseHangfireDashboard("/hangfire");

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Account}/{action=Login}/{id?}");

            app.Run();
        }
    }
}

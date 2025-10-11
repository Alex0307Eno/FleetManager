using Cars.Application;
using Cars.Data;
using Cars.Models;
using Cars.Services;
using Cars.Services.GPS;
using Hangfire;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
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
            builder.Services.AddApplication();

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
                builder.Services.AddHttpClient<HttpGpsProvider>(c => c.BaseAddress = new Uri(url));
                builder.Services.AddScoped<IGpsProvider, HttpGpsProvider>();
                Console.WriteLine($">>> 使用 HttpGpsProvider ({url})");
            }
            else
            {
                builder.Services.AddSingleton<IGpsProvider,
                FakeGpsProvider>(); 
                Console.WriteLine("⚠️ 未設定 GpsMode，預設使用 Fake 模式");
            }




            // MVC
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

            // ProblemDetails
            builder.Services.AddProblemDetails();
            // JSON 序列化設定
            builder.Services
                .AddControllersWithViews()
                .AddJsonOptions(o =>
                {
                    o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                    
                    
                });

            builder.Services.AddAuthorization();
            var app = builder.Build();
            // 把所有未處理例外轉成 RFC7807
            app.UseExceptionHandler(a => a.Run(async ctx =>
            {
                var feat = ctx.Features.Get<IExceptionHandlerFeature>();
                var ex = feat?.Error;

                var problem = new ProblemDetails
                {
                    Title = "伺服器錯誤",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = app.Environment.IsDevelopment() ? ex?.ToString() : "請聯絡系統管理員",
                    Instance = ctx.Request.Path
                };

                ctx.Response.ContentType = "application/problem+json";
                ctx.Response.StatusCode = problem.Status!.Value;
                await ctx.Response.WriteAsJsonAsync(problem);
            }));
            app.UseStatusCodePages(async context =>
            {
                var res = context.HttpContext.Response;
                var req = context.HttpContext.Request;

                // 只處理非 200 的情況
                if (res.StatusCode >= 400)
                {
                    var problem = new ProblemDetails
                    {
                        Title = res.StatusCode switch
                        {
                            401 => "未授權",
                            403 => "禁止存取",
                            404 => "找不到資源",
                            _ => "請求錯誤"
                        },
                        Status = res.StatusCode,
                        Instance = req.Path
                    };
                    res.ContentType = "application/problem+json";
                    await res.WriteAsJsonAsync(problem);
                }
            });

            using (var scope = app.Services.CreateScope())
            {
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    db.Database.Migrate();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Database initialization failed: {ex.Message}");
                }
            }



            // 注意：不要在這裡額外放一條「無條件」CSP，避免覆蓋與衝突
            if (app.Environment.IsDevelopment())
            {
            app.UseDeveloperExceptionPage();
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
                app.UseHsts();
            }
           


            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();
            app.UseSession();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseHangfireDashboard("/hangfire", new DashboardOptions
            {
                Authorization = new[] { new Cars.Web.Security.MyHangfireAuthFilter() }
            });
            app.MapControllers();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Account}/{action=Login}/{id?}");

            app.Run();
        }
    }
}

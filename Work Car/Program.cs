using Cars.Data;
using Cars.Models;
using Microsoft.EntityFrameworkCore;

namespace Cars
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 加入 DbContext (使用 appsettings.json 的連線字串)
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Google Maps 設定
            builder.Services.Configure<GoogleMapsSettings>(builder.Configuration.GetSection("GoogleMaps"));
            builder.Services.AddHttpClient();

            // 加入 MVC
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            // pipeline 設定
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}

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

            // �[�J DbContext (�ϥ� appsettings.json ���s�u�r��)
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Google Maps �]�w
            builder.Services.Configure<GoogleMapsSettings>(builder.Configuration.GetSection("GoogleMaps"));
            builder.Services.AddHttpClient();

            // �[�J MVC
            builder.Services.AddControllersWithViews();

            var app = builder.Build();

            // pipeline �]�w
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

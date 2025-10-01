using Cars.Data;
using Cars.Features.CarApplications;
using Cars.Features.Vehicles;
using Cars.Features.Drivers;
using Cars.Models;
using Cars.Services;

using LineBotDemo.Services;
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
            builder.Services.AddScoped<CarApplicationService>();
            builder.Services.AddScoped<VehicleService>();
            builder.Services.AddScoped<DriverService>();
            builder.Services.AddScoped<DispatchService>();
            builder.Services.Configure<RichMenuOptions>(builder.Configuration.GetSection("RichMenus"));




            // µù¥U GoogleMapsSettings
            builder.Services.Configure<GoogleMapsSettings>(
                builder.Configuration.GetSection("GoogleMaps"));

            // µù¥U HttpClient + DistanceService
            builder.Services.AddHttpClient<IDistanceService, GoogleDistanceService>();

            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddControllersWithViews().AddJsonOptions(opt =>
            {
                opt.JsonSerializerOptions.ReferenceHandler =
                    System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
            });

            // Swagger
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

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

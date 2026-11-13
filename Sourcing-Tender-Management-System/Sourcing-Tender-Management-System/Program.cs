using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Sourcing_Tender_Management_System.Data;
using Sourcing_Tender_Management_System.Data.Seed;
using Sourcing_Tender_Management_System.Services.TopCommittee;
using Sourcing_Tender_Management_System.Models;

namespace Sourcing_Tender_Management_System
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
                {
                    options.SignIn.RequireConfirmedAccount = true;
                    options.User.RequireUniqueEmail = true;
                })
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.LoginPath = "/Auth/Login";
                options.AccessDeniedPath = "/Auth/AccessDenied";
                options.Events.OnValidatePrincipal = async context =>
                {
                    await SecurityStampValidator.ValidatePrincipalAsync(context);
                    if (context.Principal is null) return;

                    var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                    var user = await userManager.GetUserAsync(context.Principal);
                    var supplierApproved = user?.SupplierId is null || await context.HttpContext.RequestServices
                        .GetRequiredService<ApplicationDbContext>().Suppliers.AsNoTracking()
                        .AnyAsync(x => x.Id == user.SupplierId && x.IsActive && x.RegistrationStatus == SupplierRegistrationStatus.Approved);
                    if (user is null || !user.IsActive || !supplierApproved)
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
                    }
                };
            });
            builder.Services.AddScoped<Sourcing_Tender_Management_System.Services.SupplierFileStore>();
            builder.Services.AddControllersWithViews();
            builder.Services.AddTopCommittee();
            builder.Services.AddBiometrics(builder.Configuration);

            var app = builder.Build();

            AdminBootstrap.CreateFirstAdminAsync(app.Services, app.Configuration).GetAwaiter().GetResult();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
                TopCommitteeDevSeeder.SeedAsync(app.Services).GetAwaiter().GetResult();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            // Only the dedicated supplier registration flow may create public accounts.
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.Equals("/Identity/Account/Register", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Redirect("/Auth/RegisterSupplier");
                    return;
                }
                if (context.Request.Path.Equals("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.Redirect("/Auth/Login");
                    return;
                }

                await next();
            });

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();
            app.MapRazorPages()
               .WithStaticAssets();

            app.Run();
        }
    }
}

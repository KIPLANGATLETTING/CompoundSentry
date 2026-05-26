using Compound_Sentry.Data;
using Compound_Sentry.Hubs;
using Compound_Sentry.Services;
using Compound_Sentry.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext - Using SQL Server (No timeout)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Identity with AdminUser
builder.Services.AddIdentity<AdminUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure cookie settings
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
});

// Add SignalR
builder.Services.AddSignalR();

// Add IP Cache Background Service
builder.Services.AddHostedService<IpCacheService>();


// Add Background Service
builder.Services.AddHostedService<BackgroundScannerService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<DashboardHub>("/dashboardHub");

using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var roleExists = roleManager.RoleExistsAsync("Admin").GetAwaiter().GetResult();
    if (!roleExists)
    {
        roleManager.CreateAsync(new IdentityRole("Admin")).GetAwaiter().GetResult();
        Console.WriteLine("Admin role created.");
    }
}

Console.WriteLine("Application started successfully!");
Console.WriteLine("========================================");
Console.WriteLine("Go to /Account/Register to create admin account");
Console.WriteLine("========================================");

app.Run();
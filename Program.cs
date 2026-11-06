using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PayrollDashboard.Data;
using PayrollDashboard.Models;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("PayrollDatabase")
    ?? throw new InvalidOperationException(
        "Set ConnectionStrings__PayrollDatabase to a PostgreSQL connection string.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

await InitializeDatabaseAsync(app);
await app.RunAsync();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    await using var scope = app.Services.CreateAsyncScope();
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<ApplicationDbContext>();
    await db.Database.EnsureCreatedAsync();

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "HR", "Finance", "Manager", "Employee" })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create role {role}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
            }
        }
    }

    var email = app.Configuration["BootstrapAdmin:Email"];
    var password = app.Configuration["BootstrapAdmin:Password"];
    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException(
            "Set BootstrapAdmin__Email and BootstrapAdmin__Password to create the first administrator.");
    }

    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var administrator = await userManager.FindByEmailAsync(email);
    if (administrator is null)
    {
        administrator = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Administrator"
        };
        var result = await userManager.CreateAsync(administrator, password);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create the initial administrator: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }

    if (!await userManager.IsInRoleAsync(administrator, "Admin"))
    {
        var roleResult = await userManager.AddToRoleAsync(administrator, "Admin");
        if (!roleResult.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not assign the administrator role: {string.Join("; ", roleResult.Errors.Select(error => error.Description))}");
        }
    }
}

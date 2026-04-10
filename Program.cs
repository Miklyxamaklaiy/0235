using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.EntityFrameworkCore;
using PlechPomoshchi.Data;
using PlechPomoshchi.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// DB
builder.Services.AddDbContext<ApplicationDbContext>(opt =>
{
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default"));
});

// Cookie auth (простая авторизация без полного Identity UI)
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.AccessDeniedPath = "/Account/Denied";
        opt.Cookie.Name = "pp_auth";
        opt.SlidingExpiration = true;
        opt.ExpireTimeSpan = TimeSpan.FromDays(14);
    });

builder.Services.AddAuthorization();

// Безопасность cookies
builder.Services.Configure<CookiePolicyOptions>(opt =>
{
    opt.MinimumSameSitePolicy = SameSiteMode.Lax;
    opt.HttpOnly = HttpOnlyPolicy.Always;
    opt.Secure = CookieSecurePolicy.SameAsRequest;
});

// Anti-forgery
builder.Services.AddAntiforgery(opt =>
{
    opt.HeaderName = "X-CSRF-TOKEN";
});

// App services
builder.Services.AddHttpClient();
builder.Services.AddScoped<OrgParser>();
builder.Services.AddScoped<EmailSender>();
builder.Services.AddScoped<Seeder>();

var app = builder.Build();

// Initial seed and synchronization
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<Seeder>();
    await seeder.SeedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCookiePolicy();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

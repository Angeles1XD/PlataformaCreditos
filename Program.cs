using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;

var builder = WebApplication.CreateBuilder(args);

// 🔹 CONEXIÓN SQLITE
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// 🔥 IDENTITY
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();


// =======================================
// 🔥 REDIS
// =======================================
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis__ConnectionString"]
        ?? throw new Exception("Redis no configurado");
});


// =======================================
// 🔥 SESIÓN
// =======================================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


// =======================================

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// 🔥 ORDEN CORRECTO
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// 🔥 RUTAS
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
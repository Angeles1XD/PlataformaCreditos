using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;

var builder = WebApplication.CreateBuilder(args);

// =======================================
// 🔹 SQLITE (IMPORTANTE PARA RENDER)
// =======================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=/tmp/app.db"; // fallback seguro

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));


// =======================================
// 🔥 IDENTITY
// =======================================
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();


// =======================================
// 🔥 REDIS (SIN CRASH)
// =======================================
var redisConnection = builder.Configuration["Redis__ConnectionString"];

if (!string.IsNullOrEmpty(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnection;
        options.InstanceName = "PlataformaCreditos_";
    });
}
else
{
    Console.WriteLine("⚠ Redis no configurado, usando memoria local");
}


// =======================================
// 🔥 SESIONES
// =======================================
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});


// =======================================
// 🔥 APP
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


// =======================================
// 🔥 RUTAS
// =======================================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();


// =======================================
// 🔥 CREAR BD AUTOMÁTICAMENTE (CLAVE)
// =======================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}


// =======================================

app.Run();
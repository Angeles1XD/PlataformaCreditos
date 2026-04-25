using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PlataformaCreditos.Data;
using PlataformaCreditos.Models;

var builder = WebApplication.CreateBuilder(args);

// 🔹 CONEXIÓN SQLITE
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// 🔥 IDENTITY CON ROLES
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
    options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// 🔹 PIPELINE
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // ✅ IMPORTANTE EN NET 8
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ❌ ELIMINADO: MapStaticAssets()
// ❌ ELIMINADO: WithStaticAssets()

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();


// =======================================
// 🔥 SEED DE DATOS + ROL + USUARIO ANALISTA
// =======================================

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    // 🔹 CREAR ROL ANALISTA
    if (!await roleManager.RoleExistsAsync("Analista"))
    {
        await roleManager.CreateAsync(new IdentityRole("Analista"));
    }

    // 🔹 CREAR USUARIO ANALISTA
    var email = "analista@test.com";
    var password = "P@ssw0rd!";

    var user = await userManager.FindByEmailAsync(email);

    if (user == null)
    {
        user = new IdentityUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            throw new Exception("Error creando usuario Analista");
        }
    }

    // 🔹 ASIGNAR ROL ANALISTA
    if (!await userManager.IsInRoleAsync(user, "Analista"))
    {
        await userManager.AddToRoleAsync(user, "Analista");
    }

    // 🔹 CREAR CLIENTES Y SOLICITUDES
    if (!context.Clientes.Any())
    {
        var cliente1 = new Cliente
        {
            UsuarioId = "user1",
            IngresosMensuales = 2000,
            Activo = true
        };

        var cliente2 = new Cliente
        {
            UsuarioId = "user2",
            IngresosMensuales = 3000,
            Activo = true
        };

        context.Clientes.AddRange(cliente1, cliente2);
        context.SaveChanges();

        context.Solicitudes.AddRange(
            new SolicitudCredito
            {
                ClienteId = cliente1.Id,
                MontoSolicitado = 500,
                FechaSolicitud = DateTime.Now,
                Estado = EstadoSolicitud.Pendiente
            },
            new SolicitudCredito
            {
                ClienteId = cliente2.Id,
                MontoSolicitado = 1000,
                FechaSolicitud = DateTime.Now,
                Estado = EstadoSolicitud.Aprobado
            }
        );

        context.SaveChanges();
    }
}

// =======================================

app.Run();
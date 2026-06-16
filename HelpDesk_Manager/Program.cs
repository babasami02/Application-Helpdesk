using HelpDesk_Manager.Data;
using HelpDesk_Manager.Models;
using HelpDesk_Manager.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccesDenie";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<HelpDesk_Manager.Services.NotificationService>();
builder.Services.AddHostedService<FermetureAutomatiqueTicketsService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Erreur");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// Seeding initial — seulement si aucun admin n'existe
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    var adminRole = db.Roles.FirstOrDefault(r => r.NomRole == "Administrateur");
    if (adminRole != null && !db.Utilisateurs.Any(u => u.IdRole == adminRole.IdRole))
    {
        db.Utilisateurs.Add(new Utilisateur
        {
            Nom = "Admin",
            Prenom = "Système",
            Email = "admin@novec.ma",
            MotDePasse = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
            IdRole = adminRole.IdRole,
            IsActive = true,
            DateCreation = DateTime.Now
        });
        db.SaveChanges();
    }
}

app.Run();

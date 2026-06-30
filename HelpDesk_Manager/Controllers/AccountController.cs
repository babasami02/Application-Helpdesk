using HelpDesk_Manager.Data;
using HelpDesk_Manager.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HelpDesk_Manager.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _db;

        public AccountController(AppDbContext db)
        {
            _db = db;
        }

        // GET /Account/Login
        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Index", "Home");
            return View();
        }

        // POST /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string motDePasse)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(motDePasse))
            {
                ViewBag.Erreur = "Veuillez remplir tous les champs.";
                return View();
            }

            var utilisateur = await _db.Utilisateurs
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

            if (utilisateur == null || !BCrypt.Net.BCrypt.Verify(motDePasse, utilisateur.MotDePasse))
            {
                ViewBag.Erreur = "Email ou mot de passe incorrect.";
                return View();
            }

            // Mise à jour de la dernière connexion
            utilisateur.DernierAcces = DateTime.Now;
            await _db.SaveChangesAsync();

            // Création des claims
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, utilisateur.IdUtilisateur.ToString()),
                new Claim(ClaimTypes.Name,            utilisateur.NomComplet),
                new Claim(ClaimTypes.Email,           utilisateur.Email),
                new Claim(ClaimTypes.Role,            utilisateur.Role!.NomRole),
                new Claim("IdUtilisateur",            utilisateur.IdUtilisateur.ToString()),
                new Claim("NomComplet",               utilisateur.NomComplet),
            };

            var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = true });

            // Redirection selon le rôle
            return utilisateur.Role!.NomRole switch
            {
                "Employe"        => RedirectToAction("Index",       "Home"),
                "Helpdesk"       => RedirectToAction("Index",       "Home"),
                "Technicien"     => RedirectToAction("Index",  "Home"),
                "Administrateur" => RedirectToAction("Index",       "Home"),
                _                => RedirectToAction("Index",       "Home")
            };
        }

        // GET /Account/Logout
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        // GET /Account/AccesDenie
        public IActionResult AccesDenie()
        {
            return View();
        }
    }
}

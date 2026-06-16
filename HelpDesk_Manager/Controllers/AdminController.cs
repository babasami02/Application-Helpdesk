using HelpDesk_Manager.Data;
using HelpDesk_Manager.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk_Manager.Controllers
{
    [Authorize(Roles = "Administrateur")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        public AdminController(AppDbContext db) { _db = db; }

        // ── Liste utilisateurs ───────────────────────────────────
        public async Task<IActionResult> Index(string? recherche, int? idRole)
        {
            ViewData["Title"] = "Gestion Utilisateurs";

            var query = _db.Utilisateurs
                .Include(u => u.Role)
                .AsQueryable();

            if (!string.IsNullOrEmpty(recherche))
                query = query.Where(u => u.Nom.Contains(recherche)
                                      || u.Prenom.Contains(recherche)
                                      || u.Email.Contains(recherche));

            if (idRole.HasValue)
                query = query.Where(u => u.IdRole == idRole);

            var utilisateurs = await query
                .OrderBy(u => u.IdRole)
                .ThenBy(u => u.Nom)
                .ToListAsync();

            ViewBag.Recherche = recherche;
            ViewBag.IdRole = idRole;
            ViewBag.Roles = new SelectList(
                await _db.Roles.ToListAsync(), "IdRole", "NomRole");

            return View(utilisateurs);
        }

        // ── Créer utilisateur GET ────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Creer()
        {
            ViewData["Title"] = "Nouvel Utilisateur";
            await ChargerViewBags();
            return View();
        }

        // ── Créer utilisateur POST ───────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Creer(Utilisateur utilisateur, string motDePasse)
        {
            // Vérifier email unique
            if (await _db.Utilisateurs.AnyAsync(u => u.Email == utilisateur.Email))
            {
                ModelState.AddModelError("Email", "Cet email est déjà utilisé.");
                await ChargerViewBags();
                return View(utilisateur);
            }

            utilisateur.MotDePasse = BCrypt.Net.BCrypt.HashPassword(motDePasse);
            utilisateur.DateCreation = DateTime.Now;
            utilisateur.IsActive = true;

            _db.Utilisateurs.Add(utilisateur);
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Utilisateur {utilisateur.NomComplet} créé avec succès.";
            return RedirectToAction("Index");
        }

        // ── Modifier utilisateur GET ─────────────────────────────
        [HttpGet]
        public async Task<IActionResult> Modifier(int id)
        {
            ViewData["Title"] = "Modifier Utilisateur";

            var utilisateur = await _db.Utilisateurs
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.IdUtilisateur == id);

            if (utilisateur == null) return NotFound();

            await ChargerViewBags();
            return View(utilisateur);
        }

        // ── Modifier utilisateur POST ────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Modifier(int id, Utilisateur model, string? nouveauMotDePasse)
        {
            var utilisateur = await _db.Utilisateurs.FindAsync(id);
            if (utilisateur == null) return NotFound();

            // Vérifier email unique (sauf pour lui-même)
            if (await _db.Utilisateurs.AnyAsync(u => u.Email == model.Email
                                                   && u.IdUtilisateur != id))
            {
                ModelState.AddModelError("Email", "Cet email est déjà utilisé.");
                await ChargerViewBags();
                return View(model);
            }

            utilisateur.Nom = model.Nom;
            utilisateur.Prenom = model.Prenom;
            utilisateur.Email = model.Email;
            utilisateur.IdRole = model.IdRole;
            utilisateur.Direction = model.Direction;
            utilisateur.Departement = model.Departement;
            utilisateur.Niveau = model.Niveau;
            utilisateur.Specialite = model.Specialite;
            utilisateur.Telephone = model.Telephone;
            utilisateur.IsActive = model.IsActive;

            if (!string.IsNullOrEmpty(nouveauMotDePasse))
                utilisateur.MotDePasse = BCrypt.Net.BCrypt.HashPassword(nouveauMotDePasse);

            await _db.SaveChangesAsync();
            TempData["Success"] = "Utilisateur modifié avec succès.";
            return RedirectToAction("Index");
        }

        // ── Activer / Désactiver ─────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActif(int id)
        {
            var utilisateur = await _db.Utilisateurs.FindAsync(id);
            if (utilisateur == null) return NotFound();

            utilisateur.IsActive = !utilisateur.IsActive;
            await _db.SaveChangesAsync();

            TempData["Success"] = utilisateur.IsActive
                ? $"{utilisateur.NomComplet} activé."
                : $"{utilisateur.NomComplet} désactivé.";

            return RedirectToAction("Index");
        }

        // ── Helper ───────────────────────────────────────────────
        private async Task ChargerViewBags()
        {
            ViewBag.Roles = new SelectList(
                await _db.Roles.ToListAsync(), "IdRole", "NomRole");
        }
        // ── Configuration SLA ────────────────────────────────────────
        public async Task<IActionResult> SLA()
        {
            ViewData["Title"] = "Configuration SLA";

            var slas = await _db.ConfigurationsSLA
                .Include(s => s.Urgence)
                .Include(s => s.Impact)
                .Include(s => s.Domaine)
                .Where(s => s.IdDomaine != null)
                .OrderBy(s => s.Urgence!.Ordre)
                .ThenBy(s => s.Impact!.Ordre)
                .ToListAsync();

            ViewBag.Urgences = await _db.NiveauxUrgence.OrderBy(u => u.Ordre).ToListAsync();
            ViewBag.Impacts = await _db.NiveauxImpact.OrderBy(i => i.Ordre).ToListAsync();
            ViewBag.Domaines = await _db.Domaines.Where(c => c.IsActive).ToListAsync();

            return View(slas);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ModifierSLA(int idSLA, int delaiReponsHeures,
                                                      int delaiResolutionHeures, string description)
        {
            var sla = await _db.ConfigurationsSLA.FindAsync(idSLA);
            if (sla == null) return NotFound();

            sla.DelaiReponsHeures = delaiReponsHeures;
            sla.DelaiResolutionHeures = delaiResolutionHeures;
            sla.Description = description;

            await _db.SaveChangesAsync();
            TempData["Success"] = "SLA mis à jour avec succès.";
            return RedirectToAction("SLA");
        }
    }
}

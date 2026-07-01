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
                .Include(s => s.Domaine)
                .OrderBy(s => s.Priorite)
                .ThenBy(s => s.Domaine!.NomDomaine)
                .ToListAsync();

            ViewBag.Domaines = await _db.Domaines.OrderBy(d => d.IdDomaine).ToListAsync();
            ViewBag.Priorites = new List<string> { "P1", "P2", "P3", "P4" };

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

        // ── Catégories / Sous-catégories ─────────────────────────────
        public async Task<IActionResult> Categories(int? idDomaine)
        {
            ViewData["Title"] = "Gestion des catégories";

            var domaines = await _db.Domaines
                .Include(d => d.Categories!.Where(c => c.IsActive))
                    .ThenInclude(c => c.SousCategories.Where(sc => sc.IsActive))
                .Where(d => d.IsActive)
                .OrderBy(d => d.IdDomaine)
                .ToListAsync();

            ViewBag.IdDomaineActif = idDomaine ?? domaines.FirstOrDefault()?.IdDomaine ?? 0;

            return View(domaines);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AjouterCategorie(string nomCategorie, int idDomaine)
        {
            if (!string.IsNullOrWhiteSpace(nomCategorie))
            {
                _db.Categories.Add(new Categorie
                {
                    NomCategorie = nomCategorie.Trim(),
                    IdDomaine = idDomaine
                });
                await _db.SaveChangesAsync();
                TempData["Success"] = "Catégorie ajoutée.";
            }
            return RedirectToAction("Categories", new { idDomaine });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ModifierCategorie(int idCategorie, string nomCategorie)
        {
            var cat = await _db.Categories.FindAsync(idCategorie);
            if (cat == null) return NotFound();
            cat.NomCategorie = nomCategorie.Trim();
            await _db.SaveChangesAsync();
            TempData["Success"] = "Catégorie modifiée.";
            return RedirectToAction("Categories", new { idDomaine = cat!.IdDomaine });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SupprimerCategorie(int idCategorie)
        {
            var cat = await _db.Categories
                .Include(c => c.SousCategories)
                .FirstOrDefaultAsync(c => c.IdCategorie == idCategorie);
            if (cat == null) return NotFound();

            // Désactiver la catégorie et toutes ses sous-catégories
            cat.IsActive = false;
            foreach (var sc in cat.SousCategories)
                sc.IsActive = false;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Catégorie désactivée.";
            return RedirectToAction("Categories", new { idDomaine = cat!.IdDomaine });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AjouterSousCategorie(string nomSousCategorie, int idCategorie)
        {
            var cat = await _db.Categories.FindAsync(idCategorie);
            if (!string.IsNullOrWhiteSpace(nomSousCategorie))
            {
                _db.SousCategories.Add(new SousCategorie
                {
                    NomSousCategorie = nomSousCategorie.Trim(),
                    IdCategorie = idCategorie
                });
                await _db.SaveChangesAsync();
                TempData["Success"] = "Sous-catégorie ajoutée.";
            }
            return RedirectToAction("Categories", new { idDomaine = cat!.IdDomaine });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ModifierSousCategorie(int idSousCategorie, string nomSousCategorie)
        {

            var sc = await _db.SousCategories.Include(s => s.Categorie)
                .FirstOrDefaultAsync(s => s.IdSousCategorie == idSousCategorie);

            if (sc == null) return NotFound();
            sc.NomSousCategorie = nomSousCategorie.Trim();
            await _db.SaveChangesAsync();
            TempData["Success"] = "Sous-catégorie modifiée.";
            return RedirectToAction("Categories", new { idDomaine = sc!.Categorie!.IdDomaine });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SupprimerSousCategorie(int idSousCategorie)
        {
            var sc = await _db.SousCategories.Include(s => s.Categorie)
                .FirstOrDefaultAsync(s => s.IdSousCategorie == idSousCategorie);
            if (sc == null) return NotFound();

            sc.IsActive = false;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Sous-catégorie désactivée.";
            return RedirectToAction("Categories", new { idDomaine = sc!.Categorie!.IdDomaine });
        }
    }
}

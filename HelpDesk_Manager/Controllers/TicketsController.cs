using HelpDesk_Manager.Data;
using HelpDesk_Manager.Helpers;
using HelpDesk_Manager.Models;
using HelpDesk_Manager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HelpDesk_Manager.Controllers
{
    [Authorize]
    public class TicketsController : Controller
    {
        // Remplacez le constructeur
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly NotificationService _notif;

        public TicketsController(AppDbContext db, IWebHostEnvironment env, NotificationService notif)
        {
            _db = db;
            _env = env;
            _notif = notif;
        }

        // ── Mes Tickets ─────────────────────────────────────────
        [Authorize(Roles = "Employe")]
        public async Task<IActionResult> Index(string? recherche, string? statut, int? annee, int page = 1)
        {
            ViewData["Title"] = "Mes Tickets";
            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);

            var query = _db.Tickets
                .Include(t => t.Statut)
                .Include(t => t.Domaine)
                .Where(t => t.IdDemandeur == idUser)
                .AsQueryable();

            annee ??= DateTime.Now.Year;
            query = query.Where(t => t.DateOuverture.Year == annee);

            if (!string.IsNullOrEmpty(recherche))
                query = query.Where(t => t.Titre.Contains(recherche) ||
                                         t.IdTicket.ToString().Contains(recherche));

            if (!string.IsNullOrEmpty(statut))
                query = query.Where(t => t.Statut!.NomStatut == statut);

            query = query.OrderByDescending(t => t.DateOuverture);

            var resultat = PagedList<Ticket>.Creer(query, page, 10);

            ViewBag.Annee = annee;
            ViewBag.Annees = await _db.Tickets
                .Where(t => t.IdDemandeur == idUser)
                .Select(t => t.DateOuverture.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();
            ViewBag.Recherche = recherche;
            ViewBag.Statut = statut;
            ViewBag.Statuts = await _db.StatutsTicket.ToListAsync();
            ViewBag.PageActuelle = resultat.PageActuelle;
            ViewBag.TotalPages = resultat.TotalPages;
            ViewBag.TotalItems = resultat.TotalItems;
            ViewBag.PageSize = resultat.PageSize;

            return View(resultat.Items);
        }

        // ── Créer un ticket GET ──────────────────────────────────
        [Authorize(Roles = "Employe")]
        [HttpGet]
        public async Task<IActionResult> Creer()
        {
            ViewData["Title"] = "Nouveau Ticket";
            await ChargerListesDeroulantes();
            return View();
        }

        // ── Créer un ticket POST ─────────────────────────────────
        [Authorize(Roles = "Employe")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Creer(Ticket ticket, IFormFile? fichier)
        {
            if (!ModelState.IsValid)
            {
                await ChargerListesDeroulantes();
                return View(ticket);
            }

            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);
            var statut = await _db.StatutsTicket.FirstAsync(s => s.NomStatut == "Nouveau");

            ticket.IdDemandeur = idUser;
            ticket.IdStatut = statut.IdStatut;
            ticket.DateOuverture = DateTime.Now;

            // --- Annee Actuelle numerotation ---------
            var anneeActuelle = DateTime.Now.Year;
            var dernierNumero = await _db.Tickets
                .Where(t => t.DateOuverture.Year == anneeActuelle)
                .CountAsync();
            ticket.NumeroAnnuel = dernierNumero + 1;

            _db.Tickets.Add(ticket);
            await _db.SaveChangesAsync();
            var demandeur = await _db.Utilisateurs.FindAsync(idUser);
            await _notif.TicketCreeAsync(ticket, demandeur!.NomComplet);

            // Pièce jointe
            if (fichier != null && fichier.Length > 0)
            {
                var webRoot = _env.WebRootPath
                              ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

                var dossier = Path.Combine(webRoot, "uploads");
                Directory.CreateDirectory(dossier);

                var nomFichier = $"{ticket.IdTicket}_{Path.GetFileName(fichier.FileName)}";
                var chemin = Path.Combine(dossier, nomFichier);

                using var stream = new FileStream(chemin, FileMode.Create);
                await fichier.CopyToAsync(stream);

                _db.PiecesJointes.Add(new PieceJointe
                {
                    IdTicket = ticket.IdTicket,
                    IdUtilisateur = idUser,
                    NomFichier = fichier.FileName,
                    CheminFichier = $"/uploads/{nomFichier}",
                    Taille = fichier.Length,
                    Format = Path.GetExtension(fichier.FileName).TrimStart('.')
                });
                await _db.SaveChangesAsync();
            }

            // Historique
            _db.HistoriqueTickets.Add(new HistoriqueTicket
            {
                IdTicket = ticket.IdTicket,
                IdUtilisateur = idUser,
                Action = "Création",
                NouvelleValeur = "Nouveau"
            });
            await _db.SaveChangesAsync();

            TempData["Success"] = $"Ticket #{ticket.IdTicket} créé avec succès !";
            return RedirectToAction("Index");
        }

        // ── Détail d'un ticket ───────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            ViewData["Title"] = $"Ticket #{id}";
            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);

            var ticket = await _db.Tickets
                .Include(t => t.Statut)
                .Include(t => t.Domaine)
                .Include(t => t.Categorie)
                .Include(t => t.SousCategorie)
                .Include(t => t.Nature)
                .Include(t => t.Urgence)
                .Include(t => t.Impact)
                .Include(t => t.Demandeur)
                .Include(t => t.PiecesJointes)
                .Include(t => t.Interventions)
                    .ThenInclude(i => i.Technicien)
                .Include(t => t.Interventions)
                    .ThenInclude(i => i.Statut)
                .Include(t => t.Historique)
                    .ThenInclude(h => h.Utilisateur)
                .FirstOrDefaultAsync(t => t.IdTicket == id);

            if (ticket == null) return NotFound();

            // Employé ne voit que ses propres tickets
            if (User.IsInRole("Employe") && ticket.IdDemandeur != idUser)
                return Forbid();

            return View(ticket);
        }

        // ── Helpers ──────────────────────────────────────────────
        private Task ChargerListesDeroulantes()
        {
            // Domaine et Nature sont renseignés par le Helpdesk (qualification)
            // Aucune liste déroulante nécessaire pour le formulaire employé
            return Task.CompletedTask;
        }
        // ── Note prestation GET ──────────────────────────────────────
        [Authorize(Roles = "Employe")]
        [HttpGet]
        public async Task<IActionResult> Noter(int id)
        {
            ViewData["Title"] = $"Noter le ticket #{id}";
            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);

            var ticket = await _db.Tickets
                .Include(t => t.Statut)
                .Include(t => t.Domaine)
                .Include(t => t.Interventions)
                    .ThenInclude(i => i.Technicien)
                .FirstOrDefaultAsync(t => t.IdTicket == id && t.IdDemandeur == idUser);

            if (ticket == null) return NotFound();


            // Seulement si résolu
            if (ticket.Statut?.NomStatut != "Résolu")
            {
                TempData["Erreur"] = "Ce ticket n'est pas encore résolu.";
                return RedirectToAction("Index");
            }

            // Déjà noté ?
            if (ticket.NoteEmployee.HasValue)
            {
                TempData["Erreur"] = "Vous avez déjà noté ce ticket.";
                return RedirectToAction("Index");
            }

            return View(ticket);
        }

        // ── Note prestation POST ─────────────────────────────────────
        [Authorize(Roles = "Employe")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Noter(int idTicket, int note, string? commentaire)
        {
            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);

            var ticket = await _db.Tickets
                .FirstOrDefaultAsync(t => t.IdTicket == idTicket && t.IdDemandeur == idUser);

            if (ticket == null) return NotFound();
            if ((note == 1 || note == 2) && string.IsNullOrWhiteSpace(commentaire))
            {
                TempData["Erreur"] = "Un commentaire est obligatoire pour une note de 1 ou 2 étoiles.";

                var ticketComplet = await _db.Tickets
                    .Include(t => t.Statut)
                    .Include(t => t.Domaine)
                    .Include(t => t.Interventions)
                        .ThenInclude(i => i.Technicien)
                    .FirstOrDefaultAsync(t => t.IdTicket == idTicket && t.IdDemandeur == idUser);

                ViewData["Title"] = $"Noter le ticket #{idTicket}";
                return View(ticketComplet);
            }

            ticket.NoteEmployee = note;
            ticket.CommentaireNote = commentaire;

            // Fermer définitivement le ticket
            var statut = await _db.StatutsTicket.FirstAsync(s => s.NomStatut == "Fermé");
            ticket.IdStatut = statut.IdStatut;
            ticket.DateFermeture = DateTime.Now;

            _db.HistoriqueTickets.Add(new HistoriqueTicket
            {
                IdTicket = ticket.IdTicket,
                IdUtilisateur = idUser,
                Action = "Note prestation",
                NouvelleValeur = $"{note}/5 — {commentaire}"
            });

            await _db.SaveChangesAsync();
            TempData["Success"] = "Merci pour votre évaluation !";
            return RedirectToAction("Index");
        }
    }

}
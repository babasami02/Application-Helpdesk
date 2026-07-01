using HelpDesk_Manager.Data;
using HelpDesk_Manager.Helpers;
using HelpDesk_Manager.Models;
using HelpDesk_Manager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk_Manager.Controllers
{
    [Authorize(Roles = "Helpdesk,Administrateur")]
    public class HelpdeskController : Controller
    {
        // Remplacez le constructeur
        private readonly AppDbContext _db;
        private readonly NotificationService _notif;

        public HelpdeskController(AppDbContext db, NotificationService notif)
        {
            _db = db;
            _notif = notif;
        }
        // ── Consultation tickets ─────────────────────────────────
        public async Task<IActionResult> Index(string? recherche, string? statut,
                                        string? priorite, int? domaine, int? annee, int page = 1)
        {
            ViewData["Title"] = "Consultation Tickets";

            annee ??= DateTime.Now.Year; 

            var query = _db.Tickets
                .Include(t => t.Statut)
                .Include(t => t.Domaine)
                .Include(t => t.Urgence)
                .Include(t => t.Demandeur)
                .Include(t => t.Interventions)
                .AsQueryable();

            query = query.Where(t => t.DateOuverture.Year == annee);

            if (!string.IsNullOrEmpty(recherche))
                query = query.Where(t => t.Titre.Contains(recherche) ||
                                         t.IdTicket.ToString().Contains(recherche));

            if (!string.IsNullOrEmpty(statut))
                query = query.Where(t => t.Statut!.NomStatut == statut);

            if (!string.IsNullOrEmpty(priorite))
                query = query.Where(t => t.PrioriteCalculee == priorite);

            if (domaine.HasValue)
                query = query.Where(t => t.IdDomaine == domaine);

            query = query.OrderByDescending(t => t.DateOuverture);

            var resultat = PagedList<Ticket>.Creer(query, page, 10);

            ViewBag.Recherche = recherche;
            ViewBag.Statut = statut;
            ViewBag.Priorite = priorite;
            ViewBag.Annee = annee;
            ViewBag.Annees = await _db.Tickets
                .Select(t => t.DateOuverture.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();
            ViewBag.Domaine = domaine;

            ViewBag.Statuts = await _db.StatutsTicket.ToListAsync();
            ViewBag.Domaines = new SelectList(
                await _db.Domaines.ToListAsync(), "IdDomaine", "NomDomaine");
            ViewBag.PageActuelle = resultat.PageActuelle;
            ViewBag.TotalPages = resultat.TotalPages;
            ViewBag.TotalItems = resultat.TotalItems;
            ViewBag.PageSize = resultat.PageSize;

            return View(resultat.Items);
        }

        // ── Détail ticket ────────────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            ViewData["Title"] = $"Ticket #{id}";

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

            await ChargerViewBags(ticket);
            return View(ticket);
        }

        // ── Qualifier ticket POST ────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Qualifier(int idTicket, int? idUrgence, int? idImpact,
                                                    int? idDomaine, int? idNature, int? idCategorie, int? idSousCategorie)
        {
            var ticket = await _db.Tickets
                .Include(t => t.Urgence)
                .Include(t => t.Impact)
                .FirstOrDefaultAsync(t => t.IdTicket == idTicket);

            if (ticket == null) return NotFound();

            if (!idDomaine.HasValue || !idNature.HasValue || !idUrgence.HasValue || !idImpact.HasValue)
            {
                TempData["Error"] = "Veuillez renseigner le domaine, la nature, l'urgence et l'impact avant de qualifier le ticket.";
                return RedirectToAction("Details", new { id = idTicket });
            }

            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);

            ticket.IdUrgence = idUrgence.Value;
            ticket.IdImpact = idImpact.Value;
            ticket.IdDomaine = idDomaine.Value;
            ticket.IdNature = idNature.Value;
            ticket.IdCategorie = idCategorie;
            ticket.IdSousCategorie = idSousCategorie;
            ticket.IdHelpdesk = idUser;

            var urgence = await _db.NiveauxUrgence.FindAsync(idUrgence.Value);
            var impact = await _db.NiveauxImpact.FindAsync(idImpact.Value);

            // Calculer la priorité depuis urgence × impact
            var prioriteCalculee = CalculerPriorite(urgence!.Ordre, impact!.Ordre);
            ticket.PrioriteCalculee = prioriteCalculee;

            // Chercher le SLA correspondant à cette priorité et ce domaine
            var sla = await _db.ConfigurationsSLA
                .FirstOrDefaultAsync(s => s.Priorite == prioriteCalculee
                                        && s.IdDomaine == idDomaine.Value);

            ticket.DelaiResolutionCible = sla != null
                                          ? DateTime.Now.AddHours(sla.DelaiResolutionHeures)
                                          : null;



            _db.HistoriqueTickets.Add(new HistoriqueTicket
            {
                IdTicket = ticket.IdTicket,
                IdUtilisateur = idUser,
                Action = "Qualification",
                NouvelleValeur = $"Priorité: {ticket.PrioriteCalculee}"
            });

            await _db.SaveChangesAsync();
            await _notif.TicketQualifieAsync(ticket);
            TempData["Success"] = "Ticket qualifié avec succès.";
            return RedirectToAction("Details", new { id = idTicket });
        }

        // ── Hors périmètre POST ──────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HorsPerimetre(int idTicket, string? motifHorsPerimetre)
        {
            var ticket = await _db.Tickets.FindAsync(idTicket);
            if (ticket == null) return NotFound();

            if (ticket.PrioriteCalculee != null)
            {
                TempData["Error"] = "Impossible de classer hors périmètre un ticket déjà qualifié.";
                return RedirectToAction("Details", new { id = idTicket });
            }

            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);
            var statut = await _db.StatutsTicket.FirstAsync(s => s.NomStatut == "Hors périmètre");

            ticket.IdStatut = statut.IdStatut;
            var motif = string.IsNullOrWhiteSpace(motifHorsPerimetre) ? null : motifHorsPerimetre.Trim();

            ticket.MotifRejet = motif;

            _db.HistoriqueTickets.Add(new HistoriqueTicket
            {
                IdTicket = ticket.IdTicket,
                IdUtilisateur = idUser,
                Action = "Hors périmètre",
                NouvelleValeur = motif
            });

            await _db.SaveChangesAsync();
            await _notif.TicketHorsPerimetreAsync(ticket, motif);
            TempData["Success"] = "Ticket classé Hors périmètre.";
            return RedirectToAction("Index");
        }

        // ── Annuler ticket POST ────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Annuler(int idTicket, string motifAnnulation)
        {
            var ticket = await _db.Tickets.FindAsync(idTicket);
            if (ticket == null) return NotFound();

            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);
            var statut = await _db.StatutsTicket.FirstAsync(s => s.NomStatut == "Annulé");

            ticket.IdStatut = statut.IdStatut;
            ticket.MotifRejet = motifAnnulation;

            _db.HistoriqueTickets.Add(new HistoriqueTicket
            {
                IdTicket = ticket.IdTicket,
                IdUtilisateur = idUser,
                Action = "Annulation",
                NouvelleValeur = motifAnnulation
            });

            await _db.SaveChangesAsync();
            await _notif.TicketAnnuleAsync(ticket, motifAnnulation);
            TempData["Success"] = "Ticket annulé.";
            return RedirectToAction("Index");
        }

        // ── Ajouter intervention POST ────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AjouterIntervention(int idTicket, int idTechnicien,
                                                              string? descriptionAction,
                                                      DateTime? datePlanifiee)
        {
            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);
            var statut = await _db.StatutsIntervention.FirstAsync(s => s.NomStatut == "Planifiée");

            if (string.IsNullOrWhiteSpace(descriptionAction))
            {
                var ticketDesc = await _db.Tickets.FindAsync(idTicket);
                descriptionAction = ticketDesc?.Description ?? "";
            }

            var intervention = new Intervention
            {
                IdTicket = idTicket,
                IdTechnicien = idTechnicien,
                DescriptionAction = descriptionAction,
                IdStatut = statut.IdStatut,
                DateAction = DateTime.Now,
                DatePlanifiee = datePlanifiee ?? DateTime.Now
            };

            var dernierNumero = await _db.Interventions
                .Where(i => i.IdTicket == idTicket)
                .CountAsync();
            intervention.NumeroIntervention = dernierNumero + 1;
            _db.Interventions.Add(intervention);
            // Passer le ticket en "En cours" à la première intervention
            var ticket = await _db.Tickets.FindAsync(idTicket);
            if (ticket != null)
            {
                var premierIntervention = !await _db.Interventions
                    .AnyAsync(i => i.IdTicket == idTicket);

                if (premierIntervention)
                {
                    var statutEnCours = await _db.StatutsTicket
                        .FirstAsync(s => s.NomStatut == "En cours");
                    ticket.IdStatut = statutEnCours.IdStatut;
                }
            }

            var tech = await _db.Utilisateurs.FindAsync(idTechnicien);
            _db.HistoriqueTickets.Add(new HistoriqueTicket
            {
                IdTicket = idTicket,
                IdUtilisateur = idUser,
                Action = "Ajout intervention",
                NouvelleValeur = $"Assignée à {tech?.NomComplet}"
            });

            await _db.SaveChangesAsync();
            await _notif.InterventionAssigneeAsync(intervention,
                ticket!.Titre);
            TempData["Success"] = "Intervention ajoutée avec succès.";
            return RedirectToAction("Details", new { id = idTicket });
        }

        // ── Clôturer ticket POST ─────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cloturer(int idTicket, string? remarque)
        {
            var ticket = await _db.Tickets.FindAsync(idTicket);
            if (ticket == null) return NotFound();

            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);
            var statut = await _db.StatutsTicket.FirstAsync(s => s.NomStatut == "Résolu");

            ticket.IdStatut = statut.IdStatut;
            ticket.DateResolution = DateTime.Now;
            var remarqueResolution = string.IsNullOrWhiteSpace(remarque) ? null : remarque.Trim();

            _db.HistoriqueTickets.Add(new HistoriqueTicket
            {
                IdTicket = ticket.IdTicket,
                IdUtilisateur = idUser,
                Action = "Clôture",
                NouvelleValeur = remarqueResolution
            });

            await _db.SaveChangesAsync();
            await _notif.TicketResoluAsync(ticket);
            TempData["Success"] = "Ticket clôturé avec succès.";
            return RedirectToAction("Index");
        }

        // ── Helpers ──────────────────────────────────────────────
        private async Task ChargerViewBags(Ticket ticket)
        {
            ViewBag.Urgences = new SelectList(
                await _db.NiveauxUrgence.OrderBy(u => u.Ordre).ToListAsync(),
                "IdUrgence", "NomUrgence", ticket.IdUrgence);

            ViewBag.Impacts = new SelectList(
                await _db.NiveauxImpact.OrderBy(i => i.Ordre).ToListAsync(),
                "IdImpact", "NomImpact", ticket.IdImpact);

            ViewBag.Domaines = new SelectList(
                await _db.Domaines.Where(c => c.IsActive).ToListAsync(),
                "IdDomaine", "NomDomaine", ticket.IdDomaine);

            ViewBag.Categories = new SelectList(
                ticket.IdDomaine.HasValue 
                    ? await _db.Categories.Where(c => c.IdDomaine == ticket.IdDomaine.Value && c.IsActive).ToListAsync()
                    : new List<Categorie>(),
                "IdCategorie", "NomCategorie", ticket.IdCategorie);

            ViewBag.SousCategories = new SelectList(
                ticket.IdCategorie.HasValue 
                    ? await _db.SousCategories.Where(s => s.IdCategorie == ticket.IdCategorie.Value && s.IsActive).ToListAsync()
                    : new List<SousCategorie>(),
                "IdSousCategorie", "NomSousCategorie", ticket.IdSousCategorie);

            ViewBag.Natures = new SelectList(
                await _db.Natures.ToListAsync(),
                "IdNature", "NomNature", ticket.IdNature);

            ViewBag.Techniciens = new SelectList(
                await _db.Utilisateurs
                    .Include(u => u.Role)
                    .Where(u => (u.Role!.NomRole == "Technicien" || u.Role!.NomRole == "Helpdesk")  && u.IsActive)
                    .ToListAsync(),
                "IdUtilisateur", "NomComplet");
        }

        private static string CalculerPriorite(int ordreUrgence, int ordreImpact)
        {
            var score = ordreUrgence + ordreImpact;
            return score switch
            {
                <= 3 => "P1",
                <= 5 => "P2",
                <= 7 => "P3",
                _ => "P4"
            };
        }

        // ── Endpoints AJAX pour les listes déroulantes dynamiques ──
        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetCategoriesByDomaine(int idDomaine)
        {
            var categories = await _db.Categories
                .Where(c => c.IdDomaine == idDomaine && c.IsActive)
                .Select(c => new { value = c.IdCategorie, text = c.NomCategorie })
                .ToListAsync();
            return Json(categories);
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetSousCategoriesByCategorie(int idCategorie)
        {
            var sousCategories = await _db.SousCategories
                .Where(s => s.IdCategorie == idCategorie && s.IsActive)
                .Select(s => new { value = s.IdSousCategorie, text = s.NomSousCategorie })
                .ToListAsync();
            return Json(sousCategories);
        }

        // ── Performance ──────────────────────────────────────────────
        public async Task<IActionResult> Performance(int? annee)
        {
            ViewData["Title"] = "Performance";

            annee ??= DateTime.Now.Year;
            var anneeEnCours = annee.Value;

            var tickets = await _db.Tickets
                .Include(t => t.Statut)
                .Include(t => t.Domaine)
                .Include(t => t.Interventions)
                    .ThenInclude(i => i.Technicien)
                .Where(t => t.DateOuverture.Year == anneeEnCours)
                .ToListAsync();

            var ticketsFermes = tickets
                .Where(t => t.Statut?.NomStatut == "Fermé" ||
                            t.Statut?.NomStatut == "Résolu").ToList();

            // ── KPIs ─────────────────────────────────────────────────
            // MTTR
            var mttr = ticketsFermes
                .Where(t => t.DateResolution.HasValue)
                .Select(t => (t.DateResolution!.Value - t.DateOuverture).TotalHours)
                .DefaultIfEmpty(0)
                .Average();

            // CSAT
            var csat = tickets
                .Where(t => t.NoteEmployee.HasValue)
                .Select(t => (double)t.NoteEmployee!.Value)
                .DefaultIfEmpty(0)
                .Average();

            // SLA respecté
            var ticketsAvecSLA = tickets
                .Where(t => t.DelaiResolutionCible.HasValue).ToList();
            var slaRespece = ticketsAvecSLA.Count == 0 ? 0 :
                ticketsAvecSLA.Count(t =>
                    t.DateResolution.HasValue &&
                    t.DateResolution <= t.DelaiResolutionCible) * 100.0
                / ticketsAvecSLA.Count;

            // CFR — tickets résolus avec 1 seule intervention
            var cfr = ticketsFermes.Count == 0 ? 0 :
                ticketsFermes.Count(t => t.Interventions.Count == 1) * 100.0
                / ticketsFermes.Count;

            // SLA en dépassement
            var enDepassement = tickets
                .Where(t => t.DelaiResolutionCible.HasValue
                         && !t.DateResolution.HasValue
                         && DateTime.Now > t.DelaiResolutionCible)
                .ToList();

            // ── Graphiques ───────────────────────────────────────────
            // Par statut
            var parStatut = tickets
                .GroupBy(t => t.Statut?.NomStatut ?? "Inconnu")
                .Select(g => new { Statut = g.Key, Count = g.Count() })
                .ToList();

            // Par domaine
            var parDomaine = tickets
                .GroupBy(t => t.Domaine?.NomDomaine ?? "Sans domaine")
                .Select(g => new { Domaine = g.Key, Count = g.Count() })
                .ToList();

            // Par priorité
            var parPriorite = tickets
                .GroupBy(t => t.PrioriteCalculee ?? "Non qualifié")
                .Select(g => new { Priorite = g.Key, Count = g.Count() })
                .OrderBy(g => g.Priorite)
                .ToList();

            // ── Classement techniciens ───────────────────────────────
            
            var techniciens = await _db.Utilisateurs
            .Include(u => u.Role)
            .Include(u => u.Interventions)
                .ThenInclude(i => i.Statut)  // ← manquait !
            .Include(u => u.Interventions)
                .ThenInclude(i => i.Ticket)
            .Where(u => u.Role!.NomRole == "Technicien" || u.Role!.NomRole == "Helpdesk")
            .Where(u => u.IsActive)
            .ToListAsync();

            var classementTech = techniciens
                .Where(t => t.Interventions.Any(i => i.Ticket != null && i.Ticket.DateOuverture.Year == anneeEnCours))
                .Select(t => new
            {
                Nom = t.NomComplet,
                Specialite = t.Specialite ?? "—",
                TotalInterventions = t.Interventions.Count(i => i.Ticket != null && i.Ticket.DateOuverture.Year == anneeEnCours),
                Terminees = t.Interventions.Count(i => i.Statut?.NomStatut == "Terminée" && i.Ticket != null && i.Ticket.DateOuverture.Year == anneeEnCours),
                NoteMoyenne = t.Interventions
                    .Where(i => i.Ticket != null && i.Ticket.DateOuverture.Year == anneeEnCours
                     && i.Ticket.NoteEmployee.HasValue)
                    .Select(i => (double)i.Ticket!.NoteEmployee!.Value)
                    .DefaultIfEmpty(0).Average()
            }).OrderByDescending(t => t.NoteMoyenne).ToList();

            ViewBag.TotalTickets = tickets.Count;
            ViewBag.MTTR = Math.Round(mttr, 1);
            ViewBag.CSAT = Math.Round(csat, 1);
            ViewBag.SLARespece = Math.Round(slaRespece, 1);
            ViewBag.CFR = Math.Round(cfr, 1);
            ViewBag.EnDepassement = enDepassement;
            ViewBag.ParStatut = parStatut;
            ViewBag.ParDomaine = parDomaine;
            ViewBag.ParPriorite = parPriorite;
            ViewBag.ClassementTech = classementTech;

            ViewBag.Annee = anneeEnCours;
            ViewBag.Annees = await _db.Tickets
                .Select(t => t.DateOuverture.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            return View();
        }
    }
}

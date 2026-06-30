using HelpDesk_Manager.Data;
using HelpDesk_Manager.Models;
using HelpDesk_Manager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HelpDesk_Manager.Controllers
{
    [Authorize(Roles = "Technicien,Helpdesk")]
    public class TechnicienController : Controller
    {
        // Remplacez le constructeur
        private readonly AppDbContext _db;
        private readonly NotificationService _notif;

        public TechnicienController(AppDbContext db, NotificationService notif)
        {
            _db = db;
            _notif = notif;
        }

        // ── Mes interventions ────────────────────────────────────
        public async Task<IActionResult> MesTickets(string? statut, int? annee, int? idTechnicien)
        {
            ViewData["Title"] = "Mes Interventions";
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);

            int idTechnicienCible;
            if (role == "Helpdesk")
            {
                // Le helpdesk peut choisir un technicien ; sinon on prend le premier par défaut
                ViewBag.Techniciens = await _db.Utilisateurs
                    .Include(u => u.Role)
                    .Where(u => u.Role!.NomRole == "Technicien" && u.IsActive)
                    .OrderBy(u => u.Nom)
                    .ToListAsync();

                idTechnicienCible = idTechnicien ?? idUser;
            }
            else
            {
                // Un technicien ne voit que ses propres interventions
                idTechnicienCible = idUser;
            }

            ViewBag.IdUserConnecte = idUser;

            var anneeEnCours = annee ?? DateTime.Now.Year;

            var query = _db.Interventions
                .Include(i => i.Ticket)
                    .ThenInclude(t => t!.Statut)
                .Include(i => i.Ticket)
                    .ThenInclude(t => t!.Demandeur)
                .Include(i => i.Ticket)
                    .ThenInclude(t => t!.Urgence)
                .Include(i => i.Statut)
                .Where(i => i.IdTechnicien == idTechnicienCible && i.Ticket!.DateOuverture.Year == anneeEnCours)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statut))
                query = query.Where(i => i.Statut!.NomStatut == statut);

            var interventions = await query
                .OrderByDescending(i => i.DateAction)
                .ToListAsync();

            ViewBag.Statut = statut;
            ViewBag.Statuts = await _db.StatutsIntervention.ToListAsync();
            ViewBag.Annee = anneeEnCours;
            ViewBag.IdTechnicien = idTechnicienCible;
            ViewBag.Role = role;
            ViewBag.Annees = await _db.Interventions
                .Where(i => i.IdTechnicien == idTechnicienCible)
                .Select(i => i.Ticket!.DateOuverture.Year)
                .Distinct()
                .OrderByDescending(a => a)
                .ToListAsync();

            return View(interventions);
        }

        // ── Détail intervention ──────────────────────────────────
        public async Task<IActionResult> Details(int id)
        {
            ViewData["Title"] = $"Intervention #{id}";
            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

            var query = _db.Interventions
                .Include(i => i.Ticket)
                    .ThenInclude(t => t!.Statut)
                .Include(i => i.Ticket)
                    .ThenInclude(t => t!.Demandeur)
                .Include(i => i.Ticket)
                    .ThenInclude(t => t!.Domaine)
                .Include(i => i.Ticket)
                    .ThenInclude(t => t!.Categorie)
                .Include(i => i.Ticket)
                    .ThenInclude(t => t!.SousCategorie)
                .Include(i => i.Ticket)
                    .ThenInclude(t => t!.Urgence)
                .Include(i => i.Ticket)
                    .ThenInclude(t => t!.PiecesJointes)
                .Include(i => i.Ticket)
                    .ThenInclude(t => t!.Interventions)
                        .ThenInclude(inv => inv.Technicien)
                .Include(i => i.Ticket)
                    .ThenInclude(t => t!.Interventions)
                        .ThenInclude(inv => inv.Statut)
                .Include(i => i.Ticket)
                    .ThenInclude(t => t!.Historique)
                        .ThenInclude(h => h.Utilisateur)
                .Include(i => i.Statut)
                .Where(i => i.IdIntervention == id);

            // Un technicien ne peut voir que ses propres interventions ; le helpdesk peut voir celles de tous
            if (role != "Helpdesk")
                query = query.Where(i => i.IdTechnicien == idUser);

            var intervention = await query.FirstOrDefaultAsync();

            if (intervention == null) return NotFound();

            ViewBag.Statuts = await _db.StatutsIntervention.ToListAsync();
            return View(intervention);
        }

        // ── Mettre à jour statut intervention POST ───────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MettreAJourStatut(int idIntervention, int idStatut, string? motifStatut)
        {
            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

            var queryIntervention = _db.Interventions
                .Include(i => i.Statut)
                .Where(i => i.IdIntervention == idIntervention);

            if (role != "Helpdesk")
                queryIntervention = queryIntervention.Where(i => i.IdTechnicien == idUser);

            var intervention = await queryIntervention.FirstOrDefaultAsync();

            if (intervention == null) return NotFound();
            var ancienStatut = intervention.Statut?.NomStatut;
            var nouveauStatut = await _db.StatutsIntervention.FindAsync(idStatut);
            var nomNouveauStatut = nouveauStatut?.NomStatut;

            // Validation transitions
            var transitionsAutorisees = new Dictionary<string, List<string>>
            {
                ["Planifiée"] = new() { "En cours", "Terminée", "Annulée" },
                ["En cours"] = new() { "Terminée", "Annulée", "En attente demandeur", "En attente tiers/fournisseur" },
                ["En attente demandeur"] = new() { "En cours", "Terminée" },
                ["En attente tiers/fournisseur"] = new() { "En cours", "Terminée" },
            };

            if (ancienStatut != null && transitionsAutorisees.ContainsKey(ancienStatut))
            {
                if (!transitionsAutorisees[ancienStatut].Contains(nomNouveauStatut ?? ""))
                {
                    TempData["Erreur"] = $"Transition de '{ancienStatut}' vers '{nomNouveauStatut}' non autorisée.";
                    return RedirectToAction("Details", new { id = idIntervention });
                }
            }

            // Motif obligatoire pour certains statuts
            var statutsAvecMotif = new[] { "Annulée", "En attente demandeur", "En attente tiers/fournisseur" };
            if (statutsAvecMotif.Contains(nomNouveauStatut) && string.IsNullOrWhiteSpace(motifStatut))
            {
                TempData["Erreur"] = "Un motif est obligatoire pour ce statut.";
                return RedirectToAction("Details", new { id = idIntervention });
            }

            intervention.MotifStatut = statutsAvecMotif.Contains(nomNouveauStatut) ? motifStatut : null;


            intervention.IdStatut = idStatut;

            if (nouveauStatut?.NomStatut == "En cours" && intervention.DateDebut == null)
                intervention.DateDebut = DateTime.Now;

            var maintenant = DateTime.Now;

            if (nouveauStatut?.NomStatut == "Terminée")
                intervention.DateFin = maintenant;

            // Historique ticket
            _db.HistoriqueTickets.Add(new HistoriqueTicket
            {
                IdTicket = intervention.IdTicket,
                IdUtilisateur = idUser,
                Action = "Mise à jour intervention",
                AncienneValeur = ancienStatut,
                NouvelleValeur = nouveauStatut?.NomStatut
            });

            await _db.SaveChangesAsync();

            Ticket? ticketResoluAutomatiquement = null;
            if (nouveauStatut?.NomStatut == "Terminée")
            {
                ticketResoluAutomatiquement = await ResoudreTicketSiToutesInterventionsTermineesAsync(
                    intervention.IdTicket,
                    idUser,
                    maintenant);
            }

            if (nouveauStatut?.NomStatut == "Terminée")
            {
                var technicien = await _db.Utilisateurs.FindAsync(idUser);
                await _notif.InterventionTermineeAsync(intervention, technicien!.NomComplet);
            }
            if (ticketResoluAutomatiquement != null)
            {
                await _notif.TicketResoluAsync(ticketResoluAutomatiquement);
            }
            TempData["Success"] = "Statut mis à jour.";
            return RedirectToAction("Details", new { id = idIntervention });
        }

        private async Task<Ticket?> ResoudreTicketSiToutesInterventionsTermineesAsync(
    int idTicket,
    int idUser,
    DateTime dateResolution)
        {
            var statutsTerminaux = await _db.StatutsIntervention
                .AsNoTracking()
                .Where(s => s.NomStatut == "Terminée" || s.NomStatut == "Annulée")
                .Select(s => s.IdStatut)
                .ToListAsync();

            var verification = await _db.Tickets
                .AsNoTracking()
                .Where(t => t.IdTicket == idTicket)
                .Select(t => new
                {
                    t.IdTicket,
                    StatutTicket = t.Statut!.NomStatut,
                    TotalInterventions = t.Interventions.Count,
                    InterventionsNonTerminees = t.Interventions.Count(i => !statutsTerminaux.Contains(i.IdStatut))
                })
                .FirstOrDefaultAsync();

            if (verification == null
                || verification.TotalInterventions == 0
                || verification.InterventionsNonTerminees > 0
                || verification.StatutTicket == "Résolu"
                || verification.StatutTicket == "Fermé"
                || verification.StatutTicket == "Annulé"
                || verification.StatutTicket == "Hors périmètre")
            {
                return null;
            }

            var statutResolu = await _db.StatutsTicket.FirstAsync(s => s.NomStatut == "Résolu");
            var ticket = await _db.Tickets.FindAsync(idTicket);
            if (ticket == null) return null;

            ticket.IdStatut = statutResolu.IdStatut;
            ticket.DateResolution = dateResolution;

            _db.HistoriqueTickets.Add(new HistoriqueTicket
            {
                IdTicket = ticket.IdTicket,
                IdUtilisateur = idUser,
                Action = "Résolution automatique",
                NouvelleValeur = "Toutes les interventions sont terminées ou annulées."
            });

            await _db.SaveChangesAsync();
            return ticket;
        }

        // ── Ajouter une note POST ────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AjouterNote(int idIntervention, string note)
        {
            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);
            var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";

            var queryIntervention = _db.Interventions
                .Where(i => i.IdIntervention == idIntervention);

            if (role != "Helpdesk")
                queryIntervention = queryIntervention.Where(i => i.IdTechnicien == idUser);

            var intervention = await queryIntervention.FirstOrDefaultAsync();

            if (intervention == null) return NotFound();

            // Ajouter la note à l'existante
            intervention.Notes = string.IsNullOrEmpty(intervention.Notes)
                ? $"[{DateTime.Now:dd/MM/yyyy HH:mm}] {note}"
                : $"{intervention.Notes}\n[{DateTime.Now:dd/MM/yyyy HH:mm}] {note}";

            // Historique ticket
            _db.HistoriqueTickets.Add(new HistoriqueTicket
            {
                IdTicket = intervention.IdTicket,
                IdUtilisateur = idUser,
                Action = "Note technicien",
                NouvelleValeur = note
            });

            await _db.SaveChangesAsync();
            var ticket = await _db.Tickets.FindAsync(intervention.IdTicket);
            var technicien = await _db.Utilisateurs.FindAsync(idUser);
            if (ticket != null && technicien != null)
                await _notif.NoteTechnicienAsync(ticket, technicien.NomComplet, note);
            TempData["Success"] = "Note ajoutée.";
            return RedirectToAction("Details", new { id = idIntervention });
        }
    }
}

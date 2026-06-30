using HelpDesk_Manager.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace HelpDesk_Manager.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        public HomeController(AppDbContext db) { _db = db; }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Dashboard";

            // Dashboard Employé
            if (User.IsInRole("Employe"))
            {
                var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);

                var anneeEnCours = DateTime.Now.Year;
                var mesTickets = await _db.Tickets
                    .Include(t => t.Statut)
                    .Include(t => t.Domaine)
                    .Where(t => t.IdDemandeur == idUser && t.DateOuverture.Year == anneeEnCours)
                    .OrderByDescending(t => t.DateOuverture)
                    .ToListAsync();

                ViewBag.Total = mesTickets.Count;
                ViewBag.EnCours = mesTickets.Count(t => t.Statut?.NomStatut == "En cours");
                ViewBag.Resolus = mesTickets.Count(t => t.Statut?.NomStatut == "Résolu"
                                                       || t.Statut?.NomStatut == "Fermé");
                ViewBag.Rejetes = mesTickets.Count(t => t.Statut?.NomStatut == "Annulé" 
                                                        || t.Statut?.NomStatut=="Hors périmètre");
                ViewBag.ANoter = mesTickets.Count(t => t.Statut?.NomStatut == "Résolu"
                                                       && !t.NoteEmployee.HasValue);
                ViewBag.Recents = mesTickets.Take(5).ToList();

                // Répartition par statut
                ViewBag.ParStatut = mesTickets
                    .GroupBy(t => t.Statut?.NomStatut ?? "Inconnu")
                    .Select(g => new { Statut = g.Key, Count = g.Count() })
                    .ToList();

                return View("IndexEmploye");
            }
            // Dashboard Technicien
            if (User.IsInRole("Technicien"))
            {
                var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);

                var anneeEnCours = DateTime.Now.Year;
                var mesInterventions = await _db.Interventions
                    .Include(i => i.Statut)
                    .Include(i => i.Ticket)
                        .ThenInclude(t => t!.Statut)
                    .Include(i => i.Ticket)
                        .ThenInclude(t => t!.Domaine)
                    .Include(i => i.Ticket)
                        .ThenInclude(t => t!.Demandeur)
                    .Where(i => i.IdTechnicien == idUser && i.DateAction.Year == anneeEnCours)
                    .OrderByDescending(i => i.DateAction)
                    .ToListAsync();

                ViewBag.Total = mesInterventions.Count;
                ViewBag.Planifiees = mesInterventions.Count(i => i.Statut?.NomStatut == "Planifiée");
                ViewBag.EnCours = mesInterventions.Count(i => i.Statut?.NomStatut == "En cours");
                ViewBag.Terminees = mesInterventions.Count(i => i.Statut?.NomStatut == "Terminée");
                ViewBag.EnAttente = mesInterventions.Count(i => i.Statut?.NomStatut == "En attente demandeur"
                                                              || i.Statut?.NomStatut == "En attente tiers/fournisseur");

                // Interventions planifiées pour aujourd'hui
                ViewBag.AujourdHui = mesInterventions
                    .Where(i => i.DatePlanifiee.HasValue
                             && i.DatePlanifiee.Value.Date == DateTime.Today
                             && i.Statut?.NomStatut == "Planifiée")
                    .ToList();

                ViewBag.Recentes = mesInterventions.Take(5).ToList();

                ViewBag.ParStatut = mesInterventions
                    .GroupBy(i => i.Statut?.NomStatut ?? "Inconnu")
                    .Select(g => new { Statut = g.Key, Count = g.Count() })
                    .ToList();

                return View("IndexTechnicien");
            }

            // Dashboard Admin / Helpdesk / Technicien
            var anneeEnCoursA = DateTime.Now.Year;
            var baseQuery = _db.Tickets.Where(t => t.DateOuverture.Year == anneeEnCoursA);

            ViewBag.TotalTickets = await baseQuery.CountAsync();
            ViewBag.NouveauxTickets = await baseQuery
                .Where(t => t.Statut!.NomStatut == "Nouveau").CountAsync();
            ViewBag.EnCours = await baseQuery
                .Where(t => t.Statut!.NomStatut == "En cours").CountAsync();
            ViewBag.Resolus = await baseQuery
                .Where(t => t.Statut!.NomStatut == "Résolu"
                         || t.Statut!.NomStatut == "Fermé").CountAsync();

            ViewBag.NouveauxTicketsList = await _db.Tickets
                .Include(t => t.Statut)
                .Include(t => t.Domaine)
                .Include(t => t.Demandeur)
                .Where(t => t.Statut!.NomStatut == "Nouveau" && t.DateOuverture.Year == anneeEnCoursA)
                .OrderByDescending(t => t.DateOuverture)
                .Take(10)
                .ToListAsync();

            return View();
        }

        public IActionResult Erreur() => View();
    }
}
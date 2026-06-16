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

                var mesTickets = await _db.Tickets
                    .Include(t => t.Statut)
                    .Include(t => t.Domaine)
                    .Where(t => t.IdDemandeur == idUser)
                    .OrderByDescending(t => t.DateOuverture)
                    .ToListAsync();

                ViewBag.Total = mesTickets.Count;
                ViewBag.EnCours = mesTickets.Count(t => t.Statut?.NomStatut == "En cours");
                ViewBag.Resolus = mesTickets.Count(t => t.Statut?.NomStatut == "Résolu"
                                                       || t.Statut?.NomStatut == "Fermé");
                ViewBag.Rejetes = mesTickets.Count(t => t.Statut?.NomStatut == "Annulé");
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

            // Dashboard Admin / Helpdesk / Technicien
            ViewBag.TotalTickets = await _db.Tickets.CountAsync();
            ViewBag.NouveauxTickets = await _db.Tickets
                .Where(t => t.Statut!.NomStatut == "Nouveau").CountAsync();
            ViewBag.EnCours = await _db.Tickets
                .Where(t => t.Statut!.NomStatut == "En cours").CountAsync();
            ViewBag.Resolus = await _db.Tickets
                .Where(t => t.Statut!.NomStatut == "Résolu"
                         || t.Statut!.NomStatut == "Fermé").CountAsync();

            return View();
        }

        public IActionResult Erreur() => View();
    }
}
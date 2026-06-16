using HelpDesk_Manager.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk_Manager.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly AppDbContext _db;
        public NotificationsController(AppDbContext db) { _db = db; }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Notifications";
            var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);

            // Marquer toutes comme lues
            var nonLues = await _db.Notifications
                .Where(n => n.IdUtilisateur == idUser && !n.IsLue)
                .ToListAsync();

            foreach (var n in nonLues) n.IsLue = true;
            await _db.SaveChangesAsync();

            var notifications = await _db.Notifications
                .Where(n => n.IdUtilisateur == idUser)
                .OrderByDescending(n => n.DateEnvoi)
                .Take(50)
                .ToListAsync();

            return View(notifications);
        }
        public async Task<IActionResult> OuvrirTicket(int idTicket)
        {
            if (User.IsInRole("Technicien"))
            {
                var idUser = int.Parse(User.FindFirst("IdUtilisateur")!.Value);
                var intervention = await _db.Interventions
                    .FirstOrDefaultAsync(i => i.IdTicket == idTicket && i.IdTechnicien == idUser);
                if (intervention != null)
                    return RedirectToAction("Details", "Technicien",
                        new { id = intervention.IdIntervention });
            }

            if (User.IsInRole("Employe"))
                return RedirectToAction("Details", "Tickets", new { id = idTicket });

            return RedirectToAction("Details", "Helpdesk", new { id = idTicket });
        }
    }
}
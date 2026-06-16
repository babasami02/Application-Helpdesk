using HelpDesk_Manager.Data;
using HelpDesk_Manager.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk_Manager.Services
{
    public class NotificationService
    {
        private readonly AppDbContext _db;

        public NotificationService(AppDbContext db)
        {
            _db = db;
        }

        // ── Envoi simple ─────────────────────────────────────────
        public async Task EnvoyerAsync(int idUtilisateur, string message, int? idTicket = null)
        {
            _db.Notifications.Add(new Notification
            {
                IdUtilisateur = idUtilisateur,
                Message = message,
                IdTicket = idTicket,
                DateEnvoi = DateTime.Now,
                IsLue = false
            });
            await _db.SaveChangesAsync();
        }

        // ── Envoyer à tous les Helpdesk ──────────────────────────
        public async Task EnvoyerAuxHelpdeskAsync(string message, int? idTicket = null)
        {
            var helpdesks = await _db.Utilisateurs
                .Include(u => u.Role)
                .Where(u => u.Role!.NomRole == "Helpdesk" && u.IsActive)
                .ToListAsync();

            foreach (var h in helpdesks)
                await EnvoyerAsync(h.IdUtilisateur, message, idTicket);
        }

        // ── 1. Ticket créé → Helpdesk ────────────────────────────
        public async Task TicketCreeAsync(Ticket ticket, string nomDemandeur)
        {
            await EnvoyerAuxHelpdeskAsync(
                $"📩 Nouveau ticket #{ticket.IdTicket} soumis par {nomDemandeur} : {ticket.Titre}",
                ticket.IdTicket);
        }

        // ── 2. Ticket qualifié → Employé ─────────────────────────
        public async Task TicketQualifieAsync(Ticket ticket)
        {
            await EnvoyerAsync(
                ticket.IdDemandeur,
                $"✅ Votre ticket #{ticket.IdTicket} a été pris en charge par le Helpdesk.",
                ticket.IdTicket);
        }

        // ── 3. Ticket annulé → Employé ─────────────────────────────
        public async Task TicketAnnuleAsync(Ticket ticket, string motif)
        {
            await EnvoyerAsync(
                ticket.IdDemandeur,
                $"❌ Votre ticket #{ticket.IdTicket} a été annulé. Motif : {motif}",
                ticket.IdTicket);
        }

        // ── 4. Ticket hors périmètre → Employé ─────────────────────
        public async Task TicketHorsPerimetreAsync(Ticket ticket, string? motif)
        {
            var detailMotif = string.IsNullOrWhiteSpace(motif) ? "" : $" Motif : {motif}";

            await EnvoyerAsync(
                ticket.IdDemandeur,
                $"🚫 Votre ticket #{ticket.IdTicket} est hors périmètre et ne peut être traité.{detailMotif}",
                ticket.IdTicket);
        }

        // ── 4. Intervention assignée → Technicien ────────────────
        public async Task InterventionAssigneeAsync(Intervention intervention, string titreTicket)
        {
            await EnvoyerAsync(
                intervention.IdTechnicien,
                $"🔧 Nouvelle intervention assignée sur le ticket #{intervention.IdTicket} : {titreTicket}",
                intervention.IdTicket);
        }

        // ── 5. Intervention terminée → Helpdesk ──────────────────
        public async Task InterventionTermineeAsync(Intervention intervention, string nomTechnicien)
        {
            await EnvoyerAuxHelpdeskAsync(
                $"✅ L'intervention #{intervention.IdIntervention} du ticket #{intervention.IdTicket} a été terminée par {nomTechnicien}.",
                intervention.IdTicket);
        }

        // ── 6. Ticket résolu → Employé ───────────────────────────
        public async Task TicketResoluAsync(Ticket ticket)
        {
            await EnvoyerAsync(
                ticket.IdDemandeur,
                $"🎉 Votre ticket #{ticket.IdTicket} a été résolu ! Pensez à noter la prestation.",
                ticket.IdTicket);
        }
    }
}

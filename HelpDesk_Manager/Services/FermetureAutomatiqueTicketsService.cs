using HelpDesk_Manager.Data;
using HelpDesk_Manager.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk_Manager.Services
{
    public class FermetureAutomatiqueTicketsService : BackgroundService
    {
        private static readonly TimeSpan DelaiAvantFermeture = TimeSpan.FromHours(48);
        private static readonly TimeSpan FrequenceVerification = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FermetureAutomatiqueTicketsService> _logger;

        public FermetureAutomatiqueTicketsService(
            IServiceScopeFactory scopeFactory,
            ILogger<FermetureAutomatiqueTicketsService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await FermerTicketsExpiresAsync(stoppingToken);

            using var timer = new PeriodicTimer(FrequenceVerification);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await FermerTicketsExpiresAsync(stoppingToken);
            }
        }

        private async Task FermerTicketsExpiresAsync(CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var statutResolu = await db.StatutsTicket
                    .FirstOrDefaultAsync(s => s.NomStatut == "Résolu", stoppingToken);
                var statutFerme = await db.StatutsTicket
                    .FirstOrDefaultAsync(s => s.NomStatut == "Fermé", stoppingToken);

                if (statutResolu == null || statutFerme == null)
                {
                    _logger.LogWarning("Fermeture automatique impossible : statuts Résolu/Fermé introuvables.");
                    return;
                }

                var maintenant = DateTime.Now;
                var dateLimite = maintenant.Subtract(DelaiAvantFermeture);

                var tickets = await db.Tickets
                    .Where(t => t.IdStatut == statutResolu.IdStatut
                             && t.DateResolution.HasValue
                             && t.DateResolution <= dateLimite
                             && !t.NoteEmployee.HasValue)
                    .ToListAsync(stoppingToken);

                if (tickets.Count == 0)
                {
                    return;
                }

                foreach (var ticket in tickets)
                {
                    ticket.IdStatut = statutFerme.IdStatut;
                    ticket.DateFermeture = maintenant;

                    db.HistoriqueTickets.Add(new HistoriqueTicket
                    {
                        IdTicket = ticket.IdTicket,
                        IdUtilisateur = ticket.IdHelpdesk ?? ticket.IdDemandeur,
                        Action = "Fermeture automatique",
                        AncienneValeur = "Résolu",
                        NouvelleValeur = "Fermé",
                        Commentaire = "Ticket fermé automatiquement après 48 heures sans note employé."
                    });
                }

                await db.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("{Count} ticket(s) ferme(s) automatiquement.", tickets.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erreur pendant la fermeture automatique des tickets résolus.");
            }
        }
    }
}

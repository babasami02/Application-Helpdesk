using HelpDesk_Manager.Models;
using Microsoft.EntityFrameworkCore;

namespace HelpDesk_Manager.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // ── DbSets ──────────────────────────────────────────────
        public DbSet<Role>                Roles                { get; set; }
        public DbSet<Utilisateur>         Utilisateurs         { get; set; }
        public DbSet<Domaine>             Domaines             { get; set; }
        public DbSet<Categorie> Categories { get; set; }
        public DbSet<SousCategorie> SousCategories { get; set; }
        public DbSet<Nature>              Natures              { get; set; }
        public DbSet<StatutTicket>        StatutsTicket        { get; set; }
        public DbSet<StatutIntervention>  StatutsIntervention  { get; set; }
        public DbSet<NiveauUrgence>       NiveauxUrgence       { get; set; }
        public DbSet<NiveauImpact>        NiveauxImpact        { get; set; }
        public DbSet<Ticket>              Tickets              { get; set; }
        public DbSet<PieceJointe>         PiecesJointes        { get; set; }
        public DbSet<Intervention>        Interventions        { get; set; }
        public DbSet<HistoriqueTicket>    HistoriqueTickets    { get; set; }
        public DbSet<Notification>        Notifications        { get; set; }
        public DbSet<ConfigurationSLA> ConfigurationsSLA { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ── Ticket : deux FK vers Utilisateurs ──────────────
            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.Demandeur)
                .WithMany(u => u.TicketsDemandeur)
                .HasForeignKey(t => t.IdDemandeur)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Ticket>()
                .HasOne(t => t.AgentHelpdesk)
                .WithMany(u => u.TicketsHelpdesk)
                .HasForeignKey(t => t.IdHelpdesk)
                .OnDelete(DeleteBehavior.Restrict);

            // ── Intervention ─────────────────────────────────────
            modelBuilder.Entity<Intervention>()
                .HasOne(i => i.Technicien)
                .WithMany(u => u.Interventions)
                .HasForeignKey(i => i.IdTechnicien)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ConfigurationSLA>().ToTable("ConfigurationSLA");

            // ── Noms des tables ──────────────────────────────────
            modelBuilder.Entity<Role>().ToTable("Roles");
            modelBuilder.Entity<Utilisateur>().ToTable("Utilisateurs");
            modelBuilder.Entity<Domaine>().ToTable("Domaines");
            modelBuilder.Entity<Categorie>().ToTable("Categories");
            modelBuilder.Entity<SousCategorie>().ToTable("SousCategories");
            modelBuilder.Entity<Nature>().ToTable("Natures");
            modelBuilder.Entity<StatutTicket>().ToTable("StatutsTicket");
            modelBuilder.Entity<StatutIntervention>().ToTable("StatutsIntervention");
            modelBuilder.Entity<NiveauUrgence>().ToTable("NiveauxUrgence");
            modelBuilder.Entity<NiveauImpact>().ToTable("NiveauxImpact");
            modelBuilder.Entity<Ticket>().ToTable("Tickets");
            modelBuilder.Entity<PieceJointe>().ToTable("PiecesJointes");
            modelBuilder.Entity<Intervention>().ToTable("Interventions");
            modelBuilder.Entity<HistoriqueTicket>().ToTable("HistoriqueTickets");
            modelBuilder.Entity<Notification>().ToTable("Notifications");

            // ── Index unique ─────────────────────────────────────
            modelBuilder.Entity<Utilisateur>()
                .HasIndex(u => u.Email)
                .IsUnique();
        }
    }
}

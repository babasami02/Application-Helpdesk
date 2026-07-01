using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HelpDesk_Manager.Models
{
    // ── RÔLE ────────────────────────────────────────────────────
    public class Role
    {
        [Key] public int IdRole { get; set; }
        [Required, MaxLength(50)] public string NomRole { get; set; } = "";
        [MaxLength(200)] public string? Description { get; set; }

        public ICollection<Utilisateur> Utilisateurs { get; set; } = new List<Utilisateur>();
    }

    // ── UTILISATEUR ─────────────────────────────────────────────
    public class Utilisateur
    {
        [Key] public int IdUtilisateur { get; set; }
        [Required, MaxLength(100)] public string Nom { get; set; } = "";
        [Required, MaxLength(100)] public string Prenom { get; set; } = "";
        [Required, MaxLength(200), EmailAddress] public string Email { get; set; } = "";
        [Required, MaxLength(256)] public string MotDePasse { get; set; } = "";

        public int IdRole { get; set; }
        [ForeignKey(nameof(IdRole))] public Role? Role { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime DateCreation { get; set; } = DateTime.Now;
        public DateTime? DernierAcces { get; set; }

        // Champs Employé
        [MaxLength(100)] public string? Direction { get; set; }
        [MaxLength(100)] public string? Departement { get; set; }

        // Champs Technicien
        [MaxLength(50)]  public string? Niveau { get; set; }      // N1, N2, N3
        [MaxLength(150)] public string? Specialite { get; set; }
        [MaxLength(20)]  public string? Telephone { get; set; }

        // Navigation
        public ICollection<Ticket> TicketsDemandeur { get; set; } = new List<Ticket>();
        public ICollection<Ticket> TicketsHelpdesk  { get; set; } = new List<Ticket>();
        public ICollection<Intervention> Interventions { get; set; } = new List<Intervention>();
        public ICollection<Notification> Notifications { get; set; } = new List<Notification>();

        // Propriétés calculées
        [NotMapped] public string NomComplet => $"{Prenom} {Nom}";
    }

    // ── DOMAINE ──────────────────────────────────────────────────
    public class Domaine
    {
        [Key] public int IdDomaine { get; set; }
        [Required, MaxLength(100)] public string NomDomaine { get; set; } = "";
        [MaxLength(300)] public string? Description { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
        public ICollection<Categorie> Categories { get; set; } = new List<Categorie>();

    }

    //------------ Categorie -----------------------
    public class Categorie
    {
        [Key] public int IdCategorie { get; set; }
        [Required, MaxLength(100)] public string NomCategorie { get; set; } = "";
        public bool IsActive { get; set; } = true;

        public int IdDomaine { get; set; }
        [ForeignKey(nameof(IdDomaine))] public Domaine? Domaine { get; set; }

        public ICollection<SousCategorie> SousCategories { get; set; } = new List<SousCategorie>();
        public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }

    //------ Sous-Categorie -------------------------------
    public class SousCategorie
    {
        [Key] public int IdSousCategorie { get; set; }
        [Required, MaxLength(100)] public string NomSousCategorie { get; set; } = "";
        public bool IsActive { get; set; } = true;

        public int IdCategorie { get; set; }
        [ForeignKey(nameof(IdCategorie))] public Categorie? Categorie { get; set; }

        public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    }

    // ── NATURE ───────────────────────────────────────────────────
    public class Nature
    {
        [Key] public int IdNature { get; set; }
        [Required, MaxLength(100)] public string NomNature { get; set; } = "";
        // Nature est indépendante — pas de FK vers Domaine
    }

    // ── STATUT TICKET ────────────────────────────────────────────
    public class StatutTicket
    {
        [Key] public int IdStatut { get; set; }
        [Required, MaxLength(50)] public string NomStatut { get; set; } = "";
        [MaxLength(20)] public string? Couleur { get; set; }
    }

    // ── STATUT INTERVENTION ──────────────────────────────────────
    public class StatutIntervention
    {
        [Key] public int IdStatut { get; set; }
        [Required, MaxLength(50)] public string NomStatut { get; set; } = "";
    }

    // ── NIVEAU URGENCE ───────────────────────────────────────────
    public class NiveauUrgence
    {
        [Key] public int IdUrgence { get; set; }
        [Required, MaxLength(50)] public string NomUrgence { get; set; } = "";
        public int Ordre { get; set; }
    }

    // ── NIVEAU IMPACT ────────────────────────────────────────────
    public class NiveauImpact
    {
        [Key] public int IdImpact { get; set; }
        [Required, MaxLength(50)] public string NomImpact { get; set; } = "";
        public int Ordre { get; set; }
    }

    // ── TICKET ───────────────────────────────────────────────────
    public class Ticket
    {
        [Key] public int IdTicket { get; set; }
        [Required, MaxLength(200)] public string Titre { get; set; } = "";
        [Required] public string Description { get; set; } = "";

        public DateTime DateOuverture { get; set; } = DateTime.Now;
        public DateTime? DateResolution { get; set; }
        public DateTime? DateFermeture { get; set; }
        public DateTime? DelaiResolutionCible { get; set; }
        public int NumeroAnnuel { get; set; } = 0;

        // FK
        public int IdDemandeur { get; set; }
        [ForeignKey(nameof(IdDemandeur))] public Utilisateur? Demandeur { get; set; }

        public int? IdHelpdesk { get; set; }
        [ForeignKey(nameof(IdHelpdesk))] public Utilisateur? AgentHelpdesk { get; set; }

        public int? IdDomaine { get; set; }
        [ForeignKey(nameof(IdDomaine))] public Domaine? Domaine { get; set; }

        public int? IdNature { get; set; }
        [ForeignKey(nameof(IdNature))] public Nature? Nature { get; set; }
        // TODO (futur) : IdCategorie, IdSousCategorie

        public int IdStatut { get; set; }
        [ForeignKey(nameof(IdStatut))] public StatutTicket? Statut { get; set; }

        public int? IdUrgence { get; set; }
        [ForeignKey(nameof(IdUrgence))] public NiveauUrgence? Urgence { get; set; }

        public int? IdImpact { get; set; }
        [ForeignKey(nameof(IdImpact))] public NiveauImpact? Impact { get; set; }

        public int? IdCategorie { get; set; }
        [ForeignKey(nameof(IdCategorie))] public Categorie? Categorie { get; set; }

        public int? IdSousCategorie { get; set; }
        [ForeignKey(nameof(IdSousCategorie))] public SousCategorie? SousCategorie { get; set; }

        [MaxLength(5)]   public string? PrioriteCalculee { get; set; }
        [MaxLength(500)] public string? MotifRejet { get; set; }

        [Range(1, 5)] public int? NoteEmployee { get; set; }
        [MaxLength(500)] public string? CommentaireNote { get; set; }

        // Navigation
        public ICollection<PieceJointe>      PiecesJointes { get; set; } = new List<PieceJointe>();
        public ICollection<Intervention>     Interventions { get; set; } = new List<Intervention>();
        public ICollection<HistoriqueTicket> Historique    { get; set; } = new List<HistoriqueTicket>();
    }

    // ── PIÈCE JOINTE ─────────────────────────────────────────────
    public class PieceJointe
    {
        [Key] public int IdPieceJointe { get; set; }

        public int IdTicket { get; set; }
        [ForeignKey(nameof(IdTicket))] public Ticket? Ticket { get; set; }

        public int IdUtilisateur { get; set; }
        [ForeignKey(nameof(IdUtilisateur))] public Utilisateur? Utilisateur { get; set; }

        [Required, MaxLength(255)] public string NomFichier { get; set; } = "";
        [Required, MaxLength(500)] public string CheminFichier { get; set; } = "";
        public long Taille { get; set; }
        [MaxLength(20)] public string Format { get; set; } = "";
        public DateTime DateAjout { get; set; } = DateTime.Now;
    }

    // ── INTERVENTION ─────────────────────────────────────────────
    public class Intervention
    {
        [Key] public int IdIntervention { get; set; }

        public int IdTicket { get; set; }
        [ForeignKey(nameof(IdTicket))] public Ticket? Ticket { get; set; }

        public int IdTechnicien { get; set; }
        [ForeignKey(nameof(IdTechnicien))] public Utilisateur? Technicien { get; set; }

        [Required] public string DescriptionAction { get; set; } = "";
        public DateTime DateAction { get; set; } = DateTime.Now;
        public int? TempsPasse { get; set; }  // en minutes

        public int IdStatut { get; set; }
        [ForeignKey(nameof(IdStatut))] public StatutIntervention? Statut { get; set; }

        public string? Notes { get; set; }
        public DateTime? DateDebut { get; set; }
        public DateTime? DateFin { get; set; }
        public int NumeroIntervention { get; set; } = 0;
        public DateTime? DatePlanifiee { get; set; }
        [MaxLength(500)] public string? MotifStatut { get; set; }
    }

    // ── HISTORIQUE ───────────────────────────────────────────────
    public class HistoriqueTicket
    {
        [Key] public int IdHistorique { get; set; }

        public int IdTicket { get; set; }
        [ForeignKey(nameof(IdTicket))] public Ticket? Ticket { get; set; }

        public int IdUtilisateur { get; set; }
        [ForeignKey(nameof(IdUtilisateur))] public Utilisateur? Utilisateur { get; set; }

        public DateTime DateAction { get; set; } = DateTime.Now;
        [Required, MaxLength(100)] public string Action { get; set; } = "";
        [MaxLength(500)] public string? AncienneValeur { get; set; }
        [MaxLength(500)] public string? NouvelleValeur { get; set; }
        [MaxLength(500)] public string? Commentaire { get; set; }
    }

    // ── NOTIFICATION ─────────────────────────────────────────────
    public class Notification
    {
        [Key] public int IdNotification { get; set; }

        public int IdUtilisateur { get; set; }
        [ForeignKey(nameof(IdUtilisateur))] public Utilisateur? Utilisateur { get; set; }

        public int? IdTicket { get; set; }
        [ForeignKey(nameof(IdTicket))] public Ticket? Ticket { get; set; }

        [Required, MaxLength(500)] public string Message { get; set; } = "";
        public DateTime DateEnvoi { get; set; } = DateTime.Now;
        public bool IsLue { get; set; } = false;
    }
    // Configuration SLA
    // APRÈS
    public class ConfigurationSLA
    {
        [Key] public int IdSLA { get; set; }

        [Required, MaxLength(5)] public string Priorite { get; set; } = "";

        public int IdDomaine { get; set; }
        [ForeignKey(nameof(IdDomaine))] public Domaine? Domaine { get; set; }

        public int DelaiReponsHeures { get; set; }
        public int DelaiResolutionHeures { get; set; }
        [MaxLength(200)] public string? Description { get; set; }
    }
}

# 🎫 HelpDesk Manager

> Système de gestion de tickets d'assistance développé dans le cadre d'un projet de fin d'études (PFE) en partenariat avec **NOVEC — Groupe CDG**.

---

## 📋 Présentation

**HelpDesk Manager** est une application web de gestion de tickets d'assistance informatique. Elle permet de centraliser les demandes des employés, de les qualifier, de les affecter à des techniciens et d'en assurer le suivi complet jusqu'à la résolution.

---

## 🛠️ Stack technique

| Composant | Technologie |
|---|---|
| Framework | ASP.NET Core 8 MVC |
| ORM | Entity Framework Core 8 |
| Base de données | SQL Server |
| Authentification | Cookie-based Authentication |
| Hashage mots de passe | BCrypt.Net |
| Frontend | HTML / CSS / Razor (sans framework JS) |

---

## 👥 Rôles utilisateurs

| Rôle | Description |
|---|---|
| **Administrateur** | Gestion des utilisateurs, configuration SLA |
| **Helpdesk** | Qualification, affectation et suivi des tickets |
| **Technicien** | Gestion de ses interventions assignées |
| **Employé** | Création et suivi de ses propres tickets |

---

## ✨ Fonctionnalités principales

### 🎫 Gestion des tickets
- Création de tickets avec titre, description et pièces jointes
- Numérotation annuelle automatique : `N°/ANNÉE` (ex: `42/2026`)
- Qualification : Domaine, Nature, Urgence, Impact → Priorité calculée (P1 à P4)
- Cycle de vie complet : `Nouveau → En cours → Résolu → Fermé`
- Annulation et classement hors périmètre avec motif obligatoire
- Notation de la prestation par l'employé (1 à 5 étoiles)
- Filtrage par année, statut, priorité et domaine

### 🔧 Gestion des interventions
- Numérotation par ticket : `N°Ticket/ANNÉE - N°Intervention` (ex: `42/2026 - 2`)
- Date de planification configurable
- Transitions de statuts contrôlées avec motif obligatoire pour certains statuts :

```
Planifiée → En cours / Terminée / Annulée*
En cours → Terminée / Annulée* / En attente demandeur* / En attente tiers/fournisseur*
En attente demandeur → En cours / Terminée
En attente tiers/fournisseur → En cours / Terminée
(* motif obligatoire)
```
- Résolution automatique du ticket quand toutes les interventions sont terminées

### 📊 Tableaux de bord & KPIs
- Dashboard adapté par rôle
- KPIs : MTTR, CSAT, CFR, conformité SLA
- Graphiques de performance
- Statistiques par domaine

### ⚙️ Configuration SLA
- Matrice Urgence × Impact → Priorité + délais de réponse/résolution
- Configuration par l'administrateur

### 🔔 Notifications
- 6 événements déclencheurs (création, qualification, intervention, résolution...)
- Compteur en temps réel dans la topbar

---

## 🗂️ Structure du projet

```
HelpDesk_Manager/
├── Controllers/
│   ├── AccountController.cs       # Login / Logout
│   ├── AdminController.cs         # Utilisateurs + SLA
│   ├── HelpdeskController.cs      # Tickets + Interventions
│   ├── TechnicienController.cs    # Mes interventions
│   ├── TicketsController.cs       # Tickets employé
│   ├── NotificationsController.cs
│   └── HomeController.cs          # Dashboard
├── Models/
│   └── Entities.cs                # Toutes les entités EF Core
├── Data/
│   └── AppDbContext.cs
├── Services/
│   └── NotificationService.cs
├── Views/
│   ├── Admin/
│   ├── Helpdesk/
│   ├── Technicien/
│   ├── Tickets/
│   ├── Home/
│   └── Shared/
│       ├── _Layout.cshtml
│       └── _Pagination.cshtml
└── wwwroot/
    └── uploads/                   # Pièces jointes
```

---

## 🗃️ Modèle de données principal

```
Utilisateur (Rôle)
    └── Ticket (Domaine, Nature, Urgence, Impact, Statut)
            ├── Intervention (Technicien, Statut, DatePlanifiee)
            ├── PieceJointe
            ├── HistoriqueTicket
            └── Notification

ConfigurationSLA (Urgence × Impact → Priorité, Délais)
```

---

## 🚀 Installation & Démarrage

### Prérequis
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [SQL Server](https://www.microsoft.com/fr-fr/sql-server/sql-server-downloads) (ou SQL Server Express)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) ou VS Code

### Étapes

**1. Cloner le dépôt**
```bash
git clone https://github.com/votre-username/helpdesk-manager.git
cd helpdesk-manager
```

**2. Configurer la connexion SQL Server**

Dans `appsettings.json`, modifiez la chaîne de connexion :
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=VOTRE_SERVEUR;Database=HelpDesk_DB;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

**3. Créer la base de données**

Exécutez le script SQL fourni dans SQL Server Management Studio :
```
01_CreateDatabase_HelpDesk.sql
```

**4. Lancer l'application**
```bash
dotnet run
```

Ou via Visual Studio : `F5`

---

## 🔑 Comptes de test

| Rôle | Email | Mot de passe |
|---|---|---|
| Administrateur | Admin@novec.ma | Admin@123 |
| Helpdesk | Helpdesk@novec.ma | Admin@123 |
| Technicien | Tech@novec.ma | Admin@123 |
| Employé | Employe@novec.ma | Admin@123 |

---

## 📌 Roadmap

- [ ] Export PDF / Excel des tickets
- [ ] Envoi d'emails SMTP
- [ ] Escalade automatique SLA
- [ ] Page profil utilisateur + reset mot de passe
- [ ] API REST pour application mobile
- [ ] Catégories et sous-catégories par domaine

---

## 👨‍💻 Auteur

Développé par **Sami** — Étudiant ingénieur 5IIR à **EMSI (Honoris United Universities)**
Stage de fin d'études chez **NOVEC — Direction IT — Groupe CDG**

---

## 📄 Licence

Projet académique — Usage interne NOVEC. Tous droits réservés.

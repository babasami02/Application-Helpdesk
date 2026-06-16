-- ============================================================
-- HELPDESK MANAGER - Script de création de la base de données
-- Projet: NOVEC - Direction IT
-- Auteur: Baba Sami
-- Date: 2026
-- Base: SQL Server (SQL Server Management Studio)
-- ============================================================

USE master;
GO

IF EXISTS (SELECT name FROM sys.databases WHERE name = N'HelpDesk_Manager')
BEGIN
    ALTER DATABASE HelpDesk_Manager SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE HelpDesk_Manager;
END
GO

CREATE DATABASE HelpDesk_Manager
    COLLATE French_CI_AS;
GO

USE HelpDesk_Manager;
GO

-- ============================================================
-- 1. ENUMS / TABLES DE RÉFÉRENCE
-- ============================================================

-- Rôles utilisateurs
CREATE TABLE Roles (
    IdRole      INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
    NomRole     NVARCHAR(50)  NOT NULL UNIQUE,  -- 'Employe','Helpdesk','Technicien','Administrateur'
    Description NVARCHAR(200) NULL
);

-- Catégories de tickets
CREATE TABLE Categories (
    IdCategorie  INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
    NomCategorie NVARCHAR(100) NOT NULL UNIQUE,  -- 'IT', 'Moyens généraux', 'Développement IT', 'Demande de service IT'
    Description  NVARCHAR(300) NULL,
    IsActive     BIT           NOT NULL DEFAULT 1
);

-- Niveaux d'urgence
CREATE TABLE NiveauxUrgence (
    IdUrgence  INT          NOT NULL IDENTITY(1,1) PRIMARY KEY,
    NomUrgence NVARCHAR(50) NOT NULL UNIQUE,  -- 'Critique', 'Haute', 'Moyenne', 'Basse'
    Ordre      INT          NOT NULL           -- pour le tri (1=Critique, 4=Basse)
);

-- Niveaux d'impact
CREATE TABLE NiveauxImpact (
    IdImpact  INT          NOT NULL IDENTITY(1,1) PRIMARY KEY,
    NomImpact NVARCHAR(50) NOT NULL UNIQUE,  -- 'Critique', 'Fort', 'Moyen', 'Faible'
    Ordre     INT          NOT NULL
);

-- Statuts possibles d'un ticket
CREATE TABLE StatutsTicket (
    IdStatut  INT          NOT NULL IDENTITY(1,1) PRIMARY KEY,
    NomStatut NVARCHAR(50) NOT NULL UNIQUE,
    -- 'Nouveau', 'Hors périmètre', 'En cours', 'Annulé', 'Résolu', 'Fermé'
    Couleur   NVARCHAR(20) NULL  -- ex: '#28a745' pour l'affichage badge
);

-- Statuts possibles d'une intervention
CREATE TABLE StatutsIntervention (
    IdStatut  INT          NOT NULL IDENTITY(1,1) PRIMARY KEY,
    NomStatut NVARCHAR(50) NOT NULL UNIQUE
    -- 'Planifiée', 'En cours', 'Suspendue', 'Terminée', 'Annulée'
);

-- Types de sous-catégorie ticket (optionnel, extensible)
CREATE TABLE TypesTicket (
    IdType    INT          NOT NULL IDENTITY(1,1) PRIMARY KEY,
    NomType   NVARCHAR(100) NOT NULL,
    IdCategorie INT        NOT NULL REFERENCES Categories(IdCategorie)
);

-- ============================================================
-- 2. UTILISATEURS
-- ============================================================

CREATE TABLE Utilisateurs (
    IdUtilisateur  INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
    Nom            NVARCHAR(100) NOT NULL,
    Prenom         NVARCHAR(100) NOT NULL,
    Email          NVARCHAR(200) NOT NULL UNIQUE,
    MotDePasse     NVARCHAR(256) NOT NULL,  -- hash bcrypt/SHA256
    IdRole         INT           NOT NULL REFERENCES Roles(IdRole),
    IsActive       BIT           NOT NULL DEFAULT 1,
    DateCreation   DATETIME2     NOT NULL DEFAULT GETDATE(),
    DernierAcces   DATETIME2     NULL,

    -- Champs spécifiques Employé
    Direction      NVARCHAR(100) NULL,
    Departement    NVARCHAR(100) NULL,

    -- Champs spécifiques Technicien
    Niveau         NVARCHAR(50)  NULL,  -- 'N1', 'N2', 'N3'
    Specialite     NVARCHAR(150) NULL,

    -- Contact
    Telephone      NVARCHAR(20)  NULL
);

-- ============================================================
-- 3. TICKETS
-- ============================================================

CREATE TABLE Tickets (
    IdTicket         INT            NOT NULL IDENTITY(1000,1) PRIMARY KEY,
    Titre            NVARCHAR(200)  NOT NULL,
    Description      NVARCHAR(MAX)  NOT NULL,
    DateOuverture    DATETIME2      NOT NULL DEFAULT GETDATE(),
    DateResolution   DATETIME2      NULL,
    DateFermeture    DATETIME2      NULL,

    -- Relations
    IdDemandeur      INT            NOT NULL REFERENCES Utilisateurs(IdUtilisateur),
    IdHelpdesk       INT            NULL     REFERENCES Utilisateurs(IdUtilisateur),
    IdCategorie      INT            NULL     REFERENCES Categories(IdCategorie),
    IdType           INT            NULL     REFERENCES TypesTicket(IdType),
    IdStatut         INT            NOT NULL REFERENCES StatutsTicket(IdStatut),
    IdUrgence        INT            NULL     REFERENCES NiveauxUrgence(IdUrgence),
    IdImpact         INT            NULL     REFERENCES NiveauxImpact(IdImpact),

    -- Priorité calculée automatiquement (P1 à P4)
    PrioriteCalculee NVARCHAR(5)    NULL,

    -- Clôture / résolution
    MotifRejet       NVARCHAR(500)  NULL,
    NoteEmployee     INT            NULL CHECK (NoteEmployee BETWEEN 1 AND 5),
    CommentaireNote  NVARCHAR(500)  NULL,

    -- SLA
    DelaiResolutionCible DATETIME2  NULL,  -- calculé selon priorité

    CONSTRAINT CK_Ticket_DemandeurRole CHECK (IdDemandeur <> IdHelpdesk)
);

-- ============================================================
-- 4. PIÈCES JOINTES
-- ============================================================

CREATE TABLE PiecesJointes (
    IdPieceJointe  INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
    IdTicket       INT            NOT NULL REFERENCES Tickets(IdTicket) ON DELETE CASCADE,
    NomFichier     NVARCHAR(255)  NOT NULL,
    CheminFichier  NVARCHAR(500)  NOT NULL,
    Taille         BIGINT         NOT NULL,  -- en octets
    Format         NVARCHAR(20)   NOT NULL,  -- 'jpg', 'png', 'pdf', etc.
    DateAjout      DATETIME2      NOT NULL DEFAULT GETDATE(),
    IdUtilisateur  INT            NOT NULL REFERENCES Utilisateurs(IdUtilisateur)
);

-- ============================================================
-- 5. INTERVENTIONS
-- ============================================================

CREATE TABLE Interventions (
    IdIntervention     INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
    IdTicket           INT           NOT NULL REFERENCES Tickets(IdTicket),
    IdTechnicien       INT           NOT NULL REFERENCES Utilisateurs(IdUtilisateur),
    DescriptionAction  NVARCHAR(MAX) NOT NULL,
    DateAction         DATETIME2     NOT NULL DEFAULT GETDATE(),
    TempsPasse         INT           NULL,  -- en minutes
    IdStatut           INT           NOT NULL REFERENCES StatutsIntervention(IdStatut),
    Notes              NVARCHAR(MAX) NULL,
    DateDebut          DATETIME2     NULL,
    DateFin            DATETIME2     NULL
);

-- ============================================================
-- 6. HISTORIQUE / AUDIT
-- ============================================================

CREATE TABLE HistoriqueTickets (
    IdHistorique  INT            NOT NULL IDENTITY(1,1) PRIMARY KEY,
    IdTicket      INT            NOT NULL REFERENCES Tickets(IdTicket),
    IdUtilisateur INT            NOT NULL REFERENCES Utilisateurs(IdUtilisateur),
    DateAction    DATETIME2      NOT NULL DEFAULT GETDATE(),
    Action        NVARCHAR(100)  NOT NULL,  -- 'Création', 'Qualification', 'Assignation', etc.
    AncienneValeur NVARCHAR(500) NULL,
    NouvelleValeur NVARCHAR(500) NULL,
    Commentaire   NVARCHAR(500)  NULL
);

-- ============================================================
-- 7. NOTIFICATIONS
-- ============================================================

CREATE TABLE Notifications (
    IdNotification INT           NOT NULL IDENTITY(1,1) PRIMARY KEY,
    IdUtilisateur  INT           NOT NULL REFERENCES Utilisateurs(IdUtilisateur),
    IdTicket       INT           NULL     REFERENCES Tickets(IdTicket),
    Message        NVARCHAR(500) NOT NULL,
    DateEnvoi      DATETIME2     NOT NULL DEFAULT GETDATE(),
    IsLue          BIT           NOT NULL DEFAULT 0
);

-- ============================================================
-- 8. INDEXES (PERFORMANCE)
-- ============================================================

CREATE INDEX IX_Tickets_Statut       ON Tickets(IdStatut);
CREATE INDEX IX_Tickets_Demandeur    ON Tickets(IdDemandeur);
CREATE INDEX IX_Tickets_Helpdesk     ON Tickets(IdHelpdesk);
CREATE INDEX IX_Tickets_DateOuv      ON Tickets(DateOuverture DESC);
CREATE INDEX IX_Interventions_Ticket ON Interventions(IdTicket);
CREATE INDEX IX_Interventions_Tech   ON Interventions(IdTechnicien);
CREATE INDEX IX_Notifs_User          ON Notifications(IdUtilisateur, IsLue);

-- ============================================================
-- 9. DONNÉES DE RÉFÉRENCE (SEED)
-- ============================================================

INSERT INTO Roles (NomRole, Description) VALUES
('Employe',        'Collaborateur pouvant soumettre des tickets'),
('Helpdesk',       'Agent Helpdesk niveau 1 - qualification et dispatch'),
('Technicien',     'Technicien IT - résolution des tickets'),
('Administrateur', 'Administrateur système - gestion complète');

INSERT INTO Categories (NomCategorie, Description) VALUES
('Support IT',              'Pannes, bugs logiciels, accès systèmes'),
('Moyens généraux',         'Locaux, équipements bureau, logistique'),
('Développement IT',        'Anomalies applicatives, nouvelles fonctionnalités'),
('Demande de service IT',   'Installation logiciel, création de compte, accès');

INSERT INTO NiveauxUrgence (NomUrgence, Ordre) VALUES
('Critique', 1),
('Haute',    2),
('Moyenne',  3),
('Basse',    4);

INSERT INTO NiveauxImpact (NomImpact, Ordre) VALUES
('Critique', 1),
('Fort',     2),
('Moyen',    3),
('Faible',   4);

INSERT INTO StatutsTicket (NomStatut, Couleur) VALUES
('Nouveau',          '#17a2b8'),
('Hors périmètre',   '#6f42c1'),
('En cours',         '#007bff'),
('Annulé',           '#dc3545'),
('Résolu',           '#28a745'),
('Fermé',            '#6c757d');

INSERT INTO StatutsIntervention (NomStatut) VALUES
('Planifiée'),
('En cours'),
('Suspendue'),
('Terminée'),
('Annulée');

-- Types de tickets par catégorie
INSERT INTO TypesTicket (NomType, IdCategorie) VALUES
('Problème de connexion',       1),
('Panne matérielle',            1),
('Accès refusé',                1),
('Problème messagerie',         1),
('Demande d''équipement',       2),
('Maintenance locaux',          2),
('Bug applicatif',              3),
('Nouvelle fonctionnalité',     3),
('Installation logiciel',       4),
('Création de compte',          4),
('Réinitialisation mot de passe',4);

-- Compte administrateur par défaut (mot de passe: Admin@123 - à hasher en prod)
INSERT INTO Utilisateurs (Nom, Prenom, Email, MotDePasse, IdRole)
VALUES ('Admin', 'Système', 'admin@novec.ma',
        '8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918', -- SHA256 de 'Admin@123'
        (SELECT IdRole FROM Roles WHERE NomRole = 'Administrateur'));

-- Compte Helpdesk de test
INSERT INTO Utilisateurs (Nom, Prenom, Email, MotDePasse, IdRole)
VALUES ('HelpDesk', 'Support', 'helpdesk@novec.ma',
        '8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918',
        (SELECT IdRole FROM Roles WHERE NomRole = 'Helpdesk'));

GO

PRINT '✅ Base de données HelpDesk_Manager créée avec succès.';

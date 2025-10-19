USE PagueVeloz;

CREATE TABLE AccountStatus
(
    Id TINYINT NOT NULL PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL UNIQUE
);

INSERT INTO AccountStatus (Id, Name) VALUES
(0, 'Active'),
(1, 'Inactive'),
(2, 'Blocked');


CREATE TABLE TransactionType
(
    Id TINYINT NOT NULL PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL UNIQUE
);

INSERT INTO TransactionType (Id, Name) VALUES
(0, 'Credit'),
(1, 'Debit'),
(2, 'Reserve'),
(3, 'Capture'),
(4, 'Reversal'),
(5, 'Transfer');



CREATE TABLE Client
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL
);



CREATE TABLE Account
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    AccountId NVARCHAR(20) NOT NULL UNIQUE,  
    ClientId INT NOT NULL,
    AvailableBalance BIGINT NOT NULL DEFAULT 0,  
    ReservedBalance BIGINT NOT NULL DEFAULT 0,  
    CreditLimit BIGINT NOT NULL DEFAULT 0,      
    StatusId TINYINT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Account_Client FOREIGN KEY (ClientId)
        REFERENCES Client(Id) ON DELETE CASCADE,

    CONSTRAINT FK_Account_Status FOREIGN KEY (StatusId)
        REFERENCES AccountStatus(Id)
);


CREATE TABLE [Transaction]
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    TransactionId NVARCHAR(50) NOT NULL UNIQUE,   
    AccountId NVARCHAR(20) NOT NULL,
    TypeId TINYINT NOT NULL,
    Amount BIGINT NOT NULL CHECK (Amount > 0),    
    Currency CHAR(3) NOT NULL DEFAULT 'BRL',
    ReferenceId NVARCHAR(100) NULL,
    Description NVARCHAR(200) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT FK_Transaction_Account FOREIGN KEY (AccountId)
        REFERENCES Account(AccountId) ON DELETE CASCADE,

    CONSTRAINT FK_Transaction_Type FOREIGN KEY (TypeId)
        REFERENCES TransactionType(Id)
);

CREATE TRIGGER dbo.trg_SetTransactionId
ON dbo.[Transaction]
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE t
    SET 
        t.TransactionId = CONCAT('TXN-', i.Id, '-PROCESSED'),
        t.ReferenceId   = CONCAT('TXN-', i.Id)
    FROM dbo.[Transaction] AS t
    INNER JOIN inserted AS i ON t.Id = i.Id;
END;
GO


CREATE INDEX IX_Account_ClientId ON Account(ClientId);


INSERT INTO Client (Name)
VALUES 
('Rafael Oliveira'),
('Karen Oliveira');


INSERT INTO Account (AccountId, ClientId, AvailableBalance, CreditLimit, StatusId)
VALUES
('ACC-001', 1, 0, 500000, 0),  
('ACC-002', 2, 0, 1000000, 0);
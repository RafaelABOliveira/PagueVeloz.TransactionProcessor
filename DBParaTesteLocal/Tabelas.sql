CREATE TABLE AccountStatus
(
    Id TINYINT NOT NULL PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL
);

INSERT INTO AccountStatus (Id, Name) VALUES
(0, 'Active'),
(1, 'Inactive'),
(2, 'Blocked');

CREATE TABLE TransactionType
(
    Id TINYINT NOT NULL PRIMARY KEY,
    Name NVARCHAR(50) NOT NULL
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
    ClientId INT NOT NULL,
    AvailableBalance DECIMAL(18,2) NOT NULL DEFAULT 0,
    ReservedBalance DECIMAL(18,2) NOT NULL DEFAULT 0,
    CreditLimit DECIMAL(18,2) NOT NULL DEFAULT 0,
    StatusId TINYINT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Account_Client FOREIGN KEY (ClientId) REFERENCES Client(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Account_Status FOREIGN KEY (StatusId) REFERENCES AccountStatus(Id)
);

CREATE TABLE [Transaction]
(
    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    AccountId INT NOT NULL,
    TypeId TINYINT NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Date DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_Transaction_Account FOREIGN KEY (AccountId) REFERENCES Account(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Transaction_Type FOREIGN KEY (TypeId) REFERENCES TransactionType(Id)
);

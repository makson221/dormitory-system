namespace Dormitory.Data
{
    /// <summary>Структура бази даних (DDL).</summary>
    public static class Schema
    {
        public const string CreateSql = @"
CREATE TABLE IF NOT EXISTS Users (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    Login         TEXT    NOT NULL UNIQUE COLLATE NOCASE,
    PasswordHash  TEXT    NOT NULL,
    Salt          TEXT    NOT NULL,
    FullName      TEXT    NOT NULL,
    Role          TEXT    NOT NULL CHECK (Role IN ('Admin', 'Commandant')),
    IsActive      INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS Rooms (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    Number        TEXT    NOT NULL UNIQUE,
    Floor         INTEGER NOT NULL CHECK (Floor > 0),
    Capacity      INTEGER NOT NULL CHECK (Capacity BETWEEN 1 AND 10),
    Gender        TEXT    NOT NULL DEFAULT '' CHECK (Gender IN ('', 'Ч', 'Ж')),
    PricePerMonth REAL    NOT NULL CHECK (PricePerMonth >= 0),
    Notes         TEXT
);

CREATE TABLE IF NOT EXISTS Students (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    LastName       TEXT    NOT NULL,
    FirstName      TEXT    NOT NULL,
    MiddleName     TEXT,
    BirthDate      TEXT,
    Gender         TEXT    NOT NULL CHECK (Gender IN ('Ч', 'Ж')),
    Faculty        TEXT    NOT NULL,
    Course         INTEGER NOT NULL CHECK (Course BETWEEN 1 AND 6),
    GroupName      TEXT,
    StudentTicket  TEXT    NOT NULL UNIQUE,
    Phone          TEXT,
    Email          TEXT,
    BenefitPercent INTEGER NOT NULL DEFAULT 0 CHECK (BenefitPercent BETWEEN 0 AND 100)
);

CREATE TABLE IF NOT EXISTS Accommodations (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    StudentId      INTEGER NOT NULL REFERENCES Students(Id) ON DELETE RESTRICT,
    RoomId         INTEGER NOT NULL REFERENCES Rooms(Id)    ON DELETE RESTRICT,
    CheckInDate    TEXT    NOT NULL,
    CheckOutDate   TEXT,
    ContractNumber TEXT,
    Notes          TEXT,
    CHECK (CheckOutDate IS NULL OR CheckOutDate >= CheckInDate)
);

-- студент може мати лише одне активне поселення
CREATE UNIQUE INDEX IF NOT EXISTS UX_Accommodations_ActiveStudent
    ON Accommodations(StudentId) WHERE CheckOutDate IS NULL;
CREATE INDEX IF NOT EXISTS IX_Accommodations_Room ON Accommodations(RoomId);

CREATE TABLE IF NOT EXISTS Payments (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    StudentId     INTEGER NOT NULL REFERENCES Students(Id) ON DELETE RESTRICT,
    Amount        REAL    NOT NULL CHECK (Amount > 0),
    PaymentDate   TEXT    NOT NULL,
    Period        TEXT    NOT NULL,
    ReceiptNumber TEXT,
    UserId        INTEGER REFERENCES Users(Id),
    Comment       TEXT
);

CREATE INDEX IF NOT EXISTS IX_Payments_Student ON Payments(StudentId);
CREATE INDEX IF NOT EXISTS IX_Payments_Date    ON Payments(PaymentDate);
";
    }
}

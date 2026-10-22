namespace LibraryManagement;

/// <summary>
/// Idempotent schema for both providers.
/// Loans are their own table, so a title can have several copies on loan
/// to different students at the same time.
/// Available copies = Book.TotalCopies - active (unreturned) loans.
/// </summary>
public static class Schema
{
    public static readonly string[] Sqlite =
    {
        """
        CREATE TABLE IF NOT EXISTS Admin (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            Email        TEXT NOT NULL UNIQUE COLLATE NOCASE,
            PasswordHash TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS Student (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            Name         TEXT NOT NULL,
            Address      TEXT NOT NULL DEFAULT '',
            Age          INTEGER NOT NULL,
            Email        TEXT NOT NULL UNIQUE COLLATE NOCASE,
            PasswordHash TEXT NOT NULL
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS Book (
            Id          INTEGER PRIMARY KEY AUTOINCREMENT,
            Title       TEXT NOT NULL,
            Author      TEXT NOT NULL DEFAULT '',
            ISBN        TEXT NOT NULL DEFAULT '',
            Language    TEXT NOT NULL DEFAULT '',
            Subject     TEXT NOT NULL DEFAULT '',
            Pages       INTEGER NOT NULL DEFAULT 0,
            TotalCopies INTEGER NOT NULL CHECK (TotalCopies >= 0)
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS Loan (
            Id         INTEGER PRIMARY KEY AUTOINCREMENT,
            StudentId  INTEGER NOT NULL REFERENCES Student(Id),
            BookId     INTEGER NOT NULL REFERENCES Book(Id),
            LoanedAt   TEXT NOT NULL,
            ReturnedAt TEXT NULL
        );
        """,
        "CREATE INDEX IF NOT EXISTS IX_Loan_Active ON Loan (StudentId, BookId, ReturnedAt);",
    };

    public static readonly string[] SqlServer =
    {
        """
        IF OBJECT_ID('dbo.Admin', 'U') IS NULL
        CREATE TABLE dbo.Admin (
            Id           INT IDENTITY(1,1) PRIMARY KEY,
            Email        NVARCHAR(254) NOT NULL UNIQUE,
            PasswordHash NVARCHAR(200) NOT NULL
        );
        """,
        """
        IF OBJECT_ID('dbo.Student', 'U') IS NULL
        CREATE TABLE dbo.Student (
            Id           INT IDENTITY(1,1) PRIMARY KEY,
            Name         NVARCHAR(100) NOT NULL,
            Address      NVARCHAR(200) NOT NULL DEFAULT '',
            Age          INT NOT NULL,
            Email        NVARCHAR(254) NOT NULL UNIQUE,
            PasswordHash NVARCHAR(200) NOT NULL
        );
        """,
        """
        IF OBJECT_ID('dbo.Book', 'U') IS NULL
        CREATE TABLE dbo.Book (
            Id          INT IDENTITY(1,1) PRIMARY KEY,
            Title       NVARCHAR(200) NOT NULL,
            Author      NVARCHAR(200) NOT NULL DEFAULT '',
            ISBN        NVARCHAR(20)  NOT NULL DEFAULT '',
            Language    NVARCHAR(50)  NOT NULL DEFAULT '',
            Subject     NVARCHAR(100) NOT NULL DEFAULT '',
            Pages       INT NOT NULL DEFAULT 0,
            TotalCopies INT NOT NULL CHECK (TotalCopies >= 0)
        );
        """,
        """
        IF OBJECT_ID('dbo.Loan', 'U') IS NULL
        CREATE TABLE dbo.Loan (
            Id         INT IDENTITY(1,1) PRIMARY KEY,
            StudentId  INT NOT NULL REFERENCES dbo.Student(Id),
            BookId     INT NOT NULL REFERENCES dbo.Book(Id),
            LoanedAt   DATETIME2 NOT NULL,
            ReturnedAt DATETIME2 NULL
        );
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Loan_Active')
        CREATE INDEX IX_Loan_Active ON dbo.Loan (StudentId, BookId, ReturnedAt);
        """,
    };
}

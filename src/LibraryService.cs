using System.Data;
using System.Data.Common;

namespace LibraryManagement;

public sealed record Student(int Id, string Name, string Address, int Age, string Email, int ActiveLoans);

public sealed record Book(
    int Id, string Title, string Author, string Isbn, string Language,
    string Subject, int Pages, int TotalCopies, int OnLoan)
{
    public int Available => TotalCopies - OnLoan;
}

public sealed record BookInput(
    string Title, string Author, string Isbn, string Language,
    string Subject, int Pages, int TotalCopies);

public sealed record StudentInput(string Name, string Address, int Age, string Email);

public sealed record LoanInfo(int LoanId, int BookId, string Title, DateTime LoanedAt);

public sealed record ActiveLoan(
    int LoanId, int StudentId, string StudentName, string StudentEmail,
    int BookId, string Title, DateTime LoanedAt);

public sealed record LibraryStats(int Titles, int Copies, int OnLoan, int Students);

/// <summary>Raised for expected business-rule failures shown to the user.</summary>
public sealed class LibraryException(string message) : Exception(message);

/// <summary>
/// All library rules live here, independent of the console UI.
/// </summary>
public sealed class LibraryService(Database db)
{
    public const int MaxLoansPerStudent = 3;

    private const string BookSelect = """
        SELECT b.Id, b.Title, b.Author, b.ISBN, b.Language, b.Subject, b.Pages, b.TotalCopies,
               (SELECT COUNT(*) FROM Loan l WHERE l.BookId = b.Id AND l.ReturnedAt IS NULL) AS OnLoan
        FROM Book b
        """;

    private const string StudentSelect = """
        SELECT s.Id, s.Name, s.Address, s.Age, s.Email,
               (SELECT COUNT(*) FROM Loan l WHERE l.StudentId = s.Id AND l.ReturnedAt IS NULL) AS ActiveLoans
        FROM Student s
        """;

    private static Book MapBook(IDataRecord r) => new(
        r.Int("Id"), r.Text("Title"), r.Text("Author"), r.Text("ISBN"), r.Text("Language"),
        r.Text("Subject"), r.Int("Pages"), r.Int("TotalCopies"), r.Int("OnLoan"));

    private static Student MapStudent(IDataRecord r) => new(
        r.Int("Id"), r.Text("Name"), r.Text("Address"), r.Int("Age"), r.Text("Email"), r.Int("ActiveLoans"));

    // ---- Accounts -----------------------------------------------------------

    public bool HasAnyAdmin()
    {
        using var c = db.Open();
        return Database.Scalar(c, null, "SELECT COUNT(*) FROM Admin") > 0;
    }

    public void CreateAdmin(string email, string password)
    {
        using var c = db.Open();
        EnsureEmailFree(c, "Admin", email);
        Database.Execute(c, null,
            "INSERT INTO Admin (Email, PasswordHash) VALUES (@email, @hash)",
            ("@email", Normalize(email)), ("@hash", PasswordHasher.Hash(password)));
    }

    public bool AdminLogin(string email, string password)
    {
        using var c = db.Open();
        var hash = Database.Query(c, "SELECT PasswordHash FROM Admin WHERE Email = @email",
            r => r.Text("PasswordHash"), ("@email", Normalize(email))).FirstOrDefault();
        return hash is not null && PasswordHasher.Verify(password, hash);
    }

    public Student RegisterStudent(StudentInput input, string password)
    {
        using var c = db.Open();
        EnsureEmailFree(c, "Student", input.Email);
        Database.Execute(c, null,
            """
            INSERT INTO Student (Name, Address, Age, Email, PasswordHash)
            VALUES (@name, @address, @age, @email, @hash)
            """,
            ("@name", input.Name), ("@address", input.Address), ("@age", input.Age),
            ("@email", Normalize(input.Email)), ("@hash", PasswordHasher.Hash(password)));
        return FindStudentByEmail(c, input.Email)!;
    }

    /// <returns>The student, or null when the credentials are wrong.</returns>
    public Student? StudentLogin(string email, string password)
    {
        using var c = db.Open();
        var hash = Database.Query(c, "SELECT PasswordHash FROM Student WHERE Email = @email",
            r => r.Text("PasswordHash"), ("@email", Normalize(email))).FirstOrDefault();
        if (hash is null || !PasswordHasher.Verify(password, hash))
            return null;
        return FindStudentByEmail(c, email);
    }

    // ---- Books --------------------------------------------------------------

    public List<Book> Books(string? search = null)
    {
        using var c = db.Open();
        if (string.IsNullOrWhiteSpace(search))
            return Database.Query(c, BookSelect + " ORDER BY b.Title", MapBook);

        var pattern = "%" + search.Trim().ToLowerInvariant() + "%";
        return Database.Query(c,
            BookSelect + """
             WHERE LOWER(b.Title) LIKE @q OR LOWER(b.Author) LIKE @q
                OR LOWER(b.Subject) LIKE @q OR b.ISBN LIKE @q
            ORDER BY b.Title
            """,
            MapBook, ("@q", pattern));
    }

    public Book? FindBook(int id)
    {
        using var c = db.Open();
        return Database.Query(c, BookSelect + " WHERE b.Id = @id", MapBook, ("@id", id)).FirstOrDefault();
    }

    public void AddBook(BookInput b)
    {
        using var c = db.Open();
        Database.Execute(c, null,
            """
            INSERT INTO Book (Title, Author, ISBN, Language, Subject, Pages, TotalCopies)
            VALUES (@title, @author, @isbn, @lang, @subject, @pages, @copies)
            """,
            ("@title", b.Title), ("@author", b.Author), ("@isbn", b.Isbn), ("@lang", b.Language),
            ("@subject", b.Subject), ("@pages", b.Pages), ("@copies", b.TotalCopies));
    }

    public void UpdateBook(int id, BookInput b)
    {
        using var c = db.Open();
        var onLoan = Database.Scalar(c, null,
            "SELECT COUNT(*) FROM Loan WHERE BookId = @id AND ReturnedAt IS NULL", ("@id", id));
        if (b.TotalCopies < onLoan)
            throw new LibraryException(
                onLoan == 1
                    ? "1 copy is currently on loan; total copies can't be lower than that."
                    : $"{onLoan} copies are currently on loan; total copies can't be lower than that.");

        var rows = Database.Execute(c, null,
            """
            UPDATE Book SET Title = @title, Author = @author, ISBN = @isbn, Language = @lang,
                            Subject = @subject, Pages = @pages, TotalCopies = @copies
            WHERE Id = @id
            """,
            ("@title", b.Title), ("@author", b.Author), ("@isbn", b.Isbn), ("@lang", b.Language),
            ("@subject", b.Subject), ("@pages", b.Pages), ("@copies", b.TotalCopies), ("@id", id));
        if (rows == 0) throw new LibraryException("Book not found.");
    }

    public void RemoveBook(int id)
    {
        using var c = db.Open();
        using var tx = c.BeginTransaction();
        if (Database.Scalar(c, tx, "SELECT COUNT(*) FROM Loan WHERE BookId = @id AND ReturnedAt IS NULL",
                ("@id", id)) > 0)
            throw new LibraryException("This book has copies on loan. Wait until they are returned.");

        Database.Execute(c, tx, "DELETE FROM Loan WHERE BookId = @id", ("@id", id));
        if (Database.Execute(c, tx, "DELETE FROM Book WHERE Id = @id", ("@id", id)) == 0)
            throw new LibraryException("Book not found.");
        tx.Commit();
    }

    // ---- Students -----------------------------------------------------------

    public List<Student> Students()
    {
        using var c = db.Open();
        return Database.Query(c, StudentSelect + " ORDER BY s.Name", MapStudent);
    }

    public Student? FindStudent(int id)
    {
        using var c = db.Open();
        return Database.Query(c, StudentSelect + " WHERE s.Id = @id", MapStudent, ("@id", id)).FirstOrDefault();
    }

    public void UpdateStudent(int id, StudentInput s)
    {
        using var c = db.Open();
        var owner = FindStudentByEmail(c, s.Email);
        if (owner is not null && owner.Id != id)
            throw new LibraryException("Another student already uses this email.");

        var rows = Database.Execute(c, null,
            "UPDATE Student SET Name = @name, Address = @address, Age = @age, Email = @email WHERE Id = @id",
            ("@name", s.Name), ("@address", s.Address), ("@age", s.Age),
            ("@email", Normalize(s.Email)), ("@id", id));
        if (rows == 0) throw new LibraryException("Student not found.");
    }

    public void RemoveStudent(int id)
    {
        using var c = db.Open();
        using var tx = c.BeginTransaction();
        if (Database.Scalar(c, tx, "SELECT COUNT(*) FROM Loan WHERE StudentId = @id AND ReturnedAt IS NULL",
                ("@id", id)) > 0)
            throw new LibraryException("This student still has books on loan.");

        Database.Execute(c, tx, "DELETE FROM Loan WHERE StudentId = @id", ("@id", id));
        if (Database.Execute(c, tx, "DELETE FROM Student WHERE Id = @id", ("@id", id)) == 0)
            throw new LibraryException("Student not found.");
        tx.Commit();
    }

    // ---- Loans --------------------------------------------------------------

    public List<LoanInfo> ActiveLoans(int studentId)
    {
        using var c = db.Open();
        return Database.Query(c,
            """
            SELECT l.Id, l.BookId, b.Title, l.LoanedAt
            FROM Loan l JOIN Book b ON b.Id = l.BookId
            WHERE l.StudentId = @id AND l.ReturnedAt IS NULL
            ORDER BY l.LoanedAt
            """,
            r => new LoanInfo(r.Int("Id"), r.Int("BookId"), r.Text("Title"), Convert.ToDateTime(r["LoanedAt"])),
            ("@id", studentId));
    }

    public void Lend(int studentId, int bookId)
    {
        using var c = db.Open();
        // Serializable keeps two simultaneous loans from taking the last copy.
        using var tx = c.BeginTransaction(IsolationLevel.Serializable);

        if (Database.Scalar(c, tx, "SELECT COUNT(*) FROM Student WHERE Id = @s", ("@s", studentId)) == 0)
            throw new LibraryException("Your account no longer exists. Please sign in again.");

        var active = Database.Scalar(c, tx,
            "SELECT COUNT(*) FROM Loan WHERE StudentId = @s AND ReturnedAt IS NULL", ("@s", studentId));
        if (active >= MaxLoansPerStudent)
            throw new LibraryException($"You already have {MaxLoansPerStudent} books. Return one first.");

        var exists = Database.Scalar(c, tx, "SELECT COUNT(*) FROM Book WHERE Id = @b", ("@b", bookId)) > 0;
        if (!exists) throw new LibraryException("Book not found.");
        var total = Database.Scalar(c, tx, "SELECT TotalCopies FROM Book WHERE Id = @b", ("@b", bookId));

        var onLoan = Database.Scalar(c, tx,
            "SELECT COUNT(*) FROM Loan WHERE BookId = @b AND ReturnedAt IS NULL", ("@b", bookId));
        if (onLoan >= total) throw new LibraryException("No copies of this book are available right now.");

        var already = Database.Scalar(c, tx,
            "SELECT COUNT(*) FROM Loan WHERE BookId = @b AND StudentId = @s AND ReturnedAt IS NULL",
            ("@b", bookId), ("@s", studentId));
        if (already > 0) throw new LibraryException("You already have a copy of this book.");

        Database.Execute(c, tx,
            "INSERT INTO Loan (StudentId, BookId, LoanedAt) VALUES (@s, @b, @at)",
            ("@s", studentId), ("@b", bookId), ("@at", DateTime.UtcNow));
        tx.Commit();
    }

    public void Return(int studentId, int loanId)
    {
        using var c = db.Open();
        var rows = Database.Execute(c, null,
            "UPDATE Loan SET ReturnedAt = @at WHERE Id = @id AND StudentId = @s AND ReturnedAt IS NULL",
            ("@at", DateTime.UtcNow), ("@id", loanId), ("@s", studentId));
        if (rows == 0) throw new LibraryException("That loan was not found on your account.");
    }

    public List<ActiveLoan> AllActiveLoans()
    {
        using var c = db.Open();
        return Database.Query(c,
            """
            SELECT l.Id, l.StudentId, s.Name, s.Email, l.BookId, b.Title, l.LoanedAt
            FROM Loan l
            JOIN Student s ON s.Id = l.StudentId
            JOIN Book b ON b.Id = l.BookId
            WHERE l.ReturnedAt IS NULL
            ORDER BY l.LoanedAt
            """,
            r => new ActiveLoan(r.Int("Id"), r.Int("StudentId"), r.Text("Name"), r.Text("Email"),
                r.Int("BookId"), r.Text("Title"), Convert.ToDateTime(r["LoanedAt"])));
    }

    /// <summary>Admin check-in of a returned book.</summary>
    public void MarkReturned(int loanId)
    {
        using var c = db.Open();
        var rows = Database.Execute(c, null,
            "UPDATE Loan SET ReturnedAt = @at WHERE Id = @id AND ReturnedAt IS NULL",
            ("@at", DateTime.UtcNow), ("@id", loanId));
        if (rows == 0) throw new LibraryException("That loan is already closed.");
    }

    public LibraryStats Stats()
    {
        using var c = db.Open();
        return new LibraryStats(
            (int)Database.Scalar(c, null, "SELECT COUNT(*) FROM Book"),
            (int)Database.Scalar(c, null, "SELECT COALESCE(SUM(TotalCopies), 0) FROM Book"),
            (int)Database.Scalar(c, null, "SELECT COUNT(*) FROM Loan WHERE ReturnedAt IS NULL"),
            (int)Database.Scalar(c, null, "SELECT COUNT(*) FROM Student"));
    }

    // ---- Demo data ----------------------------------------------------------

    public void SeedSampleBooks()
    {
        BookInput[] samples =
        {
            new("Clean Code", "Robert C. Martin", "9780132350884", "English", "Software", 464, 3),
            new("The Pragmatic Programmer", "Andrew Hunt, David Thomas", "9780135957059", "English", "Software", 352, 2),
            new("Designing Data-Intensive Applications", "Martin Kleppmann", "9781449373320", "English", "Databases", 616, 2),
            new("Database System Concepts", "Silberschatz, Korth, Sudarshan", "9780078022159", "English", "Databases", 1376, 1),
            new("Introduction to Algorithms", "Cormen, Leiserson, Rivest, Stein", "9780262046305", "English", "Algorithms", 1312, 1),
            new("Refactoring", "Martin Fowler", "9780134757599", "English", "Software", 448, 2),
            new("Suç ve Ceza", "Fyodor Dostoyevski", "9789750719387", "Türkçe", "Roman", 687, 2),
            new("Kürk Mantolu Madonna", "Sabahattin Ali", "9789753638029", "Türkçe", "Roman", 160, 3),
        };
        foreach (var book in samples) AddBook(book);
    }

    // ---- Helpers ------------------------------------------------------------

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static void EnsureEmailFree(DbConnection c, string table, string email)
    {
        if (Database.Scalar(c, null, $"SELECT COUNT(*) FROM {table} WHERE Email = @email",
                ("@email", Normalize(email))) > 0)
            throw new LibraryException("An account with this email already exists.");
    }

    private static Student? FindStudentByEmail(DbConnection c, string email) =>
        Database.Query(c, StudentSelect + " WHERE s.Email = @email", MapStudent,
            ("@email", Normalize(email))).FirstOrDefault();
}

using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Pages;

public class IndexModel(LibraryService library) : AppPage
{
    [BindProperty(SupportsGet = true, Name = "q")] public string? Query { get; set; }
    [BindProperty(SupportsGet = true)] public bool AvailableOnly { get; set; }

    public List<Book> Books { get; private set; } = new();
    public HashSet<int> MyBookIds { get; private set; } = new();
    public int MyLoanCount { get; private set; }

    public void OnGet()
    {
        Books = library.Books(Query);
        if (AvailableOnly) Books = Books.Where(b => b.Available > 0).ToList();

        if (User.IsInRole(Roles.Student))
        {
            var loans = library.ActiveLoans(CurrentUserId);
            MyBookIds = loans.Select(l => l.BookId).ToHashSet();
            MyLoanCount = loans.Count;
        }
    }

    // Razor Pages ignores [Authorize] on handlers, so the role is checked here.
    public IActionResult OnPostBorrow(int bookId)
    {
        if (!User.IsInRole(Roles.Student))
            return RedirectToPage("/Account/Login", new { returnUrl = "/" });

        return Try(() => library.Lend(CurrentUserId, bookId),
            "Borrowed! You'll find it under My loans.",
            routeValues: new { q = Query, AvailableOnly });
    }
}

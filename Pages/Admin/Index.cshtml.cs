using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Pages.Admin;

public class IndexModel(LibraryService library) : AppPage
{
    public LibraryStats Stats { get; private set; } = new(0, 0, 0, 0);
    public List<ActiveLoan> RecentLoans { get; private set; } = new();
    public List<Book> LowStock { get; private set; } = new();

    [BindProperty, Required, EmailAddress] public string NewAdminEmail { get; set; } = "";
    [BindProperty, Required, StringLength(128, MinimumLength = 8, ErrorMessage = "Use at least 8 characters."), DataType(DataType.Password)]
    public string NewAdminPassword { get; set; } = "";

    public void OnGet() => Load();

    private void Load()
    {
        Stats = library.Stats();
        RecentLoans = library.AllActiveLoans().OrderByDescending(l => l.LoanedAt).Take(5).ToList();
        LowStock = library.Books().Where(b => b.TotalCopies > 0 && b.Available == 0).Take(5).ToList();
    }

    public IActionResult OnPostAddAdmin()
    {
        if (!ModelState.IsValid)
        {
            Load();
            return Page();
        }
        return Try(() => library.CreateAdmin(NewAdminEmail, NewAdminPassword), $"Admin {NewAdminEmail} added.");
    }
}

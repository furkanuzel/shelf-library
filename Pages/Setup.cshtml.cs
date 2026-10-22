using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Pages;

public class SetupModel(LibraryService library) : AppPage
{
    [BindProperty, Required, EmailAddress] public string Email { get; set; } = "";
    [BindProperty, Required, StringLength(128, MinimumLength = 8, ErrorMessage = "Use at least 8 characters."), DataType(DataType.Password)]
    public string Password { get; set; } = "";
    [BindProperty] public bool SampleBooks { get; set; } = true;

    public IActionResult OnGet() => library.HasAnyAdmin() ? RedirectToPage("/Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (library.HasAnyAdmin()) return RedirectToPage("/Index");
        if (!ModelState.IsValid) return Page();

        library.CreateAdmin(Email, Password);
        if (SampleBooks && library.Books().Count == 0) library.SeedSampleBooks();

        await SignInAsync(0, "Admin", Email.Trim().ToLowerInvariant(), Roles.Admin);
        Notify("Your library is ready. Welcome!");
        return RedirectToPage("/Admin/Index");
    }
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Pages.Account;

public class RegisterModel(LibraryService library) : AppPage
{
    [BindProperty, Required, StringLength(100)] public string Name { get; set; } = "";
    [BindProperty, StringLength(200)] public string? Address { get; set; }
    [BindProperty, Range(5, 120)] public int Age { get; set; } = 18;
    [BindProperty, Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [BindProperty, Required, StringLength(128, MinimumLength = 8, ErrorMessage = "Use at least 8 characters."), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? Error { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();
        try
        {
            var student = library.RegisterStudent(
                new StudentInput(Name.Trim(), Address?.Trim() ?? "", Age, Email), Password);
            await SignInAsync(student.Id, student.Name, student.Email, Roles.Student);
            Notify($"Welcome, {student.Name}! You can borrow up to {LibraryService.MaxLoansPerStudent} books.");
            return RedirectToPage("/Index");
        }
        catch (LibraryException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }
}

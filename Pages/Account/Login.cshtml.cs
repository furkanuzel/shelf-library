using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Pages.Account;

public class LoginModel(LibraryService library) : AppPage
{
    [BindProperty, Required, EmailAddress] public string Email { get; set; } = "";
    [BindProperty, Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    [BindProperty] public string Role { get; set; } = Roles.Student;
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (Role == Roles.Admin)
        {
            if (library.AdminLogin(Email, Password))
            {
                await SignInAsync(0, "Admin", Email.Trim().ToLowerInvariant(), Roles.Admin);
                return RedirectLocal(ReturnUrl, "/Admin/Index");
            }
        }
        else
        {
            var student = library.StudentLogin(Email, Password);
            if (student is not null)
            {
                await SignInAsync(student.Id, student.Name, student.Email, Roles.Student);
                return RedirectLocal(ReturnUrl, "/Index");
            }
        }

        Error = "That email and password don't match an account.";
        Password = "";
        return Page();
    }
}

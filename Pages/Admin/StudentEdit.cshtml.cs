using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Pages.Admin;

public class StudentEditModel(LibraryService library) : AppPage
{
    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty, Required, StringLength(100)] public string Name { get; set; } = "";
    [BindProperty, StringLength(200)] public string? Address { get; set; }
    [BindProperty, Range(5, 120)] public int Age { get; set; }
    [BindProperty, Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";

    public string? Error { get; private set; }

    public IActionResult OnGet()
    {
        var s = library.FindStudent(Id);
        if (s is null) return NotFound();
        (Name, Address, Age, Email) = (s.Name, s.Address, s.Age, s.Email);
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid) return Page();
        try
        {
            library.UpdateStudent(Id, new StudentInput(Name.Trim(), Address?.Trim() ?? "", Age, Email));
            Notify("Student updated.");
            return RedirectToPage("/Admin/Students");
        }
        catch (LibraryException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }
}

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Pages.Admin;

public class BookEditModel(LibraryService library) : AppPage
{
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }

    [BindProperty, Required, StringLength(200)] public string Title { get; set; } = "";
    [BindProperty, Required, StringLength(200)] public string Author { get; set; } = "";
    [BindProperty, StringLength(20)] public string? Isbn { get; set; }
    [BindProperty, StringLength(50)] public string? Language { get; set; }
    [BindProperty, StringLength(100)] public string? Subject { get; set; }
    [BindProperty, Range(0, 100_000)] public int Pages { get; set; }
    [BindProperty, Range(0, 10_000), Display(Name = "Total copies")] public int TotalCopies { get; set; } = 1;

    public int OnLoan { get; private set; }
    public string? Error { get; private set; }

    public IActionResult OnGet()
    {
        if (Id is null) return Page();
        var book = library.FindBook(Id.Value);
        if (book is null) return NotFound();
        (Title, Author, Isbn, Language, Subject, Pages, TotalCopies, OnLoan) =
            (book.Title, book.Author, book.Isbn, book.Language, book.Subject, book.Pages, book.TotalCopies, book.OnLoan);
        return Page();
    }

    public IActionResult OnPost()
    {
        if (!ModelState.IsValid) return Page();
        var input = new BookInput(Title.Trim(), Author.Trim(), Isbn?.Trim() ?? "",
            Language?.Trim() ?? "", Subject?.Trim() ?? "", Pages, TotalCopies);
        try
        {
            if (Id is null) library.AddBook(input);
            else library.UpdateBook(Id.Value, input);
            Notify(Id is null ? $"“{input.Title}” added." : "Changes saved.");
            return RedirectToPage("/Admin/Books");
        }
        catch (LibraryException ex)
        {
            Error = ex.Message;
            return Page();
        }
    }
}

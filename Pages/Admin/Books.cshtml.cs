using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Pages.Admin;

public class BooksModel(LibraryService library) : AppPage
{
    [BindProperty(SupportsGet = true, Name = "q")] public string? Query { get; set; }
    public List<Book> Books { get; private set; } = new();

    public void OnGet() => Books = library.Books(Query);

    public IActionResult OnPostDelete(int id) =>
        Try(() => library.RemoveBook(id), "Book removed.");
}

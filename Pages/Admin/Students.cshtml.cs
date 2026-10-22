using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Pages.Admin;

public class StudentsModel(LibraryService library) : AppPage
{
    public List<Student> Students { get; private set; } = new();

    public void OnGet() => Students = library.Students();

    public IActionResult OnPostDelete(int id) =>
        Try(() => library.RemoveStudent(id), "Student removed.");
}

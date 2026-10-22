using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Pages.Admin;

public class LoansModel(LibraryService library) : AppPage
{
    public List<ActiveLoan> Loans { get; private set; } = new();

    public void OnGet() => Loans = library.AllActiveLoans();

    public IActionResult OnPostReturn(int loanId) =>
        Try(() => library.MarkReturned(loanId), "Marked as returned.");
}

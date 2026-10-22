using Microsoft.AspNetCore.Mvc;

namespace LibraryManagement.Pages;

public class MyLoansModel(LibraryService library) : AppPage
{
    public List<LoanInfo> Loans { get; private set; } = new();

    public void OnGet() => Loans = library.ActiveLoans(CurrentUserId);

    public IActionResult OnPostReturn(int loanId) =>
        Try(() => library.Return(CurrentUserId, loanId), "Returned — thanks!");
}

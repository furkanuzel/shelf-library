using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LibraryManagement;

/// <summary>Shared helpers for Razor Pages: flash messages and sign-in.</summary>
public abstract class AppPage : PageModel
{
    [TempData] public string? Flash { get; set; }
    [TempData] public string? FlashKind { get; set; }

    protected void Notify(string message, string kind = "success")
    {
        Flash = message;
        FlashKind = kind;
    }

    protected int CurrentUserId =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    protected async Task SignInAsync(int id, string name, string email, string role)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Name, name),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role),
        }, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    /// <summary>Runs a business action; on rule violation shows the message instead of crashing.</summary>
    protected IActionResult Try(Action action, string success, string? redirectPage = null, object? routeValues = null)
    {
        try
        {
            action();
            Notify(success);
        }
        catch (LibraryException ex)
        {
            Notify(ex.Message, "error");
        }
        return redirectPage is null ? RedirectToPage(routeValues) : RedirectToPage(redirectPage, routeValues);
    }

    /// <summary>Only allow local return URLs (prevents open redirects).</summary>
    protected IActionResult RedirectLocal(string? returnUrl, string fallbackPage) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToPage(fallbackPage);
}

public static class Display
{
    /// <summary>Stable accent hue per title, used for the book "spine" colour.</summary>
    public static int Hue(string text)
    {
        var hash = 17;
        foreach (var ch in text) hash = unchecked(hash * 31 + ch);
        return Math.Abs(hash % 360);
    }

    public static string Initials(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(words.Take(2).Select(w => char.ToUpperInvariant(w[0])));
    }

    public static string Ago(DateTime utc)
    {
        var span = DateTime.UtcNow - DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours} h ago";
        return $"{(int)span.TotalDays} d ago";
    }
}

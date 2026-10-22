using LibraryManagement;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

var database = Database.FromEnvironment();
database.EnsureSchema();
var library = new LibraryService(database);

builder.Services.AddSingleton(database);
builder.Services.AddSingleton(library);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.Cookie.Name = "lms.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Roles.Admin, p => p.RequireRole(Roles.Admin));
    options.AddPolicy(Roles.Student, p => p.RequireRole(Roles.Student));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", Roles.Admin);
    options.Conventions.AuthorizePage("/MyLoans", Roles.Student);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseStaticFiles();

// First run: send everyone to /Setup until an admin account exists.
var setupDone = false;
app.Use(async (context, next) =>
{
    var path = context.Request.Path;
    if (!setupDone) setupDone = library.HasAnyAdmin();
    if (!setupDone && !path.StartsWithSegments("/Setup") && !Path.HasExtension(path.Value))
    {
        context.Response.Redirect("/Setup");
        return;
    }
    await next();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();

namespace LibraryManagement
{
    public static class Roles
    {
        public const string Admin = "Admin";
        public const string Student = "Student";
    }
}

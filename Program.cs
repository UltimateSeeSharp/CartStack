using System.Globalization;
using CartStack.Auth;
using CartStack.Components;
using CartStack.Configuration;
using CartStack.Data;
using CartStack.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

DotEnvLoader.Load(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

var culture = new CultureInfo("de-AT");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Db")));

// Fly terminates TLS at the edge and forwards plain HTTP to the container,
// so the app sees Request.IsHttps=false unless we trust X-Forwarded-Proto.
// Without this, SecurePolicy.SameAsRequest emits cookies WITHOUT the Secure
// flag, which iOS standalone-PWA Safari refuses to persist across launches
// (the cookie survives the session in memory but is dropped on app close).
builder.Services.Configure<ForwardedHeadersOptions>(opt =>
{
    opt.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    opt.KnownIPNetworks.Clear();
    opt.KnownProxies.Clear();
});

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.Cookie.Name = "cartstack.auth";
        opt.Cookie.HttpOnly = true;
        opt.Cookie.SameSite = SameSiteMode.Lax;
        opt.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        opt.LoginPath = "/login";
        opt.LogoutPath = "/auth/logout";
        opt.ExpireTimeSpan = TimeSpan.FromDays(365 * 10);
        opt.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<CurrentUserAccessor>();
builder.Services.AddSingleton<LoginTicketProtector>();

builder.Services.AddSingleton<ChangeBroadcaster>();
builder.Services.AddSingleton<NameSuggestionCache>();
builder.Services.AddScoped<IGroceryService, GroceryService>();

// Persist Data Protection keys on the same volume as the DB so cookies +
// LoginTicketProtector tickets survive machine restarts. Without this,
// every restart logs the whole family out and invalidates in-flight logins.
var dpKeysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dpKeysPath))
{
    Directory.CreateDirectory(dpKeysPath);
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath))
        .SetApplicationName("CartStack");
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await SeedData.EnsureSeededAsync(db, builder.Configuration);
}

if (!app.Environment.IsDevelopment())
{
    app.UseForwardedHeaders();
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Fly terminates TLS at the edge and forwards plain HTTP to the container,
// so UseHttpsRedirection() inside has nothing useful to redirect to and
// logs a noisy warning. force_https=true in fly.toml handles it at the edge.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapAuthEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

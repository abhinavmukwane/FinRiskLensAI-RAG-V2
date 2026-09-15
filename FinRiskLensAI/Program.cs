using Autofac;
using Autofac.Extensions.DependencyInjection;
using FinRiskLensAI.Common;
using FinRiskLensAI.Core.DI;
using FinRiskLensAI.Data.DI;
using FinRiskLensAI.ML.DI;
using FinRiskLensAI.Services.DI;
using Microsoft.AspNetCore.DataProtection;
using Serilog;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Don't advertise the web server. Kestrel writes this header itself, below the
// middleware pipeline, so it can only be suppressed here.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

//Serilog
builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext());

//Autofac
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
builder.Host.ConfigureContainer<ContainerBuilder>(container =>
{
    container.RegisterModule(new CoreModule());
    container.RegisterModule(new DataModule());
    container.RegisterModule(new ServicesModule());
    container.RegisterModule(new MLModule());

    // Settings POCOs bound from configuration (EmailService and friends take these directly)
    container.RegisterInstance(
        builder.Configuration.GetSection("Smtp").Get<FinRiskLensAI.Core.Models.Common.SmtpSettings>()
        ?? new FinRiskLensAI.Core.Models.Common.SmtpSettings());
    container.RegisterInstance(
        builder.Configuration.GetSection("Groq").Get<FinRiskLensAI.Core.Models.Common.GroqSettings>()
        ?? new FinRiskLensAI.Core.Models.Common.GroqSettings());
    container.RegisterInstance(
        builder.Configuration.GetSection("Finvu").Get<FinRiskLensAI.Core.Models.Common.FinvuSettings>()
        ?? new FinRiskLensAI.Core.Models.Common.FinvuSettings());
    container.RegisterInstance(
        builder.Configuration.GetSection("Signzy").Get<FinRiskLensAI.Core.Models.Common.SignzySettings>()
        ?? new FinRiskLensAI.Core.Models.Common.SignzySettings());
    container.RegisterInstance(
        builder.Configuration.GetSection("LoanCase").Get<FinRiskLensAI.Core.Models.LoanCase.LoanCaseSettings>()
        ?? new FinRiskLensAI.Core.Models.LoanCase.LoanCaseSettings());
});

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddAppDbContext(builder.Configuration);

// Builds an MSME's Udyam / MCA / GST / IP view models by UAN. Shared by the
// customer-facing controllers (UAN from session) and the bank portal (UAN from
// route), so both render identical data from one code path.
builder.Services.AddScoped<FinRiskLensAI.Common.CustomerProfileBuilder>();

// Persist DataProtection keys so session cookies survive app restarts / pool
// recycles — otherwise ephemeral keys regenerate and everyone is silently logged
// out (this bit us: the heavy first re-analyze can recycle the app pool).
var keysDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtectionKeys");
Directory.CreateDirectory(keysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysDir))
    .SetApplicationName("FinRiskLensAI");

// Back the session with the SQL Server distributed cache instead of in-memory.
// Otherwise an app-pool recycle (a heavy re-analyze can trigger one) or a web-farm
// instance switch wipes the in-memory session, and the next request silently logs
// the user out (redirected to Auth/CustLogin). SQL-backed session survives both.
// Table: [FinRiskLensAI].[SessionCache] — see the create SQL in HANDOFF.md.
builder.Services.AddDistributedSqlServerCache(o =>
{
    o.ConnectionString = builder.Configuration["Database:ConnectionStrings:SqlServer"];
    o.SchemaName = "FinRiskLensAI";
    o.TableName = "SessionCache";
});

builder.Services.AddSession(options =>
{
    options.Cookie.Name = "frlai.sid";
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;                          // no JS access
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // HTTPS only
    options.Cookie.SameSite = SameSiteMode.Strict;           // no cross-site send
});

// HSTS — one year, subdomains included. The ASP.NET Core default is only 30
// days; the hosting panel was also emitting its own header, which is why the
// live site showed a duplicate Strict-Transport-Security. The app owns it now,
// so the panel-level one must be turned off (see web.config).
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
    // Preload is deliberately left off: submitting to the preload list is hard
    // to reverse, so enable it only once HTTPS is settled on every subdomain.
});

var app = builder.Build();

// Security headers first, so static files and short-circuited responses get them too.
app.UseSecurityHeaders(app.Configuration, app.Environment);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

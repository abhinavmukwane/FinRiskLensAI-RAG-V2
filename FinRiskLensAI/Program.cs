using Autofac;
using Autofac.Extensions.DependencyInjection;
using FinRiskLensAI.Core.DI;
using FinRiskLensAI.Data.DI;
using FinRiskLensAI.ML.DI;
using FinRiskLensAI.Services.DI;
using Microsoft.AspNetCore.DataProtection;
using Serilog;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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

using FinRiskLensAI.Data.DbContextEDMX;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FinRiskLensAI.Data.DI
{
    public static class DataServiceCollectionExtensions
    {
        public static IServiceCollection AddAppDbContext(this IServiceCollection services, IConfiguration configuration)
        {
            var connectionString = configuration["Database:ConnectionStrings:PostgreSQL"]
                ?? throw new InvalidOperationException("Missing Database:ConnectionStrings:PostgreSQL in configuration.");

            services.AddDbContext<ApplicationDbContext>(options => PostgresSetup.Configure(options, connectionString));
            return services;
        }
    }

    /// <summary>
    /// The one place the PostgreSQL provider is configured. Runtime (above) and
    /// design-time (<see cref="ApplicationDbContextFactory"/>) both call it, so the
    /// model the migrations are generated from is the model the app runs against.
    /// </summary>
    public static class PostgresSetup
    {
        static PostgresSetup()
        {
            // ponytail: legacy timestamps. The codebase writes both DateTime.Now (Local)
            // and DateTime.UtcNow; modern Npgsql maps DateTime to timestamptz and rejects
            // Local, or to timestamp and rejects Utc. Legacy mode takes both, matching the
            // SQL Server datetime2 semantics this ported from. Upgrade path: make every
            // writer UtcNow, then delete this switch and let DateTime map to timestamptz.
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        }

        public static void Configure(DbContextOptionsBuilder options, string connectionString)
            => options
                .UseNpgsql(connectionString, npgsql =>
                {
                    npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "cre");
                    npgsql.UseVector();                    // pgvector — knowledge base embeddings
                })
                // All data access is LINQ, so snake_case costs nothing today and keeps the
                // hand-written retrieval and fraud-check SQL free of quoted identifiers.
                .UseSnakeCaseNamingConvention();
    }
}

using FinRiskLensAI.Core.Models.AccountAggregator;
using FinRiskLensAI.Core.Models.Admin;
using FinRiskLensAI.Core.Models.Cases;
using FinRiskLensAI.Core.Models.Common;
using FinRiskLensAI.Core.Models.GST;
using FinRiskLensAI.Core.Models.Knowledge;
using FinRiskLensAI.Core.Models.Onboarding;
using FinRiskLensAI.Core.Models.Universal;
using FinRiskLensAI.Core.Models.User_Activity;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace FinRiskLensAI.Data.DbContextEDMX
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<AdmLogin> AdmLogins => Set<AdmLogin>();
        public DbSet<AdmBankLogin> AdmBankLogins => Set<AdmBankLogin>();
        public DbSet<FinRiskLensAI.Core.Models.LoanCase.LoanCasePush> LoanCasePushes => Set<FinRiskLensAI.Core.Models.LoanCase.LoanCasePush>();
        public DbSet<MsmeEnquiry> MsmeEnquiries => Set<MsmeEnquiry>();
        public DbSet<MsmeLocation> MsmeLocations => Set<MsmeLocation>();
        public DbSet<MsmeNicCode> MsmeNicCodes => Set<MsmeNicCode>();
        public DbSet<StaticResponseModel> StaticResponseModel => Set<StaticResponseModel>();
        public DbSet<UserRegistrationModel> UserRegistration => Set<UserRegistrationModel>();
        public DbSet<UserOtpModel> UserOtpModel => Set<UserOtpModel>();
        public DbSet<AccAggreTokenModel> AccAggreTokens => Set<AccAggreTokenModel>();
        public DbSet<AAConsentReqModel> AAConsentRequests => Set<AAConsentReqModel>();
        public DbSet<GSTR2And3BResponceModel> GSTR2And3BResponces => Set<GSTR2And3BResponceModel>();

        // RAG knowledge base — Doc/11_RAG_ARCHITECTURE.md
        public DbSet<KnowledgeDoc> KnowledgeDocs => Set<KnowledgeDoc>();
        public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
        public DbSet<ChunkEmbedding> ChunkEmbeddings => Set<ChunkEmbedding>();
        public DbSet<RagQueryLog> RagQueryLogs => Set<RagQueryLog>();

        // Cases — Doc/10_FRAUD_LENS_BUILD.md §4
        public DbSet<LoanApplication> LoanApplications => Set<LoanApplication>();
        public DbSet<CaseDocument> CaseDocuments => Set<CaseDocument>();
        public DbSet<StatementRow> StatementRows => Set<StatementRow>();
        public DbSet<PageSummary> PageSummaries => Set<PageSummary>();
        public DbSet<DocField> DocFields => Set<DocField>();
        public DbSet<Finding> Findings => Set<Finding>();
        public DbSet<Evidence> Evidence => Set<Evidence>();
        public DbSet<AiDropLog> AiDropLogs => Set<AiDropLog>();

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // Default precision for all decimals — keeps both SQL Server and PostgreSQL happy.
            configurationBuilder.Properties<decimal>().HavePrecision(18, 4);
            base.ConfigureConventions(configurationBuilder);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("vector");   // pgvector — created by the baseline migration
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }

        public override Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            SetAuditableFields();
            return base.SaveChangesAsync(ct);
        }

        private void SetAuditableFields()
        {
            var now = DateTime.UtcNow;
            foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
            {
                if (entry.State == EntityState.Added)
                    entry.Entity.CreatedAt = now;
                if (entry.State is EntityState.Added or EntityState.Modified)
                    entry.Entity.UpdatedAt = now;
            }
        }
    }
}

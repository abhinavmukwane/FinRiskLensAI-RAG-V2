using Autofac;
using FinRiskLensAI.Core.Interfaces;
using FinRiskLensAI.Fraud.Ai;
using FinRiskLensAI.Fraud.Pipeline;
using FinRiskLensAI.Fraud.Storage;
using Microsoft.Extensions.Configuration;

namespace FinRiskLensAI.Fraud.DI
{
    public class FraudModule : Module
    {
        private readonly IConfiguration _configuration;

        public FraudModule(IConfiguration configuration) => _configuration = configuration;

        protected override void Load(ContainerBuilder builder)
        {
            // Services by convention (matches the ServicesModule pattern)
            builder.RegisterAssemblyTypes(ThisAssembly)
                   .Where(t => t.Name.EndsWith("Service"))
                   .AsImplementedInterfaces().InstancePerLifetimeScope();

            // Gemini — model names are config, the project comes from Gcp:ProjectId
            var gemini = _configuration.GetSection("Gemini").Get<GeminiOptions>() ?? new GeminiOptions();
            gemini.ProjectId = _configuration["Gcp:ProjectId"] ?? string.Empty;
            builder.RegisterInstance(gemini);
            builder.RegisterType<GeminiClient>().AsSelf().SingleInstance();
            builder.RegisterType<DocumentExtractor>().AsSelf().SingleInstance();

            builder.RegisterType<CaseQueue>().AsSelf().SingleInstance();

            // Case documents in Cloud Storage when a bucket is configured. Registered after the
            // ML module, so this replaces its Azure registration for ICaseDocumentStore only —
            // the MSME source data (IMsmeDataStore) stays where it is.
            var bucket = _configuration["Gcs:CasesBucket"];
            if (!string.IsNullOrWhiteSpace(bucket))
                builder.Register(_ => new GcsDocumentStore(bucket)).As<ICaseDocumentStore>().SingleInstance();
        }
    }
}

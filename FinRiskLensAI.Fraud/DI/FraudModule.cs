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

        private string Bucket(string key) => _configuration[key] is { Length: > 0 } b
            ? b : throw new InvalidOperationException($"{key} is not configured.");

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
            builder.RegisterType<Investigator>().AsSelf().InstancePerLifetimeScope();

            builder.RegisterType<CaseQueue>().AsSelf().SingleInstance();

            // Both stores live in Cloud Storage (Mumbai): uploaded case documents, and the
            // verification sources each MSME's checks run against. Without a bucket the app
            // fails on first use with the setting to fix, rather than falling back somewhere else.
            builder.Register(_ => new GcsDocumentStore(Bucket("Gcs:CasesBucket"))).As<ICaseDocumentStore>().SingleInstance();
            builder.Register(_ => new GcsMsmeDataStore(Bucket("Gcs:SourcesBucket"))).As<IMsmeDataStore>().SingleInstance();
        }
    }
}

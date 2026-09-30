using FinRiskLensAI.Core.Interfaces;
using Autofac;
using FinRiskLensAI.ML.Features;
using FinRiskLensAI.ML.Storage;

namespace FinRiskLensAI.ML.DI
{
    public class MLModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Feature extractors — stateless parsers for the verification sources
            builder.RegisterType<UdyamFeatureExtractor>().AsSelf().SingleInstance();
            builder.RegisterType<ItrFeatureExtractor>().AsSelf().SingleInstance();
            builder.RegisterType<GstFeatureExtractor>().AsSelf().SingleInstance();
            builder.RegisterType<AaFeatureExtractor>().AsSelf().SingleInstance();

            // Blob storage — container client is created lazily on first use
            builder.RegisterType<AzureBlobDataStore>().As<IMsmeDataStore>().SingleInstance();

            // Services by convention (matches the ServicesModule pattern)
            builder.RegisterAssemblyTypes(ThisAssembly)
                   .Where(t => t.Name.EndsWith("Service"))
                   .AsImplementedInterfaces().InstancePerLifetimeScope();
        }
    }
}

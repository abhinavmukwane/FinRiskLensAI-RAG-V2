using Autofac;
using FinRiskLensAI.ML.Features;

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

            // Storage is registered by the Fraud module: Cloud Storage for both stores

            // Services by convention (matches the ServicesModule pattern)
            builder.RegisterAssemblyTypes(ThisAssembly)
                   .Where(t => t.Name.EndsWith("Service"))
                   .AsImplementedInterfaces().InstancePerLifetimeScope();
        }
    }
}

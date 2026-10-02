using Autofac;

namespace FinRiskLensAI.Fraud.DI
{
    public class FraudModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Services by convention (matches the ServicesModule pattern)
            builder.RegisterAssemblyTypes(ThisAssembly)
                   .Where(t => t.Name.EndsWith("Service"))
                   .AsImplementedInterfaces().InstancePerLifetimeScope();
        }
    }
}

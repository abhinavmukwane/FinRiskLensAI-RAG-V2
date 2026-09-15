using Autofac;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FinRiskLensAI.Services.DI
{
    public class ServicesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // ── Services — scan and register all at once
            builder.RegisterAssemblyTypes(ThisAssembly)
                   .Where(t => t.Name.EndsWith("Service"))
                   .AsImplementedInterfaces()
                   .InstancePerLifetimeScope();

            // ── Loan-case payload builders: one per downstream channel. They are
            //    resolved as IEnumerable<ILoanCasePayloadBuilder> by LoanCaseService,
            //    so a new channel is one class here and nothing else.
            builder.RegisterAssemblyTypes(ThisAssembly)
                   .Where(t => t.Name.EndsWith("PayloadBuilder"))
                   .As<FinRiskLensAI.Core.Interfaces.IServices.LoanCase.ILoanCasePayloadBuilder>()
                   .InstancePerLifetimeScope();

            // ── Or register individually
            // builder.RegisterType<ProductService>().As<IProductService>().InstancePerLifetimeScope();
            // builder.RegisterType<OrderService>().As<IOrderService>().InstancePerLifetimeScope();
        }
    }
}

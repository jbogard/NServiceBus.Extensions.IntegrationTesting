using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NServiceBus.Pipeline;

namespace NServiceBus.Extensions.IntegrationTesting
{
    internal class AttachInvokeHandlerContextToActivity : Behavior<IInvokeHandlerContext>
    {
        public override Task Invoke(
            IInvokeHandlerContext context,
            Func<Task> next
        )
        {
            Activity.Current?.AddTag("testing.invoke.handler.context", context);
            return next();
        }
    }
}
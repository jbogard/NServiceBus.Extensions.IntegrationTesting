using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NServiceBus.Pipeline;

namespace NServiceBus.Extensions.IntegrationTesting
{
    internal class AttachIncomingLogicalMessageContextToActivity : Behavior<IIncomingLogicalMessageContext>
    {
        public override Task Invoke(
            IIncomingLogicalMessageContext context,
            Func<Task> next
        )
        {
            Activity.Current?.AddTag("testing.incoming.message.context", context);
            return next();
        }
    }
}
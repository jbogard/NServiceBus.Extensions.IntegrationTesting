using System;
using System.Diagnostics;
using System.Threading.Tasks;
using NServiceBus.Pipeline;

namespace NServiceBus.Extensions.IntegrationTesting
{
    internal class AttachOutgoingLogicalMessageContextToActivity : Behavior<IOutgoingLogicalMessageContext>
    {
        public override Task Invoke(
            IOutgoingLogicalMessageContext context,
            Func<Task> next
        )
        {
            Activity.Current?.AddTag("testing.outgoing.message.context", context);
            return next();
        }
    }
}
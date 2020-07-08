using System.Collections.Generic;
using System.Linq;
using NServiceBus.Pipeline;

namespace NServiceBus.Extensions.IntegrationTesting
{
    public class ObservedMessageContexts
    {
        public IReadOnlyList<IIncomingLogicalMessageContext> IncomingMessageContexts { get; }
        public IReadOnlyList<IOutgoingLogicalMessageContext> OutgoingMessageContexts { get; }

        public IEnumerable<object> SentMessages => OutgoingMessageContexts.Select(c => c.Message.Instance);
        public IEnumerable<object> ReceivedMessages => IncomingMessageContexts.Select(c => c.Message.Instance);

        public ObservedMessageContexts(
            IReadOnlyList<IIncomingLogicalMessageContext> incomingMessageContexts,
            IReadOnlyList<IOutgoingLogicalMessageContext> outgoingMessageContexts)
        {
            IncomingMessageContexts = incomingMessageContexts;
            OutgoingMessageContexts = outgoingMessageContexts;
        }
    }
}
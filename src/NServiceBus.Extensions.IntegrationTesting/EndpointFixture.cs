using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NServiceBus.Extensions.Diagnostics;
using NServiceBus.Pipeline;

namespace NServiceBus.Extensions.IntegrationTesting
{
    public class EndpointFixture : IDisposable
    {
        private IDisposable _allListenerSubscription;

        public Task<ObservedMessageContexts> ExecuteAndWaitForHandled<TMessageHandled>(
            Func<Task> testAction,
            TimeSpan? timeout = null) =>
            ExecuteAndWait<IIncomingLogicalMessageContext>(
                testAction, 
                m => m.Message.MessageType == typeof(TMessageHandled),
                timeout);

        public Task<ObservedMessageContexts> ExecuteAndWaitForSent<TMessage>(
            Func<Task> testAction,
            TimeSpan? timeout = null) =>
            ExecuteAndWait<IOutgoingLogicalMessageContext>(
                testAction,
                m => m.Message.MessageType == typeof(TMessage),
                timeout);

        public Task<ObservedMessageContexts> ExecuteAndWait(
            Func<Task> testAction,
            Func<IIncomingLogicalMessageContext, bool> incomingPredicate,
            TimeSpan? timeout = null) => 
            ExecuteAndWait<IIncomingLogicalMessageContext>(testAction, incomingPredicate,  timeout);

        public Task<ObservedMessageContexts> ExecuteAndWait(
            Func<Task> testAction,
            Func<IOutgoingLogicalMessageContext, bool> outgoingPredicate,
            TimeSpan? timeout = null) => 
            ExecuteAndWait<IOutgoingLogicalMessageContext>(testAction, outgoingPredicate, timeout);

        private async Task<ObservedMessageContexts> ExecuteAndWait<TMessageContext>(
            Func<Task> testAction,
            Func<TMessageContext, bool> predicate,
            TimeSpan? timeout = null)
            where TMessageContext : IPipelineContext
        {
            timeout = Debugger.IsAttached
                ? (TimeSpan?)null
                : timeout ?? TimeSpan.FromSeconds(10);

            var incomingMessageContexts = new List<IIncomingLogicalMessageContext>();
            var outgoingMessageContexts = new List<IOutgoingLogicalMessageContext>();
            var obs = Observable.Empty<object>();

            _allListenerSubscription = DiagnosticListener.AllListeners
                .Subscribe(listener =>
                {
                    switch (listener.Name)
                    {
                        case ActivityNames.IncomingLogicalMessage:
                            var incomingObs = listener
                                .Select(e => e.Value)
                                .Cast<IIncomingLogicalMessageContext>();

                            incomingObs.Subscribe(incomingMessageContexts.Add);

                            if (typeof(TMessageContext) ==  typeof(IIncomingLogicalMessageContext))
                            {
                                obs = obs.Merge(incomingObs);
                            }

                            break;
                        case ActivityNames.OutgoingLogicalMessage:
                            var outgoingObs = listener
                                .Select(e => e.Value)
                                .Cast<IOutgoingLogicalMessageContext>();

                            outgoingObs.Subscribe(outgoingMessageContexts.Add);

                            if (typeof(TMessageContext) == typeof(IOutgoingLogicalMessageContext))
                            {
                                obs = obs.Merge(outgoingObs);
                            }

                            break;
                    }
                });

            var finalObs = obs.Cast<TMessageContext>().TakeUntil(predicate);
            if (timeout != null)
            {
                finalObs = finalObs.Timeout(timeout.Value);
            }

            await testAction();

            // Force the observable to complete
            await finalObs;

            return new ObservedMessageContexts(incomingMessageContexts, outgoingMessageContexts);
        }

        public void Dispose()
        {
            _allListenerSubscription?.Dispose();
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
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
            ExecuteAndWait(
                testAction, 
                m => m.Message.MessageType == typeof(TMessageHandled),
                null,
                timeout);

        public Task<ObservedMessageContexts> ExecuteAndWaitForSent<TMessage>(
            Func<Task> testAction,
            TimeSpan? timeout = null) =>
            ExecuteAndWait(
                testAction,
                null,
                m => m.Message.MessageType == typeof(TMessage),
                timeout);

        public Task<ObservedMessageContexts> ExecuteAndWait(
            Func<Task> testAction,
            Func<IIncomingLogicalMessageContext, bool> incomingPredicate,
            TimeSpan? timeout = null) => 
            ExecuteAndWait(testAction, incomingPredicate, null, timeout);

        public Task<ObservedMessageContexts> ExecuteAndWait(
            Func<Task> testAction,
            Func<IOutgoingLogicalMessageContext, bool> outgoingPredicate,
            TimeSpan? timeout = null) => 
            ExecuteAndWait(testAction, null, outgoingPredicate, timeout);

        public async Task<ObservedMessageContexts> ExecuteAndWait(
            Func<Task> testAction,
            Func<IIncomingLogicalMessageContext, bool> incomingPredicate,
            Func<IOutgoingLogicalMessageContext, bool> outgoingPredicate,
            TimeSpan? timeout = null)
        {
            timeout ??= Debugger.IsAttached
                ? (TimeSpan?)null
                : TimeSpan.FromSeconds(10);

            var incomingMessageContexts = new List<IIncomingLogicalMessageContext>();
            var outgoingMessageContexts = new List<IOutgoingLogicalMessageContext>();
            IObservable<IPipelineContext> obs = null;

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

                            if (incomingPredicate != null)
                            {
                                obs = incomingObs.TakeUntil(incomingPredicate).Cast<IPipelineContext>();

                                if (timeout != null)
                                {
                                    obs = obs.Timeout(timeout.Value);
                                }
                            }

                            break;
                        case ActivityNames.OutgoingLogicalMessage:
                            var outgoingObs = listener
                                .Select(e => e.Value)
                                .Cast<IOutgoingLogicalMessageContext>();

                            outgoingObs.Subscribe(outgoingMessageContexts.Add);

                            if (outgoingPredicate != null)
                            {
                                obs = outgoingObs.TakeUntil(outgoingPredicate).Cast<IPipelineContext>();

                                if (timeout != null)
                                {
                                    obs = obs.Timeout(timeout.Value);
                                }
                            }

                            break;
                    }
                });

            await testAction();

            // Force the observable
            foreach (var _ in obs?.ToEnumerable()) { }

            return new ObservedMessageContexts(incomingMessageContexts, outgoingMessageContexts);
        }

        public void Dispose()
        {
            _allListenerSubscription?.Dispose();
        }
    }
}
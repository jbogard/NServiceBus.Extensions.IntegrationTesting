using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading.Tasks;
using NServiceBus.Extensions.Diagnostics;
using NServiceBus.Pipeline;
using NServiceBus.Sagas;

namespace NServiceBus.Extensions.IntegrationTesting
{
    public static class EndpointFixture
    {
        public static Task<ObservedMessageContexts> ExecuteAndWaitForHandled<TMessageHandled>(
            Func<Task> testAction,
            TimeSpan? timeout = null) =>
            ExecuteAndWait<IIncomingLogicalMessageContext>(
                testAction, 
                m => m.Message.MessageType == typeof(TMessageHandled),
                timeout);

        public static Task<ObservedMessageContexts> ExecuteAndWaitForSent<TMessage>(
            Func<Task> testAction,
            TimeSpan? timeout = null) =>
            ExecuteAndWait<IOutgoingLogicalMessageContext>(
                testAction,
                m => m.Message.MessageType == typeof(TMessage),
                timeout);

        public static Task<ObservedMessageContexts> ExecuteAndWaitForSagaCompletion<TSaga>(
            Func<Task> testAction,
            TimeSpan? timeout = null) => ExecuteAndWait<IInvokeHandlerContext>(
            testAction,
            m =>
            {
                if (m.MessageHandler.HandlerType != typeof(TSaga)) return false;
                
                if (m.Extensions.TryGet(out ActiveSagaInstance saga))
                {
                    return !saga.NotFound && saga.Instance.Completed;
                }
                return false;
            }, timeout);
        
        public static Task<ObservedMessageContexts> ExecuteAndWait(
            Func<Task> testAction,
            Func<IIncomingLogicalMessageContext, bool> incomingPredicate,
            TimeSpan? timeout = null) => 
            ExecuteAndWait<IIncomingLogicalMessageContext>(testAction, incomingPredicate,  timeout);

        public static Task<ObservedMessageContexts> ExecuteAndWait(
            Func<Task> testAction,
            Func<IOutgoingLogicalMessageContext, bool> outgoingPredicate,
            TimeSpan? timeout = null) => 
            ExecuteAndWait<IOutgoingLogicalMessageContext>(testAction, outgoingPredicate, timeout);

        private static async Task<ObservedMessageContexts> ExecuteAndWait<TMessageContext>(
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
            var invokeHandlerContexts = new List<IInvokeHandlerContext>();
            
            var obs = Observable.Empty<object>();

            using var allListenerSubscription = DiagnosticListener.AllListeners
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
                        case ActivityNames.InvokedHandler:
                            var invokeHandlerObs = listener.Select(e => e.Value).Cast<IInvokeHandlerContext>();
                            invokeHandlerObs.Subscribe(invokeHandlerContexts.Add);

                            if (typeof(TMessageContext) == typeof(IInvokeHandlerContext))
                            {
                                obs = obs.Merge(invokeHandlerObs);
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

            return new ObservedMessageContexts(
                incomingMessageContexts, 
                outgoingMessageContexts,
                invokeHandlerContexts);
        }
    }
}
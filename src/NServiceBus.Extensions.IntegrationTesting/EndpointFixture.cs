using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
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
            ExecuteAndWait<IIncomingLogicalMessageContext>(testAction, incomingPredicate, timeout);

        public static Task<ObservedMessageContexts> ExecuteAndWait(
            Func<Task> testAction,
            Func<IOutgoingLogicalMessageContext, bool> outgoingPredicate,
            TimeSpan? timeout = null) =>
            ExecuteAndWait<IOutgoingLogicalMessageContext>(testAction, outgoingPredicate, timeout);

        private static async Task<ObservedMessageContexts> ExecuteAndWait<TMessageContext>(
            Func<Task> testAction,
            Func<TMessageContext, bool> predicate,
            TimeSpan? timeout = null)
            where TMessageContext : class, IPipelineContext
        {
            timeout = Debugger.IsAttached
                ? (TimeSpan?)null
                : timeout ?? TimeSpan.FromSeconds(10);

            var incomingMessageContexts = new List<IIncomingLogicalMessageContext>();
            var outgoingMessageContexts = new List<IOutgoingLogicalMessageContext>();
            var invokeHandlerContexts = new List<IInvokeHandlerContext>();
            var subscriptions = new List<IDisposable>();

            var messageReceivingTaskSource = new TaskCompletionSource<object>();

            // https://github.com/dotnet/runtime/blob/eccafbac942be9e3b06d48cff735fd6e50c3f25a/src/libraries/System.Diagnostics.DiagnosticSource/tests/ActivitySourceTests.cs#L134
            using ActivityListener listener = new();
            listener.ActivityStopped = (activitySource) =>
            {
                switch (activitySource.OperationName)
                {
                    case NsbActivityNames.IncomingMessageActivityName:
                        var context = activitySource.GetTagItem("testing.incoming.message.context") as IIncomingLogicalMessageContext;
                        
                        incomingMessageContexts.Add(context);

                        if (context is TMessageContext ctx && predicate(ctx))
                        {
                            messageReceivingTaskSource.SetResult(null);
                        }

                        break;
                    case NsbActivityNames.OutgoingMessageActivityName:
                    case NsbActivityNames.PublishMessageActivityName:
                        var outgoingContext = activitySource.GetTagItem("testing.outgoing.message.context") as IOutgoingLogicalMessageContext;

                        outgoingMessageContexts.Add(outgoingContext);

                        if (outgoingContext is TMessageContext ctx2 && predicate(ctx2))
                        {
                            messageReceivingTaskSource.SetResult(null);
                        }


                        break;
                    case NsbActivityNames.InvokeHandlerActivityName:
                        var handlerContext = activitySource.Parent.GetTagItem("testing.invoke.handler.context") as IInvokeHandlerContext;
                     
                        invokeHandlerContexts.Add(handlerContext);

                        if (handlerContext is TMessageContext ctx3 && predicate(ctx3))
                        {
                            messageReceivingTaskSource.SetResult(null);
                        }


                        break;
                }
            };
            listener.ShouldListenTo = _ => true;
            listener.Sample = (ref ActivityCreationOptions<ActivityContext> activityOptions) => ActivitySamplingResult.AllData;

            ActivitySource.AddActivityListener(listener);



            await testAction();

            if (timeout.HasValue)
            {
                var timeoutTask = Task.Delay(timeout.Value);
                var finishedTask = await Task.WhenAny(messageReceivingTaskSource.Task, timeoutTask);
                if (finishedTask == timeoutTask)
                {
                    throw new TimeoutException();
                }
            }
            else
            {
                await messageReceivingTaskSource.Task;
            }

            // Wait for either a timeout or a message
            await messageReceivingTaskSource.Task;

            // clean up all active subscriptions
            subscriptions.ForEach(x => x.Dispose());

            return new ObservedMessageContexts(
                incomingMessageContexts,
                outgoingMessageContexts,
                invokeHandlerContexts);
        }
    }
}
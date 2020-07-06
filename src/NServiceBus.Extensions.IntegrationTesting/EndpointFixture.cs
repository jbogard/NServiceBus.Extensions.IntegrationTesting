using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using NServiceBus.Extensions.Diagnostics;
using NServiceBus.Pipeline;

namespace NServiceBus.Extensions.IntegrationTesting
{
    public class EndpointFixture : IDisposable
    {
        private IDisposable _allListenerSubscription;
        private IDisposable _listenerSubscriber;

        public async Task<IEnumerable<TMessageHandled>> ExecuteAndWaitForHandled<TMessageHandled>(
            Func<Task> testAction,
            TimeSpan? timeout = null)
        {
            var values = await ExecuteAndWait<IOutgoingLogicalMessageContext>(
                testAction, 
                m => m.Message.MessageType == typeof(TMessageHandled),
                timeout);

            return values
                .Select(m => m.Message.Instance)
                .OfType<TMessageHandled>();
        }

        public async Task<IEnumerable<TMessage>> ExecuteAndWaitForSent<TMessage>(
            Func<Task> testAction,
            TimeSpan? timeout = null)
        {
            var values = await ExecuteAndWait<IOutgoingLogicalMessageContext>(
                testAction, 
                m => m.Message.MessageType == typeof(TMessage),
                timeout);

            return values
                .Select(m => m.Message.Instance)
                .OfType<TMessage>();
        }

        public async Task<IEnumerable<TDiagnosticEvent>> ExecuteAndWait<TDiagnosticEvent>(
            Func<Task> testAction,
            Func<TDiagnosticEvent, bool> predicate,
            TimeSpan? timeout = null)
        {
            timeout ??= Debugger.IsAttached
                ? (TimeSpan?)null
                : TimeSpan.FromSeconds(10);

            string eventName = null;
            IConnectableObservable<KeyValuePair<string, object>> diagnosticListener = null;

            if (typeof(TDiagnosticEvent) == typeof(IIncomingLogicalMessageContext))
            {
                eventName = ActivityNames.IncomingLogicalMessage;
            }
            else if (typeof(TDiagnosticEvent) == typeof(IOutgoingLogicalMessageContext))
            {
                eventName = ActivityNames.OutgoingLogicalMessage;
            }

            if (eventName != null)
            {
                _allListenerSubscription = DiagnosticListener.AllListeners
                    .Where(l => l.Name == eventName)
                    .Subscribe(listener => diagnosticListener = listener.Publish());
            }

            _listenerSubscriber = diagnosticListener?.Subscribe();
            diagnosticListener?.Connect();

            var obs = diagnosticListener
                ?.Select(e => e.Value)
                .Cast<TDiagnosticEvent>()
                .TakeUntil(predicate);

            if (timeout != null)
            {
                obs = obs?.Timeout(timeout.Value);
            }

            await testAction();

            return obs?.ToEnumerable() ?? Enumerable.Empty<TDiagnosticEvent>();
        }

        public void Dispose()
        {
            _allListenerSubscription?.Dispose();
            _listenerSubscriber?.Dispose();
        }
    }
}
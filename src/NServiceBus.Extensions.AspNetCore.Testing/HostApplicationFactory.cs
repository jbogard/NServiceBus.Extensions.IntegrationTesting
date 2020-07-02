using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus.Extensions.Diagnostics;
using NServiceBus.Pipeline;

namespace NServiceBus.Extensions.AspNetCore.Testing
{
    public class HostApplicationFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint> 
        where TEntryPoint : class
    {
        private IDisposable _allListenerSubscription;
        private IDisposable _listenerSubscriber;

        public async Task<IEnumerable<TMessageHandled>> SendAndWaitForHandled<TMessageHandled>(object commandToSend, TimeSpan? timeout = null)
        {
            var values = await ExecuteAndWait<IOutgoingLogicalMessageContext>(
                session => session.Send(commandToSend),
                m => m.Message.MessageType == typeof(TMessageHandled),
                timeout);

            return values
                .Select(m => m.Message.Instance)
                .OfType<TMessageHandled>();
        }

        public async Task<IEnumerable<TMessageHandled>> PublishAndWaitForHandled<TMessageHandled>(object eventToPublish, TimeSpan? timeout = null)
        {
            var values = await ExecuteAndWait<IOutgoingLogicalMessageContext>(
                session => session.Publish(eventToPublish),
                m => m.Message.MessageType == typeof(TMessageHandled),
                timeout);

            return values
                .Select(m => m.Message.Instance)
                .OfType<TMessageHandled>();
        }

        public async Task<IEnumerable<TMessageHandled>> ExecuteAndWaitForHandled<TMessageHandled>(
            Func<IMessageSession, Task> sendAction,
            TimeSpan? timeout = null)
        {
            var values = await ExecuteAndWait<IOutgoingLogicalMessageContext>(
                sendAction,
                m => m.Message.MessageType == typeof(TMessageHandled),
                timeout);

            return values
                .Select(m => m.Message.Instance)
                .OfType<TMessageHandled>();
        }

        public async Task<IEnumerable<TMessage>> ExecuteAndWaitForSent<TMessage>(
            Func<IMessageSession, Task> sendAction,
            TimeSpan? timeout = null)
        {
            var values = await ExecuteAndWait<IOutgoingLogicalMessageContext>(
                sendAction,
                m => m.Message.MessageType == typeof(TMessage),
                timeout);

            return values
                .Select(m => m.Message.Instance)
                .OfType<TMessage>();
        }

        public async Task<IEnumerable<TDiagnosticEvent>> ExecuteAndWait<TDiagnosticEvent>(
            Func<IMessageSession, Task> sendAction,
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

            var session = Services.GetService<IMessageSession>();

            await sendAction(session);

            return obs?.ToEnumerable() ?? Enumerable.Empty<TDiagnosticEvent>();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _allListenerSubscription?.Dispose();
                _listenerSubscriber?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
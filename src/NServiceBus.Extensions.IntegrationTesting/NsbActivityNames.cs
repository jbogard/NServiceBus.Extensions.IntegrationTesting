namespace NServiceBus.Extensions.IntegrationTesting
{
    internal static class NsbActivityNames
    {
        public const string IncomingMessageActivityName = "NServiceBus.Diagnostics.ReceiveMessage";

        public const string OutgoingMessageActivityName = "NServiceBus.Diagnostics.SendMessage";

        public const string OutgoingEventActivityName = "NServiceBus.Diagnostics.PublishMessage";

        public const string SubscribeActivityName = "NServiceBus.Diagnostics.Subscribe";

        public const string UnsubscribeActivityName = "NServiceBus.Diagnostics.Unsubscribe";

        public const string InvokeHandlerActivityName = "NServiceBus.Diagnostics.InvokeHandler";
    }
}
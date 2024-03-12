namespace NServiceBus.Extensions.IntegrationTesting
{
    internal static class NsbActivityNames
    {
        public const string IncomingMessageActivityName = "NServiceBus.Diagnostics.ReceiveMessage";

        public const string OutgoingMessageActivityName = "NServiceBus.Diagnostics.SendMessage";

        public const string PublishMessageActivityName = "NServiceBus.Diagnostics.PublishMessage";

        public const string InvokeHandlerActivityName = "NServiceBus.Diagnostics.InvokeHandler";
    }
}
using System;

namespace NServiceBus.Extensions.IntegrationTesting
{
    public static class EndpointConfigurationExtensions
    {
        /// <summary>
        /// Configure your endpoint for integration by configuring:
        /// - Transport to be the learning transport
        /// - Turn off auditing
        /// - Purge queues at startup
        /// - Turn off all retries
        /// </summary>
        /// <param name="endpoint">Endpoint configuration</param>
        /// <returns>Endpoint configuration</returns>
        public static EndpointConfiguration ConfigureTestEndpoint(this EndpointConfiguration endpoint)
            => endpoint.ConfigureTestEndpoint(null);

        /// <summary>
        /// Configure your endpoint for integration by configuring:
        /// - Transport to be the learning transport
        /// - Turn off auditing
        /// - Purge queues at startup
        /// - Turn off all retries
        /// </summary>
        /// <param name="endpoint">Endpoint configuration</param>
        /// <param name="transportConfigurationAction">Transport configuration action</param>
        /// <returns>Endpoint configuration</returns>
        public static EndpointConfiguration ConfigureTestEndpoint(this EndpointConfiguration endpoint, 
            Action<TransportExtensions<LearningTransport>> transportConfigurationAction)
        {
            var transport = endpoint.UseTransport<LearningTransport>();

            transportConfigurationAction?.Invoke(transport);

            endpoint.PurgeOnStartup(true);
            endpoint.DisableFeature<Features.Audit>();

            endpoint.Pipeline.Register(
                new AttachIncomingLogicalMessageContextToActivity(),
                "Attach incoming logical message context as OpenTelemetry tags");

            endpoint.Pipeline.Register(
                new AttachOutgoingLogicalMessageContextToActivity(),
                "Attach Outgoing Logical Message Context as OpenTelemetry tags");

            endpoint.Pipeline.Register(
                new AttachInvokeHandlerContextToActivity(),
                "Attach invoke handler Context as OpenTelemetry tags");

            endpoint
                .Recoverability()
                .Immediate(i => i.NumberOfRetries(0))
                .Delayed(d => d.NumberOfRetries(0));

            return endpoint;
        }
    }
}
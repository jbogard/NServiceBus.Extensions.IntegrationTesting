using System;
using NServiceBus.Features;

namespace NServiceBus.Extensions.AspNetCore.Testing
{
    public static class EndpointConfigurationExtensions
    {
        public static EndpointConfiguration ConfigureTestEndpoint(this EndpointConfiguration endpoint)
            => endpoint.ConfigureTestEndpoint(null);

        public static EndpointConfiguration ConfigureTestEndpoint(this EndpointConfiguration endpoint, 
            Action<TransportExtensions<LearningTransport>> transportConfigurationAction)
        {
            var transport = endpoint.UseTransport<LearningTransport>();

            transportConfigurationAction?.Invoke(transport);

            endpoint.PurgeOnStartup(true);
            endpoint.DisableFeature<Audit>();
            endpoint
                .Recoverability()
                .Immediate(i => i.NumberOfRetries(0))
                .Delayed(d => d.NumberOfRetries(0));

            return endpoint;
        }
    }
}
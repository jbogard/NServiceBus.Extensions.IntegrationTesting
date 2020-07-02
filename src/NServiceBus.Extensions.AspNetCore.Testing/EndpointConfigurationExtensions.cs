using NServiceBus.Features;

namespace NServiceBus.Extensions.AspNetCore.Testing
{
    public static class EndpointConfigurationExtensions
    {
        public static EndpointConfiguration ConfigureTestEndpoint(this EndpointConfiguration endpoint)
        {
            endpoint.UseTransport<LearningTransport>();
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
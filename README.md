# NServiceBus.Extensions.Diagnostics

![CI](https://github.com/jbogard/NServiceBus.Extensions.Diagnostics/workflows/CI/badge.svg)

## Usage

This repo includes two packages:

 - [NServiceBus.Extensions.Diagnostics](https://www.nuget.org/packages/NServiceBus.Extensions.Diagnostics/)
 - [NServiceBus.Extensions.Diagnostics.OpenTelemetry](https://www.nuget.org/packages/NServiceBus.Extensions.Diagnostics.OpenTelemetry/)
 
The `NServiceBus.Extensions.Diagnostics` package extends NServiceBus to expose telemetry information via `System.Diagnostics`.

The `NServiceBus.Extensions.Diagnostics.OpenTelemetry` package provides adapters to [OpenTelemetry](https://opentelemetry.io/).

To use `NServiceBus.Extensions.Diagnostics`, simply reference the package. The `DiagnosticsFeature` is enabled by default.

The Diagnostics package exposes four different events from [behaviors](https://docs.particular.net/nservicebus/pipeline/manipulate-with-behaviors) via Diagnostics:

 - IIncomingPhysicalMessageContext
 - IIncomingLogicalMessageContext
 - IOutgoingPhysicalMessageContext
 - IOutgoingLogicalMessageContext
 
The Physical message variants include full Activity support. All diagnostics events pass through the corresponding [context object](https://docs.particular.net/nservicebus/pipeline/steps-stages-connectors) as its event argument.
 
This package supports NServiceBus version 7.0 and above.

### W3C traceparent and Correlation-Context support

The Diagnostics package also provides support for both the [W3C Trace Context recommendation](https://www.w3.org/TR/trace-context/) and [W3C Correlation Context June 2020 draft](https://w3c.github.io/correlation-context/).

The Trace Context supports propagates the `traceparent` and `tracecontext` headers into outgoing messages, and populates `Activity` parent ID based on incoming messages.

The Correlation Context support consumes incoming headers into `Activity.Baggage`, and propagates `Activity.Baggage` into outgoing messages.

If you would like to add additional correlation context, inside your handler you can add additional baggage:

```csharp
Activity.Current.AddBaggage("mykey", "myvalue");
```

Correlation context can then flow out to tracing and observability tools. Common usage for correlation context are user IDs, session IDs, conversation IDs, and anything you might want to search traces to triangulate specific traces.

## OpenTelemetry usage

Once you've referenced the Diagnostics package to expose diagnostics events as above, you can configure OpenTelemetry (typically through the [OpenTelemetry.Extensions.Hosting](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting/0.2.0-alpha.275) package).

```csharp
services.AddOpenTelemetry(builder => {
    builder
        // Configure exporters
        .UseZipkin()
        // Configure adapters
        .UseRequestAdapter()
        .UseDependencyAdapter()
        .AddNServiceBusAdapter(); // Adds NServiceBus OTel support
});
```

Since OTel is supported at the NServiceBus level, any transport that NServiceBus supports also supports OTel.
This package supports the latest released alpha package on NuGet.

By default, the message body is not logged to OTel. To change this, configure the options:

```csharp
services.AddOpenTelemetry(builder => {
    builder
        // Configure exporters
        .UseZipkin()
        // Configure adapters
        .UseRequestAdapter()
        .UseDependencyAdapter()
        .AddNServiceBusAdapter(opt => opt.CaptureMessageBody = true); // Adds NServiceBus OTel support
});
```

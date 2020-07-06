# NServiceBus.Extensions.IntegrationTesting

[![CI](https://github.com/jbogard/NServiceBus.Extensions.IntegrationTesting/workflows/CI/badge.svg)](https://github.com/jbogard/NServiceBus.Extensions.IntegrationTesting/workflows/CI)
[![NuGet](https://img.shields.io/nuget/vpre/NServiceBus.Extensions.IntegrationTesting.svg)](https://www.nuget.org/packages/NServiceBus.Extensions.IntegrationTesting)
[![MyGet (dev)](https://img.shields.io/myget/jbogard-ci/v/NServiceBus.Extensions.IntegrationTesting.svg)](https://myget.org/gallery/jbogard-ci)

## Usage

This library extends the [Microsoft.AspNetCore.Testing.Mvc](https://www.nuget.org/packages/Microsoft.AspNetCore.Mvc.Testing) and [NServiceBus.Extensions.Hosting](https://www.nuget.org/packages/NServiceBus.Extensions.Hosting/) package to provide extensions for integration testing for messaging-based applications.

Typical integration tests with NServiceBus manually execute a single handler at a time. However, an entire application might handle multiple messages in a chain.

This package provides two extensions:

- Testing-friendly configuration for NServiceBus `EndpointConfiguration`
- Test methods to `WebApplicationHost`

To use, first create a `HostApplicationFactory` instance as you would with ASP.NET Core:

```csharp
public class TestFactory : HostingApplicationFactory<Startup> 
{
}
```

Next, you will need to override your normal host building to provide test-specific configuration for NServiceBus:

```csharp
protected override IHostBuilder CreateHostBuilder() =>
     Host.CreateDefaultBuilder()
         .UseNServiceBus(ctxt =>
         {
             var endpoint = new EndpointConfiguration("HostApplicationFactoryTests");

             // Set up NServiceBus with testing-friendly defaults
             endpoint.ConfigureTestEndpoint();

             // Set up any of your other configuration here

             return endpoint;
         })
         .ConfigureWebHostDefaults(b => b.UseStartup<Startup>());
```

Typically with xUnit, this factory becomes a fixture:

```csharp
public class HostApplicationFactoryTests 
    : IClassFixture<TestFactory>
{
    private readonly TestFactory _factory;

    public HostApplicationFactoryTests(TestFactory factory) => _factory = factory;
```

In your tests, you can call the various overloads to send a message and wait in `HostingApplicationFactory`:

```csharp
[Fact]
public async Task Can_send_and_wait()
{
    var firstMessage = new FirstMessage {Message = "Hello World"};

    var results = 
        (await _factory.SendLocalAndWaitForHandled<FinalMessage>(firstMessage))
        .ToList();

    results.ShouldNotBeEmpty();

    var message = results.Single();

    message.Message.ShouldBe(firstMessage.Message);
}
```

In the above, the `FinalMessage` is the expected "final message" after sending the first message. Either the method returns with a list of those final messages, or it will time out.

There are methods to Send/SendLocal/Publish and then wait for messages to be either sent or handled.
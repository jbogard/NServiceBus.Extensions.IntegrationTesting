# NServiceBus.Extensions.IntegrationTesting

[![CI](https://github.com/jbogard/NServiceBus.Extensions.IntegrationTesting/workflows/CI/badge.svg)](https://github.com/jbogard/NServiceBus.Extensions.IntegrationTesting/workflows/CI)
[![NuGet](https://img.shields.io/nuget/vpre/NServiceBus.Extensions.IntegrationTesting.svg)](https://www.nuget.org/packages/NServiceBus.Extensions.IntegrationTesting)
[![MyGet (dev)](https://img.shields.io/myget/jbogard-ci/v/NServiceBus.Extensions.IntegrationTesting.svg)](https://myget.org/gallery/jbogard-ci)

## Usage

This library extends the [NServiceBus.Extensions.Hosting](https://www.nuget.org/packages/NServiceBus.Extensions.Hosting/) package to provide extensions for integration testing for messaging-based applications.

Typical integration tests with NServiceBus manually execute a single handler at a time. However, an entire application might handle multiple messages in a chain.

This package provides two extensions:

- Testing-friendly configuration for NServiceBus `EndpointConfiguration`
- A test fixture to consume in your integration test, typically with the ASP.NET Core MVC testing package.

To use, first create a `WebApplicationFactory` instance as you would with ASP.NET Core:

```csharp
public class TestFactory : WebApplicationFactory<Startup> 
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

Finally, you need to add the `EndpointFixture` to your `WebApplicationFactory`, and override the `Disposing` method. This is not strictly necessary but makes for easier fixtures:

```csharp
public EndpointFixture EndpointFixture { get; }

public TestFactory() => EndpointFixture = new EndpointFixture();

protected override void Dispose(bool disposing)
{
    if (disposing)
    {
        EndpointFixture.Dispose();
    }

    base.Dispose(disposing);
}
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

    var session = _factory.Services.GetService<IMessageSession>()_

    var result = await _factory.ExecuteAndWaitForHandled<FinalMessage>(() => session.SendLocal(firstMessage)));

    result.IncomingMessageContexts.Count.ShouldBe(3);
    result.OutgoingMessageContexts.Count.ShouldBe(3);

    result.ReceivedMessages.ShouldNotBeEmpty();

    var message = result.ReceivedMessages.OfType<FinalMessage>().Single();

    message.Message.ShouldBe(firstMessage.Message);
}
```

In the above, the `FinalMessage` is the expected "final message" after sending the first message. Either the method returns with a list of all sent and received messages, or it will time out.

You can wait for specific messages to be sent or received, as well as supply a custom timeout (if startup takes a while).

### Multiple endpoints

If you have multiple endpoints to test in a single integration test, you can create a single fixture that includes a `WebApplicationFactory` for each endpoint:

```csharp
public class SystemFixture : IDisposable
{
    public WebAppFactory WebAppHost { get; }

    public WebApplicationFactory<Program> WorkerHost { get; }

    public ChildWorkerServiceFactory ChildWorkerHost { get; }

    public EndpointFixture EndpointFixture { get; }
```

Unfortunately, the hosts aren't started until the first time a client is created so you may want to include a `Start` method:

```csharp
public void Start()
{
    WorkerHost.CreateClient();
    WebAppHost.CreateClient();
    ChildWorkerHost.CreateClient();
}

public void Dispose()
{
    WebAppHost?.Dispose();
    WorkerHost?.Dispose();
    ChildWorkerHost?.Dispose();
    EndpointFixture?.Dispose();
}
```

### Xunit usage

In XUnit, tests execute in parallel by default. With async messaging, this means that multiple tests sending in parallel will overwrite each other.

To get around this, configure your fixture to be a collection fixture with parallelization turned off:

```csharp
[CollectionDefinition(nameof(SystemCollection), DisableParallelization = true)]
public class SystemCollection : ICollectionFixture<SystemFixture> { }
```

Then in your integration test, you create a constructor as normal but add the collection fixture definition:

```csharp
[Collection(nameof(SystemCollection))]
public class EndToEndTests : XunitContextBase
{
    private readonly SystemFixture _fixture;

    public EndToEndTests(SystemFixture fixture, ITestOutputHelper output) : base(output)
    {
        _fixture = fixture;
        _fixture.Start();
    }
```

In this case I am also using the [XunitContext](https://github.com/SimonCropp/XunitContext) project to output console messages to the test window so that we can see the log messages from the started hosts.
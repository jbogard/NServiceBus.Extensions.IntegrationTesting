using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NServiceBus.Pipeline;
using NServiceBus.Sagas;
using Shouldly;
using Xunit;
using static NServiceBus.Extensions.IntegrationTesting.EndpointFixture;

[assembly:CollectionBehavior(DisableTestParallelization = true)]

namespace NServiceBus.Extensions.IntegrationTesting.Tests
{
    public class HostApplicationFactoryTests 
        : IClassFixture<HostApplicationFactoryTests.TestFactory>
    {
        private readonly TestFactory _factory;

        public HostApplicationFactoryTests(TestFactory factory) => _factory = factory;

        [Fact]
        public async Task Can_send_and_wait()
        {
            var firstMessage = new FirstMessage {Message = "Hello World"};

            var session = _factory.Services.GetService<IMessageSession>();

            var result = await ExecuteAndWaitForHandled<FinalMessage>(() => session.SendLocal(firstMessage));

            result.IncomingMessageContexts.Count.ShouldBe(4);
            result.OutgoingMessageContexts.Count.ShouldBe(4);

            result.ReceivedMessages.ShouldNotBeEmpty();

            var message = result.ReceivedMessages.OfType<FinalMessage>().Single();

            message.Message.ShouldBe(firstMessage.Message);
        }

        [Fact]
        public Task Will_timeout_when_message_never_arrives()
        {
            var firstMessage = new FirstMessage {Message = "Hello World"};

            var session = _factory.Services.GetService<IMessageSession>();

            Task Action() => ExecuteAndWaitForHandled<NotHandledMessage>(() => session.SendLocal(firstMessage), TimeSpan.FromSeconds(2));

            return Should.ThrowAsync<TimeoutException>(Action);
        }

        [Fact]
        public async Task Will_wait_for_saga_to_be_completed()
        {
            var firstMessage = new StartSagaMessage {Message = "Hello World"};

            var session = _factory.Services.GetService<IMessageSession>();

            var result = await ExecuteAndWaitForSagaCompletion<SagaExample>(() => session.SendLocal(firstMessage));

            var saga = result.InvokedHandlers.Single(x =>
                x.MessageHandler.HandlerType == typeof(SagaExample)).GetSagaInstance();
            
            Assert.NotNull(saga);
            
            Assert.Equal(firstMessage.Message, ((SagaData)saga.Instance.Entity).Message);
        }
        
        public class TestFactory : WebApplicationFactory<HostApplicationFactoryTests>
        {
            protected override IHostBuilder CreateHostBuilder() =>
                Host.CreateDefaultBuilder()
                    .UseNServiceBus(ctxt =>
                    {
                        var endpoint = new EndpointConfiguration("HostApplicationFactoryTests");
                        endpoint.ConfigureTestEndpoint();
                        endpoint.UsePersistence<LearningPersistence>();
                        endpoint.UseSerialization<SystemJsonSerializer>();
                        return endpoint;
                    })
                    .ConfigureWebHostDefaults(b => b.Configure(app => {}));

            protected override IHost CreateHost(IHostBuilder builder)
            {
                builder.UseContentRoot(Directory.GetCurrentDirectory());
                return base.CreateHost(builder);
            }
        }

        public class FirstMessage : ICommand
        {
            public string Message { get; set; }
        }

        public class SecondMessage : ICommand
        {
            public string Message { get; set; }
        }

        public class ThirdMessage : IEvent
        {
            public string Message { get; set; }
        }

        public class FinalMessage : ICommand
        {
            public string Message { get; set; }
        }

        public class NotHandledMessage : ICommand
        {
            public string Message { get; set; }
        }

        public class FirstHandler : IHandleMessages<FirstMessage>
        {
            public Task Handle(FirstMessage message, IMessageHandlerContext context) => 
                context.SendLocal(new SecondMessage {Message = message.Message});
        }

        public class SecondHandler : IHandleMessages<SecondMessage>
        {
            public Task Handle(SecondMessage message, IMessageHandlerContext context) => 
                context.Publish(new ThirdMessage {Message = message.Message});
        }

        public class ThirdHandler : IHandleMessages<ThirdMessage>
        {
            public Task Handle(ThirdMessage message, IMessageHandlerContext context) => 
                context.SendLocal(new FinalMessage {Message = message.Message});
        }

        public class FinalHandler : IHandleMessages<FinalMessage>
        {
            public Task Handle(FinalMessage message, IMessageHandlerContext context) => 
                Task.CompletedTask;
        }

        public class StartSagaMessage : ICommand
        {
            public string Message { get; set; } 
        }

        public class SagaExample : Saga<SagaData>,
            IAmStartedByMessages<StartSagaMessage>
        {
            protected override void ConfigureHowToFindSaga(SagaPropertyMapper<SagaData> mapper)
            {
                //note that mapping on a string is the worst example ever
                mapper.MapSaga(saga => saga.Message)
                    .ToMessage<StartSagaMessage>(m => m.Message);
            }

            public Task Handle(StartSagaMessage message, IMessageHandlerContext context)
            {
                Data.Message = message.Message;
                MarkAsComplete();
                return Task.CompletedTask;
            }
        }

        public class SagaData : ContainSagaData
        {
            public string Message { get; set; }
        }
    }
    
    public static class InvokeHandlerContextExtension
    {
        public static ActiveSagaInstance GetSagaInstance(this IInvokeHandlerContext context)
        {
            return context.Extensions.TryGet(out ActiveSagaInstance saga) ? saga : null;
        }
    }
}

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.Domain.Entities.Events.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Uow;
using Xunit;

namespace Volo.Abp.EventBus.Distributed;

public class LocalDistributedEventBus_Test : LocalDistributedEventBusTestBase
{
    protected override void AfterAddApplication(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<ILocalEventBus, UnitTestLocalEventBus>());
        base.AfterAddApplication(services);
    }

    [Fact]
    public async Task Should_Call_Handler_AndDispose()
    {
        DistributedEventBus.Subscribe<MySimpleEventData, MySimpleDistributedTransientEventHandler>();

        await DistributedEventBus.PublishAsync(new MySimpleEventData(1));
        await DistributedEventBus.PublishAsync(new MySimpleEventData(2));
        await DistributedEventBus.PublishAsync(new MySimpleEventData(3));

        Assert.Equal(3, MySimpleDistributedTransientEventHandler.HandleCount);
        Assert.Equal(3, MySimpleDistributedTransientEventHandler.DisposeCount);
    }

    [Fact]
    public async Task Should_Change_TenantId_If_EventData_Is_MultiTenant()
    {
        var tenantId = Guid.NewGuid();

        DistributedEventBus.Subscribe<MySimpleEventData>(GetRequiredService<MySimpleDistributedSingleInstanceEventHandler>());

        await DistributedEventBus.PublishAsync(new MySimpleEventData(3, tenantId));

        Assert.Equal(tenantId, MySimpleDistributedSingleInstanceEventHandler.TenantId);
    }

    [Fact]
    public async Task Should_Change_TenantId_If_Generic_EventData_Is_MultiTenant()
    {
        var tenantId = Guid.NewGuid();

        DistributedEventBus.Subscribe<EntityCreatedEto<MySimpleEventData>>(GetRequiredService<MySimpleDistributedSingleInstanceEventHandler>());

        await DistributedEventBus.PublishAsync(new MySimpleEventData(3, tenantId));

        Assert.Equal(tenantId, MySimpleDistributedSingleInstanceEventHandler.TenantId);
    }

    [Fact]
    public async Task Should_Get_TenantId_From_EventEto_Extra_Property()
    {
        var tenantId = Guid.NewGuid();

        DistributedEventBus.Subscribe<MySimpleEto>(GetRequiredService<MySimpleDistributedSingleInstanceEventHandler>());

        await DistributedEventBus.PublishAsync(new MySimpleEto
        {
            Properties =
            {
                {"TenantId", tenantId.ToString()}
            }
        });

        Assert.Equal(tenantId, MySimpleDistributedSingleInstanceEventHandler.TenantId);
    }

    [Fact]
    public async Task DistributedEventSentAndReceived_Test()
    {
        var localEventBus = GetRequiredService<ILocalEventBus>();
        if (localEventBus is UnitTestLocalEventBus eventBus)
        {
            eventBus.OnEventHandleInvoking = async (eventType, eventData) =>
            {
                await localEventBus.PublishAsync(new DistributedEventReceived()
                {
                    Source = DistributedEventSource.Direct,
                    EventName = EventNameAttribute.GetNameOrDefault(eventType),
                    EventData = eventData
                }, onUnitOfWorkComplete: false);
            };

            eventBus.OnPublishing = async (eventType, eventData) =>
            {
                await localEventBus.PublishAsync(new DistributedEventSent()
                {
                    Source = DistributedEventSource.Direct,
                    EventName = EventNameAttribute.GetNameOrDefault(eventType),
                    EventData = eventData
                }, onUnitOfWorkComplete: false);
            };
        }

        GetRequiredService<ILocalEventBus>().Subscribe<DistributedEventSent, DistributedEventHandles>();
        GetRequiredService<ILocalEventBus>().Subscribe<DistributedEventReceived, DistributedEventHandles>();

        DistributedEventBus.Subscribe<MyEventDate, MyEventHandle>();

        using (var uow = GetRequiredService<IUnitOfWorkManager>().Begin())
        {
            await DistributedEventBus.PublishAsync(new MyEventDate(), onUnitOfWorkComplete: false);

            Assert.Equal(1, DistributedEventHandles.SentCount);
            Assert.Equal(1, DistributedEventHandles.ReceivedCount);

            await DistributedEventBus.PublishAsync(new MyEventDate(), onUnitOfWorkComplete: true);

            await uow.CompleteAsync();

            Assert.Equal(2, DistributedEventHandles.SentCount);
            Assert.Equal(2, DistributedEventHandles.ReceivedCount);
        }
    }

    class MyEventDate
    {

    }

    class MyEventHandle : IDistributedEventHandler<MyEventDate>
    {
        public Task HandleEventAsync(MyEventDate eventData)
        {
            return Task.CompletedTask;
        }
    }
}

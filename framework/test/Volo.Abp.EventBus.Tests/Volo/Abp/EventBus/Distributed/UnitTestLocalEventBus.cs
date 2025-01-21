using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace Volo.Abp.EventBus.Distributed;

/// <summary>
/// This class is used in unit tests and supports to publish DistributedEventSent and DistributedEventReceived events.
/// </summary>
public class UnitTestLocalEventBus : LocalEventBus
{
    public UnitTestLocalEventBus(
        [NotNull] IOptions<AbpLocalEventBusOptions> options,
        [NotNull] IServiceScopeFactory serviceScopeFactory,
        [NotNull] ICurrentTenant currentTenant,
        [NotNull] IUnitOfWorkManager unitOfWorkManager,
        [NotNull] IEventHandlerInvoker eventHandlerInvoker)
        : base(options, serviceScopeFactory, currentTenant, unitOfWorkManager, eventHandlerInvoker)
    {
    }

    public Func<Type, object, Task> OnEventHandleInvoking { get; set; }

    protected async override Task InvokeEventHandlerAsync(IEventHandler eventHandler, object eventData, Type eventType)
    {
        if (OnEventHandleInvoking != null && eventType != typeof(DistributedEventSent) && eventType != typeof(DistributedEventReceived))
        {
            await OnEventHandleInvoking(eventType, eventData);
        }

        await base.InvokeEventHandlerAsync(eventHandler, eventData, eventType);
    }

    public Func<Type, object, Task> OnPublishing { get; set; }

    public async override Task PublishAsync(
        Type eventType,
        object eventData,
        bool onUnitOfWorkComplete = true)
    {
        if (onUnitOfWorkComplete && UnitOfWorkManager.Current != null)
        {
            AddToUnitOfWork(
                UnitOfWorkManager.Current,
                new UnitOfWorkEventRecord(eventType, eventData, EventOrderGenerator.GetNext())
            );
            return;
        }

        if (OnPublishing != null && eventType != typeof(DistributedEventSent) && eventType != typeof(DistributedEventReceived))
        {
            await OnPublishing(eventType, eventData);
        }

        await PublishToEventBusAsync(eventType, eventData);
    }
}

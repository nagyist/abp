# OpenIddict Events Model: Custom Request Processing Logic

[ABP's OpenIddict Module](https://abp.io/docs/latest/modules/openiddict) provides an integration with the [OpenIddict](https://github.com/openiddict/openiddict-core) library which provides advanced authentication features like single sign-on, single log-out, and API access control.

OpenIddict provides an event-driven model ([event models](https://documentation.openiddict.com/introduction#events-model)) that allows developers to customize authentication and authorization processes. This event model enables handling actions such as user **sign-in**, **sign-out**, **token validation**, and **request handling** dynamically.

In this article, we will explore OpenIddict event models, their key use cases, and how to implement them effectively.

## OpenIddict Event Models

OpenIddict events are primarily used within the OpenIddict server component. These events provide hooks into the OpenID Connect flow, allowing developers to modify behavior at different stages of authentication & authorization processes.

For example if you want to do the following things, then you can use these event models:

* Adding custom logic after users sign-out from the application,
* Adding custom logic after users sign-in to the application,
* Make additional checks after token validation,
* and more...

OpenIddict provides multiple server events, under the `OpenIddictServerEvents` static class to make them easier to find (also provides additonal validation events under the `OpenIddictValidationEvents` static class). Here are some of the pre-defined `OpenIddictServerEvents`:

![](openiddict-server-events.png)

Each event represents a specific moment in the **request processing pipeline** (e.g the moment the OpenIddict server determines whether the request is a valid OpenID Connect request it should handle, the moment it extracts it, handles it or returns a response). Thanks to that, only thing you should do as an application developer is creating an event handler to subscribe to these events when they are triggered.

Let's see, how to do that in the next section with an example.

## Example: How to add custom logic when a user signs out?

Assume that you want to apply a custom logic after a user signs-out from our application. To do that, you can create a custom event handler. There are only two steps that you need to do as the following:

1. Create a custom event handler that subscribes to `OpenIddictServerEvents.ProcessSignOutContext`:

```csharp
using System.Threading.Tasks;
using OpenIddict.Server;

namespace MySolution;

public class SignOutEventHandler : IOpenIddictServerHandler<OpenIddictServerEvents.ProcessSignOutContext>
{
    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ProcessSignOutContext>()
            .UseSingletonHandler<SignOutEventHandler>()
            .SetOrder(100_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();
    
    public ValueTask HandleAsync(OpenIddictServerEvents.ProcessSignOutContext context)
    {
        //your logic...
        
        return ValueTask.CompletedTask;
    }
}
```

Here, you have subscribed to the `ProcessSignOutContext` server event and it get triggered after each signout request. So, in the `HandleAsync` method, you can apply your own logic by ensuring the users are being signed-out of the application. For example, you might be deleting some temporary data about the signed-in user from your services or any other logic that you want to do.

Notice, you have created an static property called `Descriptor`, you have set the event type as `Custom` (_OpenIddictServerHandlerType.Custom_), life time of the event handler as _Singleton_ and set an order.

> **Note:** Multiple handlers of the same type can be registered: they will be sequentially invoked in the same order as the one used to register them. Ref: https://kevinchalet.com/2018/07/02/implementing-advanced-scenarios-using-the-new-openiddict-rc3-events-model/

2. After creating an event handler, next thing you need to do is registering the event handler by configuring the `OpenIddictServerBuilder` as follows:

```cs

public class MySolutionAuthServerModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        //...
        
        PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
        {
            serverBuilder.AddEventHandler(SignOutEventHandler.Descriptor);
        });
        
        //...
    }
}

```

Here, you have configured the `OpenIddictServerBuilder` and registered the custom event handler. By doing this, the OpenIddict library be aware of the related event handler and triggers it when the related event occurs (signing-out, in this case).

That's it all. After these steps, your `SignOutEventHandler.HandleAsync()` method should be triggered after each signout request. You can also use other pre-defined server events for other stages of the authentication & authorization processes such as;

* `OpenIddictServerEvents.ProcessSignInContext` -> after each sign-in,
* `OpenIddictServerEvents.ProcessErrorContext` -> when an error occurs in the authentication,
* `OpenIddictServerEvents.ProcessChallengeContext` -> called when processing a challenge operation,
* and other 40+ server events...

## Conclusion

ABP Framework integrates OpenIddict as its authentication and authorization module. OpenIddict provides an event-driven model that allows developers to customize authentication and authorization processes within their ABP applications. It's pre-installed & pre-configured in the ABP's startup templates.

OpenIddict's event model enables handling actions such as user **sign-in**, **sign-out**, **token validation**, and **request handling** dynamically. Thanks to that, adding custom logic is pretty straight-forward and it allows modify behavior at different stages of authentication & authorization processes.

## References

* https://kevinchalet.com/2018/07/02/implementing-advanced-scenarios-using-the-new-openiddict-rc3-events-model/
* https://documentation.openiddict.com/introduction#events-model
* https://abp.io/docs/latest/modules/openiddict
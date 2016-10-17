# Microsoft.Bot.Builder.RedisStore
Redis Store for state sotrage in Microsoft Bot Framework
I have written a blog post explaining this source - https://ankitbko.github.io/2016/10/Microsoft-Bot-Framework-Use-Redis-to-store-conversation-state/

## Usage
Update the autofac registration as given below. I have used StackExchange.Redis as client, therefore follow their way of defining connection string. `RedisStoreOptions.Configuration` will be used as it is for creating `ConnectionMultiplexer`.

```csharp
private void RegisterBotDependencies()
{
    var builder = new ContainerBuilder();

    RedisStoreOptions redisOptions = new RedisStoreOptions()
    {
        Configuration = "localhost"
    };

    builder.Register(c => new RedisStore(redisOptions))
       .As<RedisStore>()
       .SingleInstance();

    builder.Register(c => new CachingBotDataStore(c.Resolve<RedisStore>(),
                                                  CachingBotDataStoreConsistencyPolicy.ETagBasedConsistency))
        .As<IBotDataStore<BotData>>()
        .AsSelf()
        .InstancePerLifetimeScope();

    builder.Update(Conversation.Container);

    DependencyResolver.SetResolver(new AutofacDependencyResolver(Conversation.Container));
}
```

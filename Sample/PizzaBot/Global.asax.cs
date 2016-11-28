using Autofac;
using Autofac.Integration.Mvc;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Internals;
using Microsoft.Bot.Connector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;

namespace Microsoft.Bot.Sample.PizzaBot
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            RegisterBotDependencies();
            GlobalConfiguration.Configure(WebApiConfig.Register);
        }

        private void RegisterBotDependencies()
        {
            var builder = new ContainerBuilder();

            RedisStoreOptions redisOptions = new RedisStoreOptions()
            {
				Configuration = "localhost"
			};

			RedisBotCache.Configure(redisOptions);

			DependencyResolver.SetResolver(new AutofacDependencyResolver(Conversation.Container));
        }
    }
}

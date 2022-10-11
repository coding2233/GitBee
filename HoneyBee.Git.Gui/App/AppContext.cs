using strange.extensions.context.api;
using strange.extensions.context.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App.Controller;
using Wanderer.App.Mediator;
using Wanderer.App.Model;
using Wanderer.App.Service;
using Wanderer.App.View;

namespace Wanderer.App
{
    public class AppContext : MVCSContext
    {
        public AppContext(ContextView contextView, ContextStartupFlags flags) : base(contextView, flags)
        {

        }

        protected override void mapBindings()
        {
            crossContextBridge.Bind(AppEvent.ShowGitRepo);

            injectionBinder.Bind<IDatabaseService>().To<DatabaseService>().ToSingleton().CrossContext();
            injectionBinder.Bind<IAppModel>().To<AppModel>().ToSingleton().CrossContext();

            mediationBinder.Bind<AppImGuiView>().To<AppImGuiMediator>();
            mediationBinder.Bind<HomeView>().To<HomeMediator>();

            commandBinder.Bind(ContextEvent.START).To<StartCommand>().Once();
        }
    }
}

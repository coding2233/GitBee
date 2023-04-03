using strange.extensions.context.api;
using strange.extensions.context.impl;
using strange.extensions.mediation.api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
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
            crossContextBridge.Bind(AppEvent.OpenFile);
            crossContextBridge.Bind(AppEvent.OpenFolder);

            injectionBinder.Bind<IDatabaseService>().To<DatabaseService>().ToSingleton().CrossContext();
            injectionBinder.Bind<IPluginService>().To<PluginService>().ToSingleton().CrossContext();
            injectionBinder.Bind<IAppModel>().To<AppModel>().ToSingleton().CrossContext();

            mediationBinder.Bind<AppImGuiView>().To<AppImGuiMediator>();
            mediationBinder.Bind<HomeView>().To<HomeMediator>();
            mediationBinder.Bind<SSHView>().To<SSHMediator>();

            commandBinder.Bind(ContextEvent.START).To<StartCommand>().Once();
        }

        override public void AddView(object view)
        {
            if (mediationBinder != null)
            {
                mediationBinder.Trigger(MediationEvent.AWAKE, view as IView);
            }
            else
            {
                cacheView(view as MonoBehaviour);
            }
        }

        public override void RemoveView(object view)
        {
            base.RemoveView(view);
        }
    }
}

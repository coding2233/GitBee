using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App.View;

namespace Wanderer.App.Mediator
{
    internal class AppImGuiMediator:EventMediator
    {

        [Inject]
        public AppImGuiView appImGuiView { get; set; }

        public override void OnRegister()
        {
            base.OnRegister();

            appImGuiView.onOpenRepository += OnOpenRepository;
        }

        public override void OnRemove()
        {
            appImGuiView.onOpenRepository += OnOpenRepository;

            base.OnRemove();
        }


        private void OnOpenRepository(string gitPath)
        {
            dispatcher.Dispatch(AppEvent.ShowGitRepo, gitPath);
        }

    }
}

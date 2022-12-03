using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App.Model;
using Wanderer.App.Service;
using Wanderer.App.View;
using Wanderer.Common;

namespace Wanderer.App.Mediator
{
    internal class AppImGuiMediator:EventMediator
    {

        [Inject]
        public AppImGuiView appImGuiView { get; set; }

        [Inject]
        public IDatabaseService database { get; set; }

        [Inject]
        public IAppModel appModel { get; set; }

        public override void OnRegister()
        {
            base.OnRegister();

            appImGuiView.OnOpenRepository += OnOpenRepository;
            appImGuiView.OnSetStyleColors += OnSetStyleColors;

            appImGuiView.SetStyleColors(database.GetCustomerData<int>("StyleColors",1));
        }

        public override void OnRemove()
        {
            appImGuiView.OnOpenRepository -= OnOpenRepository;
            appImGuiView.OnSetStyleColors -= OnSetStyleColors;

            base.OnRemove();
        }


        private void OnOpenRepository(string gitPath)
        {
            dispatcher.Dispatch(AppEvent.ShowGitRepo, gitPath);
        }

        private void OnSetStyleColors(int style)
        {
            ImGuiView.StyleColors = style;
            database.SetCustomerData<int>("StyleColors", style);
        }

    }
}

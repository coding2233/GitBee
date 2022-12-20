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
using Wanderer.GitRepository.View;

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

            GitCommandView.ViewCommands = database.GetCustomerData<List<ViewCommand>>("ViewCommand");
            if (GitCommandView.ViewCommands == null)
            {
                GitCommandView.ViewCommands = new List<ViewCommand>();
            }
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

using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.IO;
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
            appImGuiView.OnSearchRepository += OnSearchRepository;
            appImGuiView.OnSetStyleColors += OnSetStyleColors;

            appImGuiView.SetStyleColors(database.GetCustomerData<int>("StyleColors",1));
        }

        public override void OnRemove()
        {
            appImGuiView.OnOpenRepository -= OnOpenRepository;
            appImGuiView.OnSearchRepository -= OnSearchRepository;
            appImGuiView.OnSetStyleColors -= OnSetStyleColors;

            base.OnRemove();
        }


        private void OnOpenRepository(string gitPath)
        {
            dispatcher.Dispatch(AppEvent.ShowGitRepo, gitPath);
        }

        private async void OnSearchRepository(string path)
        {
            try
            {
                List<string> dirLists = null;
                await Task.Run(() =>
                {
                    dirLists = GetGitRepoPaths(path, 0);
                });

                if (dirLists != null && dirLists.Count > 0)
                {
                    foreach (var item in dirLists)
                    {
                        dispatcher.Dispatch(AppEvent.SearchGitRepo, item);
                    }
                }

                Log.Info("Search complete: {0}", path);
            }
            catch (Exception e)
            {
                Log.Warn("OnSearchRepository Exception {0}",e);
            }
        }

        private void OnSetStyleColors(int style)
        {
            ImGuiView.StyleColors = style;
            database.SetCustomerData<int>("StyleColors", style);
        }


        private List<string> GetGitRepoPaths(string dir,int index)
        {
            index++;
            Log.Info("Search git repo in {0}", dir);
            List<string> paths = new List<string>();
            if (index < 5 && Directory.Exists(dir))
            {
                var dirs = Directory.GetDirectories(dir);
                foreach (var itemDir in dirs)
                {
                    if (itemDir.EndsWith(".git"))
                    {
                        Log.Info("Search git repo, get -> {0}", itemDir);
                        paths.Add(itemDir);
                    }
                    else
                    {
                        paths.AddRange(GetGitRepoPaths(itemDir, index));
                    }
                }
            }
            else
            {
                Log.Info("藏得太深,拒绝擦查找 {0} {1}", dir, index);
            }

            return paths;
        }

    }
}

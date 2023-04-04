using strange.extensions.command.impl;
using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App;
using Wanderer.App.Service;
using Wanderer.Common;
using Wanderer.GitRepository.View;

namespace Wanderer.GitRepository.Controller
{
    internal class ShowGitRepoCommand:EventCommand
    {
        [Inject(ContextKeys.CONTEXT)]
        public IContext context { get; set; }

        [Inject]
        public IDatabaseService database { get; set; }

        public override void Execute()
        {
            string gitPath = evt.data as string;
            if (!string.IsNullOrEmpty(gitPath))
            {
                if (Directory.Exists(gitPath) && gitPath.EndsWith(".git"))
                {
                    Log.Info("ShowGitRepoCommand: {0}",gitPath);

                    //保存到数据库
                    database.AddRepository(gitPath);

                    AppContextView.AddView<GitRepoView>(gitPath);
                }
                else
                {
                    Log.Warn("Git repository is not exists: {0}", gitPath);
                }
            }
            else
            {
                Log.Warn("Git repository is null!");
            }
        }
    }
}

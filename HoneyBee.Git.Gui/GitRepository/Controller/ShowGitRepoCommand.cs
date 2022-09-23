using strange.extensions.command.impl;
using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository.View;

namespace Wanderer.GitRepository.Controller
{
    internal class ShowGitRepoCommand:EventCommand
    {
        [Inject(ContextKeys.CONTEXT)]
        public IContext context { get; set; }

        public override void Execute()
        {
            string gitPath = evt.data as string;
            if (!string.IsNullOrEmpty(gitPath))
            {
                if (Directory.Exists(gitPath) && gitPath.EndsWith(".git"))
                {
                    Log.Info("ShowGitRepoCommand: {0}",gitPath);

                    var gitRepoView = ImGuiView.Create<GitRepoView>(context);
                    gitRepoView.SetGitRepoPath(gitPath);
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

using strange.extensions.command.impl;
using strange.extensions.context.api;
using System.IO;
using Wanderer.App;
using Wanderer.App.Service;
using Wanderer.App.View;

namespace Wanderer.GitRepository.Controller
{
    public class SearchGitRepoCommand : EventCommand
    {
        [Inject(ContextKeys.CONTEXT)]
        public IContext context { get; set; }

        [Inject]
        public IDatabaseService database { get; set; }

        public override void Execute()
        {
            string gitRepoPath = (string)evt.data;

            if (!string.IsNullOrEmpty(gitRepoPath) && Directory.Exists(gitRepoPath) && gitRepoPath.EndsWith(".git"))
            {
                database.AddRepository(gitRepoPath);
                dispatcher.Dispatch(AppEvent.RefreshGitRepo);
            }
        }
    }
}

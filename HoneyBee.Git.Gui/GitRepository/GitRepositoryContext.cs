using strange.extensions.context.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App;
using Wanderer.GitRepository.Controller;
using Wanderer.GitRepository.Mediator;
using Wanderer.GitRepository.Service;
using Wanderer.GitRepository.View;

namespace Wanderer.GitRepository
{
    internal class GitRepositoryContext:MVCSContext
    {
        public GitRepositoryContext(ContextView view):base(view)
        {
            
        }

        protected override void mapBindings()
        {
            base.mapBindings();

            injectionBinder.Bind<IGitRepoService>().To<GitRepoService>().ToSingleton();

            commandBinder.Bind(AppEvent.ShowGitRepo).To<ShowGitRepoCommand>();

            mediationBinder.Bind<GitRepoView>().To<GitRepoMediator>();
        }
    }
}

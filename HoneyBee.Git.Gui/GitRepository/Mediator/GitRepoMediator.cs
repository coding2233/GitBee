using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.GitRepository.Common;
using Wanderer.GitRepository.Service;
using Wanderer.GitRepository.View;

namespace Wanderer.GitRepository.Mediator
{
    public class GitRepoMediator:EventMediator
    {
        [Inject]
        public IGitRepoService gitRepoService { get; set; }

        [Inject]
        public GitRepoView gitRepoView { get; set; }

        public GitRepo GetGitRepo(string gitPath)
        {
            return gitRepoService.GetGitRepo(gitPath);
        }

    }
}

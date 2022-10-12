using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App.Model;
using Wanderer.App.Service;
using Wanderer.GitRepository.Common;
using Wanderer.GitRepository.Service;
using Wanderer.GitRepository.View;

namespace Wanderer.GitRepository.Mediator
{
    public class GitRepoMediator : EventMediator
    {
        [Inject]
        public IGitRepoService gitRepoService { get; set; }

        [Inject]
        public GitRepoView gitRepoView { get; set; }

        [Inject]
        public IDatabaseService database { get; set; }

        [Inject]
        public IAppModel appModel { get; set; }

        //public GitRepo GetGitRepo(string gitPath)
        //{
        //    return gitRepoService.GetGitRepo(gitPath);
        //}

        public T GetUserData<T>(string key)
        {
            return database.GetCustomerData<T>(key);
        }

        public void SetUserData<T>(string key, T value)
        {
            database.SetCustomerData<T>(key, value);
        }


    }
}

using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App.Service;
using Wanderer.GitRepository.Common;

namespace Wanderer.GitRepository.Service
{
    public interface IGitRepoService
    {
        GitRepo GetGitRepo(string repoPath);
    }

    internal class GitRepoService : IGitRepoService
    {
        [Inject]
        public IDatabaseService database { get; set; }

        private string m_repoPath;
        private GitRepo m_gitRepo;

        public GitRepo GetGitRepo(string repoPath)
        {
            if (string.IsNullOrEmpty(repoPath))
            {
                return null;
            }

            repoPath = repoPath.Replace("\\", "/");
            if (repoPath.Equals(m_repoPath))
            {
                return m_gitRepo;
            }
            else
            {
                if (m_gitRepo != null)
                {
                    database.ReleaseDb(GetPathHash(m_repoPath));
                    m_gitRepo.Dispose();
                    m_gitRepo = null;
                }
                m_repoPath = "";
            }

            if (Directory.Exists(repoPath)&& repoPath.EndsWith("/.git"))
            {
                m_repoPath = repoPath;
                m_gitRepo = new GitRepo(repoPath, database.GetLiteDb(GetPathHash(repoPath)));
            }

            return m_gitRepo;
        }


        private string GetPathHash(string path)
        {
            string hashPath = path.GetHashCode().ToString();
            Log.Info("hash code: {0} -> {1}", path, hashPath);
            return hashPath;
        }
       
    }
}

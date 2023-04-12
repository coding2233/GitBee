using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using LiteDB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository.View;

namespace Wanderer.GitRepository.Common
{
    public class GitRepo : IDisposable
    {
        private Repository m_repository;

        public Repository Repo => m_repository;
        public string Name { get; private set; }
        public string RootPath { get; private set; }
        public BranchCollection Branches => m_repository.Branches;
        public List<BranchTreeViewNode> LocalBranchNodes { get; private set; } = new List<BranchTreeViewNode>();
        public List<BranchTreeViewNode> RemoteBranchNodes { get; private set; } = new List<BranchTreeViewNode>();
        public Dictionary<string, List<string>> CommitNotes { get; private set; } = new Dictionary<string, List<string>>();
        public List<GitTag> Tags { get; private set; } = new List<GitTag>();
        public List<GitSubmodule> Submodules { get; private set; } = new List<GitSubmodule>();
        public StashCollection Stashes => m_repository.Stashes;

        private Signature m_signatureAuthor;
        public Signature SignatureAuthor
        {
            get
            {
                if (m_signatureAuthor == null)
                {
                    m_signatureAuthor = m_repository.Config.BuildSignature(DateTimeOffset.Now);
                }
                return m_signatureAuthor;
            }
        }

        public Commit SelectCommit;

        public LibGit2Sharp.Diff Diff => m_repository.Diff;

        private int m_commitCount;
        public int CommitCount => m_commitCount;


        private bool m_runTask;
        private Action<float> m_taskProgress;
        internal GitRepo(string repoPath)
        {
            RootPath = repoPath.Replace("\\", "/").Replace("/.git", "");
            Name = Path.GetFileName(RootPath);
            ////同步仓库信息
            //SyncGitRepoTask();
        }

        //更新UI状态
        public void ReBuildUIData()
        {
            Task.Run(() => {
                if (m_repository == null)
                {
                    m_repository = new Repository(RootPath);
                }

                SetBranchNodes();
                SetTags();
                SetSubmodules();

                m_commitCount = m_repository.Commits.Count();
            });
        }

        public void SetSelectCommit(string sha)
        {
            SelectCommit = m_repository.Commits.Where(x => x.Sha.Equals(sha)).First();
        }

        //public string FormatCommandAction(ViewCommand command)
        //{
        //    string action = command.Action;
        //    if (string.IsNullOrEmpty(action))
        //    {
        //        action = "git --help";
        //    }
        //    else
        //    {
        //        switch (command.Target)
        //        {
        //            case ViewCommandTarget.Head:
        //            case ViewCommandTarget.Branch:
        //                break;
        //            case ViewCommandTarget.Remote:
        //                break;
        //            case ViewCommandTarget.Commit:
        //                break;
        //            case ViewCommandTarget.Tag:
        //                break;
        //            default:
        //                break;
        //        }
        //    }

        //    return action;
        //}


        public void Pull(Func<string,bool> onProgress, Func<TransferProgress, bool> onTransferProgressHandler)
        {
            // Credential information to fetch
            LibGit2Sharp.PullOptions options = new LibGit2Sharp.PullOptions();
            options.FetchOptions = new FetchOptions();
            if (onProgress != null)
            {
                options.FetchOptions.OnProgress = new ProgressHandler(onProgress);
            }

            if (onTransferProgressHandler!=null)
            {
                options.FetchOptions.OnTransferProgress = new TransferProgressHandler(onTransferProgressHandler);
            }

            //这里需要用户验证信息
            //options.FetchOptions.CredentialsProvider = new CredentialsHandler(
            //    (url, usernameFromUrl, types) =>
            //        new UsernamePasswordCredentials()
            //        {
            //            Username = USERNAME,
            //            Password = PASSWORD
            //        });

            // User information to create a merge commit
            var signature = BuildSignature();

            // Pull
            Commands.Pull(m_repository, signature, options);
        }

        public void Fetch(string remoteName)
        {
            string logMessage="";
            var remote = m_repository.Network.Remotes[remoteName];
            IEnumerable<string> refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
            Commands.Fetch(m_repository, remote.Name, refSpecs, null, logMessage);

            //next
            //git fetch --all
        }

        //private int m_oldCommintMax = 0;
        //public int GetCommitCount()
        //{
        //    try
        //    {
        //        if (m_oldCommintMax == 0)
        //        {
        //            m_oldCommintMax = m_repository.Commits.Count();
        //        }
        //        return m_oldCommintMax;
        //    }
        //    catch (Exception e)
        //    {
        //        Log.Warn("提交的总数获取失败: {0}", e);
        //    }
          
        //    return 0;
        //}


        public void Commit(string commitMessage)
        {
            if (string.IsNullOrEmpty(commitMessage))
                return;

            BuildSignature();
            //提交到仓库中
            m_repository.Commit(commitMessage, m_signatureAuthor, m_signatureAuthor);
            ReBuildUIData();
        }

        private Signature BuildSignature()
        {
            m_signatureAuthor = m_repository.Config.BuildSignature(DateTimeOffset.Now);
            return m_signatureAuthor;
        }

        public bool CheckIndex(string file)
        {
            return m_repository.Index.Where(x => x.Path.Equals(file)).Count() > 0;
        }

        public void Restore(IEnumerable<string> files)
        {
            if (files == null || files.Count() == 0)
                return;

            var options = new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force };
            m_repository.CheckoutPaths(m_repository.Head.FriendlyName, files, options);
            //Status();
        }

        //public void AddFile(IEnumerable<string> files)
        //{
        //    //IEnumerable<StatusEntry> Added
        //    if (files != null && files.Count() > 0)
        //    {
        //        foreach (var item in files)
        //        {
        //            m_repository.Index.Add(item);
        //        }
        //        m_repository.Index.Write();
        //    }
        //}

        public void Stage(IEnumerable<string> files = null)
        {
            if (files == null)
            {
                Commands.Stage(m_repository, "*");
            }
            else
            {
                if (files.Count() > 0)
                {
                    Commands.Stage(m_repository, files);
                }
            }
        }

        public void Unstage(IEnumerable<string> files = null)
        {
            if (files == null)
            {
                Commands.Unstage(m_repository, "*");
            }
            else
            {
                if (files.Count() > 0)
                    Commands.Unstage(m_repository, files);
            }
        }

        public Commit GetCommit(string commitSha)
        {
            return m_repository.Commits.Where(x=>x.Sha.Equals(commitSha)).FirstOrDefault();
        }

  

        public void Dispose()
        {

            m_repository?.Dispose();
            m_repository = null;
        }

        //设置分支
        private void SetBranchNodes()
        {
            List<BranchTreeViewNode> localbranchNodes = new List<BranchTreeViewNode>();
            List<BranchTreeViewNode> remotebranchNodes = new List<BranchTreeViewNode>();
            Dictionary<string, string> branchNotes = new Dictionary<string, string>();
            foreach (var branch in m_repository.Branches)
            {
                //string[] nameArgs = branch.FriendlyName.Split('/');
                //Queue<string> nameTree = new Queue<string>();
                //foreach (var item in nameArgs)
                //{
                //    nameTree.Enqueue(item);
                //}
                if (branch.IsRemote)
                {
                    //JointBranchNode(remotebranchNodes, nameTree, branch);

                    BranchTreeViewNode.JoinTreeViewNode(remotebranchNodes, branch.FriendlyName, branch);
                }
                else
                {
                    //JointBranchNode(localbranchNodes, nameTree, branch);
                    BranchTreeViewNode.JoinTreeViewNode(localbranchNodes, branch.FriendlyName, branch);

                }

                branchNotes.Add(branch.Reference.CanonicalName, branch.Reference.TargetIdentifier);
            }

            //整理标签
            CommitNotes.Clear();
            foreach (var item in branchNotes)
            {
                string key = item.Key;
                string value = item.Value;
                if (branchNotes.ContainsKey(value))
                {
                    value = branchNotes[value];
                }
                List<string> listValue = null;
                if (!CommitNotes.TryGetValue(value, out listValue))
                {
                    listValue = new List<string>();
                    CommitNotes.Add(value, listValue);
                }
                
                listValue.Add(Icon.Get(key.Contains("remotes")?Icon.Material_cloud: Icon.Material_download_for_offline) +key.Replace("refs/remotes/","").Replace("refs/heads/",""));
            }
            //添加tag标签
            foreach (var item in m_repository.Tags)
            {
                List<string> listValue = null;
                if (!CommitNotes.TryGetValue(item.Target.Sha, out listValue))
                {
                    listValue = new List<string>();
                    CommitNotes.Add(item.Target.Sha, listValue);
                }
                listValue.Add(Icon.Get(Icon.Material_label)+item.FriendlyName);
            }

            foreach (var item in localbranchNodes)
            {
                item.UpdateByIndex();
            }

            LocalBranchNodes = localbranchNodes;
            RemoteBranchNodes = remotebranchNodes;
        }

 

        //设置标签
        private void SetTags()
        {
            Tags.Clear();
            foreach (var item in m_repository.Tags)
            {
                GitTag gitTag = new GitTag();
                gitTag.FriendlyName = item.FriendlyName;
                gitTag.Sha = item.Target.Sha;
                Tags.Add(gitTag);
            }
        }

        //设置子模块
        private void SetSubmodules()
        {
            Submodules.Clear();
            foreach (var item in m_repository.Submodules)
            {
                GitSubmodule submodule = new GitSubmodule();
                submodule.Name = item.Name;
                submodule.Path = item.Path;
                Submodules.Add(submodule);
            }
        }

       

    }

}

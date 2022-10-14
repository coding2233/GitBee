using LibGit2Sharp;
using LiteDB;
using SharpDX.Direct3D11;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Wanderer.Common;
using static SharpDX.Utilities;

namespace Wanderer.GitRepository.Common
{
    public class GitRepo : IDisposable
    {
        private Repository m_repository;
        
        public Repository Repo => m_repository;

        private LiteDatabase m_liteDb;
        public string Name { get; private set; }
        public string RootPath { get; private set; }
        public BranchCollection Branches => m_repository.Branches;
        public List<GitBranchNode> LocalBranchNodes { get; private set; } = new List<GitBranchNode>();
        public List<GitBranchNode> RemoteBranchNodes { get; private set; } = new List<GitBranchNode>();
        public Dictionary<string, List<string>> BranchNotes { get; private set; }=new Dictionary<string,List<string>>();
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

        public LibGit2Sharp.Diff Diff => m_repository.Diff;

        public RepositoryStatus RetrieveStatus => m_repository.RetrieveStatus();

        private bool m_runTask;
        private Action<float> m_taskProgress;
        internal GitRepo(string repoPath)
        {
            RootPath = repoPath.Replace("\\","/").Replace("/.git", "");
            Name = Path.GetFileName(RootPath);
            m_liteDb = new LiteDatabase(Path.Combine(Application.UserPath,$"{Name}.db"));
            m_repository = new Repository(RootPath);
            ////同步仓库信息
            //SyncGitRepoTask();
        }

        //同步仓库信息
        public async void SyncGitRepoTask(Action<float> progress, Action complete)
        {
            if (m_runTask)
            {
                Log.Info("正在执行任务中，请勿打扰");
                return;
            }
            m_taskProgress = progress;
            m_runTask = true;
            var tasks = SyncGitRepoToDatabase();
            foreach (var itemTask in tasks)
            {
                await itemTask;
            }

            m_runTask = false;
            complete?.Invoke();
        }

        //将git数据同步到数据库
        private List<Task> SyncGitRepoToDatabase()
        {
            List<Task> list = new List<Task>();

            try
            {
                //分支更新到数据库
                list.Add(Task.Run(SetBranchNodes));

                //Tags更新到数据
                list.Add(Task.Run(SetTags));

                //子模块更新到数据
                list.Add(Task.Run(SetSubmodules));

                //本地提交更新到数据
                list.Add(Task.Run(SetRepoCommits));


            }
            catch (Exception e)
            {
                Log.Warn("检查Git仓库与数据库是否匹配,异常: {0}",e);
            }

            return list;

            //Log.Info($"SyncGitRepoToDatabase complete.");
        }


        public int GetCommitCount()
        {
            try
            {
                var commitsCol = m_liteDb.GetCollection<GitRepoCommit>();
                int count = commitsCol.Query().Count();
                return count;
            }
            catch (Exception e)
            {
                Log.Warn("提交的总数获取失败: {0}", e);
            }
          
            return 0;
        }

        public List<GitRepoCommit> GetCommits(int startIndex, int endIndex)
        {
            try
            {
                //这里在一直获取 ， 可以优化

                //id为倒序， 要转换一遍
                var commitsCol = m_liteDb.GetCollection<GitRepoCommit>();
                int commitCount = commitsCol.Query().Count();
                startIndex = commitCount - startIndex;
                endIndex = commitCount - endIndex;
                var commits = commitsCol.Query().Where(x => x.Id <= startIndex && x.Id >= endIndex).ToList();
                commits.Reverse();
                //倒序
                return commits;
            }
            catch (Exception e)
            {
                Log.Warn("获取提交失败: {0}",e);
            }
            return null;
        }

        public void Commit(string commitMessage)
        {
            if (string.IsNullOrEmpty(commitMessage))
                return;

            //提交到仓库中
            m_signatureAuthor = m_repository.Config.BuildSignature(DateTimeOffset.Now);
            m_repository.Commit(commitMessage, m_signatureAuthor, m_signatureAuthor);

            //更新数据
            SyncGitRepoTask(null,null);
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

        public void AddFile(IEnumerable<string> files)
        {
            //IEnumerable<StatusEntry> Added
            if (files != null && files.Count() > 0)
            {
                foreach (var item in files)
                {
                    m_repository.Index.Add(item);
                }
                m_repository.Index.Write();
            }
        }

        public void Stage(IEnumerable<string> files = null)
        {
            if (files == null)
            {
                Commands.Stage(m_repository, "*");
            }
            else
            {
                if (files.Count() > 0)
                    Commands.Stage(m_repository, files);
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
            return m_repository.Commits.Where(x=>x.Sha.Equals(commitSha)).First();
        }

        public GitRepoCommit GetGitCommit(string commitSha)
        {
            var commitsCol = m_liteDb.GetCollection<GitRepoCommit>();
            return commitsCol.Query().Where(x=>x.Commit.Equals(commitSha)).FirstOrDefault();
        }


        public void Dispose()
        {
            m_liteDb?.Dispose();
            m_liteDb = null;

            m_repository?.Dispose();
            m_repository = null;
        }


        private void SetRepoCommits()
        {
            //更新以前得数据
            var commitCol = m_liteDb.GetCollection<GitRepoCommit>();
            Task.Run(() =>
            {
                var oldGitCommits = commitCol.Query().Where(x=>x.Branchs == null || x.Branchs.Count == 0).ToEnumerable();
                foreach (var oldGitCommit in oldGitCommits)
                {
                    Task.Run(() => {
                        UpdateRef(commitCol, oldGitCommit, GetCommit(oldGitCommit.Commit));
                    });
                }
            });

            //具体提交
            int oldCommitCount = commitCol.Query().Count(); 
            var commits = m_repository.Commits.ToList();
            for (int i = commits.Count -1 - oldCommitCount; i >=0; i--)
            {
                m_taskProgress?.Invoke((commits.Count - i) / (float)commits.Count);

                var commit = commits[i];
                var oldGitCommit = commitCol.Query().Where(x => x.Commit.Equals(commit.Sha)).FirstOrDefault();
                if (oldGitCommit!=null)
                {
                    if (oldGitCommit.Branchs == null|| oldGitCommit.Branchs.Count==0)
                    {
                        Task.Run(() => {
                            UpdateRef(commitCol, oldGitCommit, commit);
                        });
                    }
                    continue;
                }
                else
                {
                    GitRepoCommit gitRepoCommit = new GitRepoCommit();
                    gitRepoCommit.Description = commit.MessageShort;
                    gitRepoCommit.Date = commit.Author.When.ToString("yyyy-MM-dd HH:mm:ss");
                    gitRepoCommit.Author = commit.Author.Name;
                    gitRepoCommit.Commit = commit.Sha;
                    gitRepoCommit.Message = commit.Message;
                    gitRepoCommit.Email = commit.Author.Email;
                    gitRepoCommit.Parents = new List<string>();
                    foreach (var itemParent in commit.Parents)
                    {
                        gitRepoCommit.Parents.Add(itemParent.Sha);
                    }

                    Task.Run(() => {
                        UpdateRef(commitCol, gitRepoCommit, commit);
                    });

                    commitCol.Insert(gitRepoCommit);
                }
            }
        }

        private void UpdateRef(ILiteCollection<GitRepoCommit> commitCol, GitRepoCommit gitRepoCommit,Commit commit)
        {
            IEnumerable<Reference> refs = m_repository.Refs.ReachableFrom(new[] { commit });
            if (refs != null)
            {
                gitRepoCommit.Branchs = new List<string>();
                foreach (var itemRefs in refs)
                {
                    if (itemRefs.CanonicalName.EndsWith("/HEAD"))
                    {
                        continue;
                    }
                    gitRepoCommit.Branchs.Add(itemRefs.CanonicalName);
                }
                commitCol.Update(gitRepoCommit);
            }
            gitRepoCommit = null;
        }


        //设置分支
        private void SetBranchNodes()
        {
            List<GitBranchNode> localbranchNodes = new List<GitBranchNode>();
            List<GitBranchNode> remotebranchNodes = new List<GitBranchNode>();
            Dictionary<string, string> branchNotes = new Dictionary<string, string>();
            foreach (var branch in m_repository.Branches)
            {
                string[] nameArgs = branch.FriendlyName.Split('/');
                Queue<string> nameTree = new Queue<string>();
                foreach (var item in nameArgs)
                {
                    nameTree.Enqueue(item);
                }
                if (branch.IsRemote)
                {
                    JointBranchNode(remotebranchNodes, nameTree, branch);
                }
                else
                {
                    JointBranchNode(localbranchNodes, nameTree, branch);
                }

                branchNotes.Add(branch.Reference.CanonicalName, branch.Reference.TargetIdentifier);
            }

            //整理标签
            //还差tags
            BranchNotes.Clear();
            foreach (var item in branchNotes)
            {
                string key = item.Key;
                string value = item.Value;
                if (branchNotes.ContainsKey(value))
                {
                    value = branchNotes[value];
                }
                List<string> listValue = null;
                if (!BranchNotes.TryGetValue(value, out listValue))
                {
                    listValue = new List<string>();
                    BranchNotes.Add(value, listValue);
                }
                listValue.Add(key.Replace("refs/remotes/","").Replace("refs/heads/",""));
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

        private void JointBranchNode(List<GitBranchNode> branchNodes, Queue<string> nameTree, Branch branch)
        {
            if (nameTree.Count == 1)
            {
                GitBranchNode branchNode = new GitBranchNode();
                branchNode.Name = nameTree.Dequeue();
                branchNode.FullName = branch.FriendlyName;
                branchNode.Branch = branch;
                branchNodes.Add(branchNode);
            }
            else
            {
                string name = nameTree.Dequeue();
                var findNode = branchNodes.Find(x => x.Name.Equals(name));
                if (findNode == null)
                {
                    findNode = new GitBranchNode();
                    findNode.Name = name;
                    findNode.Children = new List<GitBranchNode>();
                    branchNodes.Add(findNode);
                }
                JointBranchNode(findNode.Children, nameTree, branch);
            }
        }

    }

}

using LibGit2Sharp;
using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.GitRepository.Common
{
    public class GitRepo : IDisposable
    {
        private Repository m_repository;

        private LiteDatabase m_liteDb;
        public string Name { get; private set; }
        public string RootPath { get; private set; }
        public List<GitBranchNode> LocalBranchNodes { get; private set; } = new List<GitBranchNode>();
        public List<GitBranchNode> RemoteBranchNodes { get; private set; } = new List<GitBranchNode>();
        public List<GitTag> Tags { get; private set; } = new List<GitTag>();
        public List<GitSubmodule> Submodules { get; private set; } = new List<GitSubmodule>();
        public StashCollection Stashes => m_repository.Stashes;

        internal GitRepo(string m_repoPath, LiteDatabase db)
        {
            RootPath = m_repoPath.Replace("/.git", "");
            Name = Path.GetFileName(RootPath);
            m_liteDb = db;
            m_repository = new Repository(RootPath);
        }

        //将git数据同步到数据库
        public async void SyncGitRepoToDatabase(Action onComplete)
        {
            //分支更新到数据库
            await Task.Run(SetBranchNodes);

            //Tags更新到数据
            new Task(SetTags).Start();

            //子模块更新到数据
            new Task(SetSubmodules).Start();

            //本地提交更新到数据
            var commitCol = m_liteDb.GetCollection<GitRepoCommit>();
            var commits = await SetRepoCommits();
            //判断一下 ， 不需要更新数据库的操作，避免强制刷新
            if (commitCol.Count() != commits.Count())
            {
                commitCol.DeleteAll();
                commitCol.Insert(commits);
            }

            Log.Info($"commits count: {commits}");
            //完成回调
            onComplete?.Invoke();
        }


        public int GetCommitCount()
        {
            var commitsCol = m_liteDb.GetCollection<GitRepoCommit>();
            int count = commitsCol.Query().Count();
            return count;
        }

        public List<GitRepoCommit> GetCommits(int startIndex, int endIndex)
        {
            var commitsCol = m_liteDb.GetCollection<GitRepoCommit>();
            var commits= commitsCol.Query().Where(x => x.Id >= startIndex && x.Id < endIndex).ToList();
            return commits;
        }

        public Commit GetCommit(int index)
        {
            return m_repository.Commits.ElementAt(index);
        }

        public void Dispose()
        {
        }


        private Task<List<GitRepoCommit>> SetRepoCommits()
        {
            TaskCompletionSource<List<GitRepoCommit>> taskCompletionSource = new TaskCompletionSource<List<GitRepoCommit>>();
            List<GitRepoCommit> commitsResult = new List<GitRepoCommit>();
            Task.Run(() => {
                foreach (var commit in m_repository.Commits)
                {
                    GitRepoCommit gitRepoCommit = new GitRepoCommit();
                    gitRepoCommit.Description = commit.MessageShort;
                    gitRepoCommit.Date = commit.Committer.When.ToString("yyyy-MM-dd HH:mm:ss");
                    gitRepoCommit.Author = commit.Committer.Name;
                    gitRepoCommit.Commit = commit.Sha;
                    commitsResult.Add(gitRepoCommit);
                }
                taskCompletionSource.SetResult(commitsResult);
            });
            return taskCompletionSource.Task;
        }


        //设置分支
        private void SetBranchNodes()
        {
            List<GitBranchNode> localbranchNodes = new List<GitBranchNode>();
            List<GitBranchNode> remotebranchNodes = new List<GitBranchNode>();

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

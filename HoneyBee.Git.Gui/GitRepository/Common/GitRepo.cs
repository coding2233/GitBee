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
            //var localBranchCol = m_liteDb.GetCollection<GitBranchNode>("LocalBranchNodes");
            //localBranchCol.DeleteAll();
            //localBranchCol.Insert(LocalBranchNodes);
            //var remoteBranchCol = m_liteDb.GetCollection<GitBranchNode>("RemoteBranchNodes");
            //remoteBranchCol.DeleteAll();
            //remoteBranchCol.Insert(RemoteBranchNodes);

            //Tags更新到数据

            //本地提交更新到数据
            var commitCol = m_liteDb.GetCollection<GitRepoCommit>();
            var commits = await SetRepoCommits();
            commitCol.DeleteAll();
            commitCol.Insert(commits);

            //完成回调
            onComplete?.Invoke();
        }

       

        public List<GitRepoCommit> GetCommits(int startIndex, int endIndex=100)
        {
            var commitsCol = m_liteDb.GetCollection<GitRepoCommit>();
            var commits= commitsCol.Query().Where(x => x.Id >= startIndex && x.Id < endIndex).ToList();
            return commits;
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

        private void SetTags()
        {

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

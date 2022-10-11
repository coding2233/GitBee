using LibGit2Sharp;
using LiteDB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;

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

        internal GitRepo(string repoPath)
        {
            RootPath = repoPath.Replace("\\","/").Replace("/.git", "");
            Name = Path.GetFileName(RootPath);
            m_liteDb = new LiteDatabase(Path.Combine(Application.UserPath,$"{Name}.db"));
            m_repository = new Repository(RootPath);

            new Task(SyncGitRepoToDatabase).Start();
        }

        //将git数据同步到数据库
        private void SyncGitRepoToDatabase()
        {
            try
            {
                //分支更新到数据库
                SetBranchNodes();

                //Tags更新到数据
                SetTags();

                //子模块更新到数据
                SetSubmodules();

                //本地提交更新到数据
                SetRepoCommits();

            }
            catch (Exception e)
            {
                Log.Warn("检查Git仓库与数据库是否匹配,异常: {0}",e);
            }

            Log.Info($"SyncGitRepoToDatabase complete.");
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
                var commitsCol = m_liteDb.GetCollection<GitRepoCommit>();
                var commits = commitsCol.Query().Where(x => x.Id >= startIndex && x.Id < endIndex).ToList();
                return commits;
            }
            catch (Exception e)
            {
                Log.Warn("获取提交失败: {0}",e);
            }
            return null;
        }

        public Commit GetCommit(int index)
        {
            return m_repository.Commits.ElementAt(index);
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
            var commitCol = m_liteDb.GetCollection<GitRepoCommit>();

            TaskCompletionSource<List<GitRepoCommit>> taskCompletionSource = new TaskCompletionSource<List<GitRepoCommit>>();
            List<GitRepoCommit> commitsResult = new List<GitRepoCommit>();
            foreach (var commit in m_repository.Commits)
            {
                bool hasCommit = commitCol.Query().Where(x => x.Commit.Equals(commit.Sha)).Count() > 0;
                if (hasCommit)
                {
                    break;
                }
                else
                {
                    GitRepoCommit gitRepoCommit = new GitRepoCommit();
                    gitRepoCommit.Description = commit.MessageShort;
                    gitRepoCommit.Date = commit.Committer.When.ToString("yyyy-MM-dd HH:mm:ss");
                    gitRepoCommit.Author = commit.Committer.Name;
                    gitRepoCommit.Commit = commit.Sha;
                    gitRepoCommit.Message = commit.Message;
                    gitRepoCommit.Email = commit.Committer.Email;
                    gitRepoCommit.Parents = new List<string>();
                    foreach (var itemParent in commit.Parents)
                    {
                        gitRepoCommit.Parents.Add(itemParent.Sha);
                    }
                    commitsResult.Add(gitRepoCommit);
                }
            }

            if (commitsResult.Count>0)
            {
                for (int i = commitsResult.Count-1; i>=0;  i--)
                {
                    commitCol.Insert(commitsResult[i]);
                }
            }

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

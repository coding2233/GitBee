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
    public class GitRepo:IDisposable
    {
        private Repository m_repository;

        private LiteDatabase m_liteDb;
        public string Name { get; private set; }
        public string RootPath { get; private set; }

        internal GitRepo(string m_repoPath, LiteDatabase db)
        {
            RootPath = m_repoPath.Replace("/.git", "");
            Name = Path.GetFileName(RootPath);
            m_liteDb = db;
            m_repository = new Repository(RootPath);

            //将git数据同步到数据库
            SyncGitRepoToDatabase();
        }

        private async void SyncGitRepoToDatabase()
        {
            var commitsCol = m_liteDb.GetCollection<GitRepoCommit>();
            await Task.Delay(10);
            commitsCol.DeleteAll();
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
    }


    public class GitRepoCommit
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public string Date { get; set; }
        public string Author { get; set; }
        public string Commit { get; set; }
    }

}

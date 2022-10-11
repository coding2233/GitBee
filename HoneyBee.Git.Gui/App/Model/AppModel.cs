using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App.Service;

namespace Wanderer.App.Model
{
    public interface IAppModel
    {
        //string[] GetRepositories();
        //void AddRepository(string gitPath);
        //void RemoveRepository(string gitPath);
    }

    public class AppModel : IAppModel
    {
        [Inject]
        public IDatabaseService database { get; set; }

        private string dataKey = "GitRepositories";
        private List<string> m_repositories;

        public AppModel()
        {
            m_repositories = database.GetCustomerData<List<string>>(dataKey, null);
            if (m_repositories == null)
            {
                m_repositories = new List<string>();
            }
        }

        public void AddRepository(string gitPath)
        {
            gitPath = gitPath.Replace("\\", "/");
            if (!m_repositories.Contains(gitPath))
            {
                m_repositories.Add(gitPath);
                database.SetCustomerData(dataKey, m_repositories);
            }
        }

        public string[] GetRepositories()
        {
            if (m_repositories == null || m_repositories.Count == 0)
                return null;

            return m_repositories.ToArray();
        }

        public void RemoveRepository(string gitPath)
        {
            if (m_repositories.Contains(gitPath))
            {
                m_repositories.Remove(gitPath);
                database.SetCustomerData(dataKey, m_repositories);
            }
        }
    }
}

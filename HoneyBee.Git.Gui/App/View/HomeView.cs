using ImGuiNET;
using strange.extensions.context.api;
using strange.extensions.dispatcher.eventdispatcher.api;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App.Model;
using Wanderer.App.Service;
using Wanderer.Common;

namespace Wanderer.App.View
{
    public class HomeView : ImGuiTabView
    {
        public override string Name => "Home";

        public override string UniqueKey => Name;

        private SplitView m_splitView = new SplitView(SplitView.SplitType.Horizontal, 200);

        private List<string> m_repositories;
        private List<string> m_repositoriesName;
        private string m_repoSearchText="";

        private string m_selectGitPath;
        private string m_selectGitName;

        [Inject]
        public IAppModel appModel { get; set; }

        [Inject]
        public IDatabaseService database { get; set; }

        public HomeView() 
        {
        }

        public override void OnAwake()
        {
            base.OnAwake();
            dispatcher.AddListener(AppEvent.RefreshGitRepo, OnRefreshGitRepo);
            OnRefreshGitRepo(null);
        }
        protected override void OnDestroy()
        {
            dispatcher.RemoveListener(AppEvent.RefreshGitRepo, OnRefreshGitRepo);
            base.OnDestroy();
        }

        private void OnRefreshGitRepo(IEvent e)
        {
            var repos = database.GetRepositories();
            SetRepositories(repos);
        }

        public override void OnDraw()
        {
            m_splitView.Begin();
            OnRepositoriesDraw();
            m_splitView.Separate();
            OnRepositoryContentDraw();
            m_splitView.End();
        }

        private void SetRepositories(string[] repositories)
        {
            m_repositories = new List<string>();
            m_repositoriesName = new List<string>();
            if (repositories!=null)
            {
                for (int i = 0; i < repositories.Length; i++)
                {
                    m_repositories.Add(repositories[i]);
                    int index = repositories[i].LastIndexOf('.');
                    string name = repositories[i].Substring(0, index-1);
                    name = Path.GetFileName(name);
                    m_repositoriesName.Add(name);
                }
            }

            if (string.IsNullOrEmpty(m_selectGitPath))
            {
                //选择默认的
                SelectRepo(0);
            }
        }


        private void SelectRepo(int index)
        {
            if (m_repositories != null && index < m_repositories.Count)
            {
                if (Directory.Exists(m_repositories[index]))
                {
                    m_selectGitPath = m_repositories[index];
                    m_selectGitName = m_repositoriesName[index];
                    //README.md
                    string readMePath = Path.Combine(m_selectGitPath, "../README.md");

                    if (!File.Exists(readMePath))
                    {
                        readMePath = Path.Combine(m_selectGitPath, "../docs/README.md");
                    }

                    if (File.Exists(readMePath))
                    {
                        ImGuiMarkDown.SetMarkdownPath(readMePath);
                    }

                }

            }
        }

        private void OnRepositoriesDraw()
        {
            ImGui.Text("Repositories");
            ImGui.SetNextItemWidth(ImGui.GetWindowWidth());
            ImGui.InputText("",ref m_repoSearchText,100);
            if (m_repositories != null && m_repositories.Count > 0)
            {
                for (int i = 0; i < m_repositories.Count; i++)
                {
                    if (!string.IsNullOrEmpty(m_repoSearchText) && !m_repositories[i].Contains(m_repoSearchText, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    bool repoExists = Directory.Exists(m_repositories[i]);
                    if (!repoExists)
                    {
                        ImGui.BeginDisabled();
                    }

                    if (ImGui.RadioButton(m_repositoriesName[i], m_repositories[i].Equals(m_selectGitPath)))
                    {
                        if (repoExists)
                        {
                            SelectRepo(i);
                        }
                        else
                        {
                            m_selectGitPath = null;
                        }
                    }

                    if (!repoExists)
                    {
                        //ImGui.SameLine();
                        //ImGui.TextDisabled("is missing!");
                        ImGui.EndDisabled();
                        ImGui.SameLine();
                        if (ImGui.Button("Remove"))
                        {
                            RemoveRepoPath(m_repositories[i]);
                        }
                    }
                }
                
            }
        }

        private void OnRepositoryContentDraw()
        {
            if (!string.IsNullOrEmpty(m_selectGitPath))
            {
                ImGui.Text(m_selectGitName);
                ImGui.SameLine();
                if (ImGui.Button("Remove"))
                {
                    RemoveRepoPath(m_selectGitPath);
                }
                ImGui.SameLine();
                if (ImGui.Button("Open"))
                {
                    dispatcher.Dispatch(AppEvent.ShowGitRepo, m_selectGitPath);
                }
                ImGui.Text(m_selectGitPath);

                ImGui.Separator();

                if (ImGuiMarkDown.IsValid)
                {
                    ImGui.BeginChild("Home-OnRepositoryContentDraw-README");
                    ImGuiMarkDown.Render();
                    ImGui.EndChild();
                }
                else
                {
                    ImGui.Text("No README.md");
                }
            }
        }


        private void RemoveRepoPath(string path)
        {
            database.RemoveRepository(path);
            OnRefreshGitRepo(null);
        }
    }
}

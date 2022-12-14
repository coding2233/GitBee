using ImGuiNET;
using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private string m_selectGitPath;
        private string m_selectGitName;
        private string m_readMeText;

        internal Action<string> OnOpenRepository;
        internal Action<string> OnRemoveRepository;

        public HomeView(IContext context) : base(context)
        {
        }


        public override void OnDraw()
        {
            m_splitView.Begin();
            OnRepositoriesDraw();
            m_splitView.Separate();
            OnRepositoryContentDraw();
            m_splitView.End();
        }

        public void SetRepositories(string[] repositories)
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

            //选择默认的
            SelectRepo(0);
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
                    m_readMeText = null;
                    string readMePath = Path.Combine(m_selectGitPath, "../README.md");
                    if (File.Exists(readMePath))
                    {
                        m_readMeText = File.ReadAllText(readMePath);
                    }
                }

            }
        }

        private void OnRepositoriesDraw()
        {
            ImGui.Text("Repositories");
            if (m_repositories != null && m_repositories.Count > 0)
            {
                for (int i = 0; i < m_repositories.Count; i++)
                {
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
                            OnRemoveRepository?.Invoke(m_repositories[i]);
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
                    OnRemoveRepository?.Invoke(m_selectGitPath);
                }
                ImGui.SameLine();
                if (ImGui.Button("Open"))
                {
                    OnOpenRepository?.Invoke(m_selectGitPath);
                }
                ImGui.Text(m_selectGitPath);

                ImGui.Separator();

                if (string.IsNullOrEmpty(m_readMeText))
                {
                    ImGui.Text("No README.md");
                }
                else
                {
                    ImGui.BeginChild("Home-OnRepositoryContentDraw-README");
                    ImGui.Text(m_readMeText);
                    ImGui.EndChild();
                }
            }
        }

    }
}

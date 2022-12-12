using ImGuiNET;
using LibGit2Sharp;
using SFB;
using strange.extensions.context.api;
using strange.extensions.context.impl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository.View;

namespace Wanderer.App.View
{
    internal class AppImGuiView : ImGuiView
    {
        internal Action<string> OnOpenRepository;
        internal Action<int> OnSetStyleColors;
        private int m_styleColors;
        //private string m_statusLog = Icon.Get(Icon.Material_open_with);
        private string m_fullLog = Icon.Get(Icon.Material_open_with);
        private bool m_showPreference;
        private SplitView m_commandsSplit01 = new SplitView(SplitView.SplitType.Horizontal,0.3f);
        private SplitView m_commandsSplit02 = new SplitView();
        private int m_commandSeleted;
        public AppImGuiView(IContext context) : base(context)
        {
            Log.LogMessageReceiver += (logger) =>
            {
                m_fullLog = logger.fullLog;
            };
        }

        //设置
        internal void SetStyleColors(int styleColors)
        {
            if (m_styleColors != styleColors)
            {
                m_styleColors = styleColors;
                OnSetStyleColors?.Invoke(m_styleColors);
            }
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 3);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 3);
            ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 3);
            switch (m_styleColors)
            {
                case 0:
                    ImGui.StyleColorsLight();
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1);
                    break;
                case 1:
                    ImGui.StyleColorsDark();
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0.0f);
                    break;
                case 2:
                    ImGui.StyleColorsClassic();
                    ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0.0f);
                    break;
            }
            //TextEditor.SetStyle(userSettings.TextStyleColors);
        }


        public override void OnDraw()
        {
            //DrawMainMenuBar();
            if (m_showPreference)
            {
                var viewport = ImGui.GetMainViewport();
                ImGui.OpenPopup("Preference");
                ImGui.SetNextWindowSize(viewport.WorkSize * 0.7f);
                if (ImGui.BeginPopupModal("Preference", ref m_showPreference, ImGuiWindowFlags.NoResize| ImGuiWindowFlags.NoMove))
                {
                    if (ImGui.BeginTabBar("PreferenceTab"))
                    {
                        if (ImGui.BeginTabItem("Commands##PreferenceTabCommands"))
                        {
                            var commands = GitCommandView.ViewCommands;

                            m_commandsSplit01.Begin();
                            ImGui.BeginChild("Preference-Commands-Name",ImGui.GetWindowSize()-new System.Numerics.Vector2(0,ImGui.GetTextLineHeight()*2));
                            for (int i = 0; i < commands.Count; i++)
                            {
                                var command = commands[i];
                                if (ImGui.Selectable($"{command.Target}|{command.Name}|{command.Action}", m_commandSeleted == i))
                                {
                                    m_commandSeleted = i;
                                }
                            }
                       
                            ImGui.EndChild();
                            ImGui.Button("+");
                            ImGui.SameLine();
                            ImGui.Button("-");
                            m_commandsSplit01.Separate();
                            m_commandsSplit02.Begin();

                            if (m_commandSeleted >= 0 && m_commandSeleted < commands.Count)
                            {
                                var selectCommand = commands[m_commandSeleted];
                                int targetSelect = 0;
                                ImGui.Combo("Target", ref targetSelect, new string[] { "branch", "remote", "tag", "commit" }, 4);
                            }
                            m_commandsSplit02.Separate();

                            ImGui.Text("$branch selected branch name");
                            ImGui.Text("$remote selected branch remote name");

                            m_commandsSplit02.End();

                            m_commandsSplit01.End();

                          
                            ImGui.EndTabItem();
                        }
                        ImGui.EndTabBar();
                    }
                    ImGui.End();
                }
            }

           
        }

        public void DrawMainMenuBar()
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.BeginMenu("New"))
                    {
                        //if (ImGui.MenuItem("Folder Diff"))
                        //{
                        //    //mainModel.CreateTab<DiffFolderWindow>();
                        //}
                        //if (ImGui.MenuItem("File Diff"))
                        //{
                        //    //mainModel.CreateTab<DiffFileWindow>();
                        //}
                        if (ImGui.MenuItem("Open Repository"))
                        {
                            //mainModel.CreateTab<GitRepoWindow>();
                            StandaloneFileBrowser.OpenFolderPanelAsync("Open Repository", "", false, (folders) => {
                                if (folders != null && folders.Length > 0)
                                {
                                    string gitPath = Path.Combine(folders[0], ".git");
                                    Log.Info("StandaloneFileBrowser.OpenFolderPanel: {0}", gitPath);
                                    if (Directory.Exists(gitPath))
                                    {
                                        OnOpenRepository?.Invoke(gitPath);
                                    }
                                }

                            });
                           
                        }
                        ImGui.EndMenu();
                    }

                    if (ImGui.MenuItem("Preference"))
                    {
                        m_showPreference = true;
                    }

                    ImGui.Separator();
                    if (ImGui.MenuItem("Exit"))
                    {
                        Environment.Exit(0);
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Edit"))
                {
                    if (ImGui.BeginMenu("Style"))
                    {
                        var styleIndex = m_styleColors;
                        if (ImGui.MenuItem("Light", "", styleIndex == 0))
                        {
                            styleIndex = 0;
                        }
                        if (ImGui.MenuItem("Drak", "", styleIndex == 1))
                        {
                            styleIndex = 1;
                        }
                        if (ImGui.MenuItem("Classic", "", styleIndex == 2))
                        {
                            styleIndex = 2;
                        }

                        if (styleIndex != m_styleColors)
                        {
                            SetStyleColors(styleIndex);
                        }
                        ImGui.EndMenu();
                    }

                    //if (ImGui.MenuItem("Text Style"))
                    //{
                    //    //_textStyleModal.Popup();
                    //}
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Window"))
                {
                    if (ImGui.MenuItem("Home"))
                    {
                        ImGuiView.Create<HomeView>(context, 0);
                    }
                    //if (ImGui.MenuItem("Terminal Window"))
                    //{
                    //    //mainModel.CreateTab<TerminalWindow>();
                    //}
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Help"))
                {
                    if (ImGui.MenuItem("About"))
                    {
                        //mainModel.CreateTab<AboutTabWindow>();
                    }
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

        }


        public void DrawStatusBar()
        {
            ImGui.Text(m_fullLog);
        }


    }
}

using ImGuiNET;
using LibGit2Sharp;
using SFB;
using strange.extensions.context.api;
using strange.extensions.context.impl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository.Common;
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

        struct DisplayDialogInfo
        {
            public string Title;
            public string Message;
            public string OK;
            public string Cancel;
            public Action<bool> OnCallback;
            public bool FirstFrame;
        }

        private static DisplayDialogInfo s_displayDialogInfo;

        public static void DisplayDialog(string title, string message, string ok, string cancel, Action<bool> onCallback)
        {
            if (s_displayDialogInfo.OnCallback != null || onCallback==null)
            {
                return;
            }

            s_displayDialogInfo.Title = title;
            s_displayDialogInfo.Message = message;
            s_displayDialogInfo.OK = string.IsNullOrEmpty(ok) ? "OK" : ok;
            s_displayDialogInfo.Cancel = cancel;
            s_displayDialogInfo.OnCallback = onCallback;
            s_displayDialogInfo.FirstFrame = true;
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
            ImGui.PushStyleColor(ImGuiCol.ModalWindowDimBg, Vector4.Zero);
           
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
                            OnDrawPreferenceCommand();

                            ImGui.EndTabItem();
                        }

                        if (ImGui.BeginTabItem("Plugin"))
                        {
                            if (ImGui.Button("Call lua"))
                            {
                               
                            }

                            //Plugin.Call("OnDraw");

                            ImGui.EndTabItem();
                        }
                       

                        ImGui.EndTabBar();

                       
                    }
                    ImGui.End();
                }
            }

            //DisplayDialog
            bool showDisplayDialog = s_displayDialogInfo.OnCallback != null;
            if (showDisplayDialog)
            {
                string showDisplayDialogTitle = $"{s_displayDialogInfo.Title}##DisplayDialog";
                if (s_displayDialogInfo.FirstFrame)
                {
                    var viewport = ImGui.GetMainViewport();
                    ImGui.SetNextWindowSize(viewport.WorkSize * 0.3f);
                    s_displayDialogInfo.FirstFrame = false;
                }
                ImGui.OpenPopup(showDisplayDialogTitle);
                if (ImGui.BeginPopupModal(showDisplayDialogTitle, ref showDisplayDialog))//ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove
                {
                    ImGui.Text(s_displayDialogInfo.Message);

                    if (ImGui.Button(s_displayDialogInfo.OK))
                    {
                        s_displayDialogInfo.OnCallback.Invoke(true);
                        s_displayDialogInfo.OnCallback = null;
                    }

                    if (!string.IsNullOrEmpty(s_displayDialogInfo.Cancel))
                    {
                        ImGui.SameLine();
                        if (ImGui.Button(s_displayDialogInfo.Cancel))
                        {
                            s_displayDialogInfo.OnCallback.Invoke(false);
                            s_displayDialogInfo.OnCallback = null;
                        }
                        ImGui.SameLine();
                    }

                    ImGui.End();
                }

                if (!showDisplayDialog)
                {
                    s_displayDialogInfo.OnCallback.Invoke(false);
                    s_displayDialogInfo.OnCallback = null;
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
                        try
                        {
                            Process.Start("Explorer", "https://coding2233.github.io/GitBee/");
                        }
                        catch (Exception e)
                        {
                            Log.Warn("Contact {0}", e);
                        }
                    }
                    //if (ImGui.MenuItem("Contact"))
                    //{
                        
                    //}
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

        }


        public void DrawStatusBar()
        {
            ImGui.Text(m_fullLog);
        }


        private void OnDrawPreferenceCommand()
        {
            //var commands = GitCommandView.ViewCommands;

            m_commandsSplit01.Begin();
            ImGui.BeginChild("Preference-Commands-Name", ImGui.GetWindowSize() - new System.Numerics.Vector2(0, ImGui.GetTextLineHeight() * 2));
            //for (int i = 0; i < commands.Count; i++)
            //{
            //    var command = commands[i];
            //    if (ImGui.Selectable($"{command.Target}|{command.Name}|{command.Action}", m_commandSeleted == i))
            //    {
            //        m_commandSeleted = i;
            //    }
            //}

            ImGui.EndChild();
            ImGui.Button("+");
            ImGui.SameLine();
            ImGui.Button("-");
            m_commandsSplit01.Separate();
            m_commandsSplit02.Begin();
            //var vireTargetNames = Enum.GetNames(typeof(ViewCommandTarget));
            //if (m_commandSeleted >= 0 && m_commandSeleted < commands.Count)
            //{
            //    int targetSelect = (int)commands[m_commandSeleted].Target;
            //    if (ImGui.Combo("Target", ref targetSelect, vireTargetNames, vireTargetNames.Length))
            //    {
            //        commands[m_commandSeleted].Target = (ViewCommandTarget)targetSelect;
            //    }
            //}
            ImGui.SameLine();
            bool showUI = false;
            ImGui.Checkbox("With UI", ref showUI);
            m_commandsSplit02.Separate();

            ImGui.Text("$branch selected branch name");
            ImGui.Text("$remote selected branch remote name");

            m_commandsSplit02.End();

            m_commandsSplit01.End();
        }

    }
}

using ImGuiNET;
using SFB;
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
    internal class AppImGuiView : ImGuiView
    {
        internal Action<string> onOpenRepository;

        public AppImGuiView(IContext context) : base(context)
        {
        }

        public override void OnDraw()
        {
            DrawMainMenuBar();
        }

        private void DrawMainMenuBar()
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.BeginMenu("New"))
                    {
                        if (ImGui.MenuItem("Folder Diff"))
                        {
                            //mainModel.CreateTab<DiffFolderWindow>();
                        }
                        if (ImGui.MenuItem("File Diff"))
                        {
                            //mainModel.CreateTab<DiffFileWindow>();
                        }
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
                                        onOpenRepository?.Invoke(gitPath);
                                    }
                                }

                            });
                           
                        }
                        ImGui.EndMenu();
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
                        //var styleIndex = userSettings.StyleColors;
                        //if (ImGui.MenuItem("Light", "", styleIndex == 0))
                        //{
                        //    styleIndex = 0;
                        //}
                        //if (ImGui.MenuItem("Drak", "", styleIndex == 1))
                        //{
                        //    styleIndex = 1;
                        //}
                        //if (ImGui.MenuItem("Classic", "", styleIndex == 2))
                        //{
                        //    styleIndex = 2;
                        //}
                        //if (styleIndex != userSettings.StyleColors)
                        //{
                        //    userSettings.StyleColors = styleIndex;
                        //    SetStyleColors();
                        //}
                        ImGui.EndMenu();
                    }

                    if (ImGui.MenuItem("Text Style"))
                    {
                        //_textStyleModal.Popup();
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Window"))
                {
                    if (ImGui.MenuItem("Main Window"))
                    {
                        //mainModel.CreateTab<MainTabWindow>();
                    }
                    if (ImGui.MenuItem("Terminal Window"))
                    {
                        //mainModel.CreateTab<TerminalWindow>();
                    }
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
    }
}

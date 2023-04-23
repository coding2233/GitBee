using ImGuiNET;
using SFB;
using strange.extensions.context.api;
using strange.extensions.context.impl;
using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Wanderer.App.Service;
using Wanderer.App.View;
using Wanderer.Common;
using Wanderer.GitRepository;
using Wanderer.GitRepository.View;
using Wanderer.TextCodeEditor;

namespace Wanderer.App
{
    public class AppContextView : ContextView
    {
        protected ImGuiWindowFlags s_defaultWindowFlag = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove;
        private static Vector2 s_statusBarSize;
        private string m_fullLog = Icon.Get(Icon.Material_open_with);
        private List<ImGuiView> s_imGuiViews = new List<ImGuiView>();
        private List<ImGuiTabView> s_imGuiTabViews = new List<ImGuiTabView>();
        private ImGuiTabView s_lastActiveImGuiTabView;
        private HashSet<ImGuiTabView> s_imGuiTabViewsWaitClose = new HashSet<ImGuiTabView>();
        private AppMainImGuiView m_appMainImGuiView;

        private AppContext m_appContext;

        
        public AppContextView()
        {
            Log.LogMessageReceiver += (logger) =>
            {
                m_fullLog = logger.fullLog;
            };

            m_appContext = new AppContext(this, ContextStartupFlags.MANUAL_LAUNCH);
            context = m_appContext;
            //添加子Context
            AddChildContext();
            //启动
            context.Launch();

            AddView<AppMainImGuiView>();
        }


        internal void OnImGuiRender()
        {
            ////这里可设置背景图片
            //var bgImage = Application.LoadTextureFromFile(@"C:\Users\EDY\Pictures\WallPaper\wallhaven-rddgwm.jpg");
            ////ImGui.Image(bgImage.Image, bgImage.Size);
            //ImGui.GetBackgroundDrawList().AddImage(bgImage.Image, Vector2.Zero, Vector2.One * 2000);
            //var bgColor = ImGui.ColorConvertU32ToFloat4(ImGui.GetColorU32(ImGuiCol.WindowBg));
            //bgColor.W = 0.6f;
            //ImGui.PushStyleColor(ImGuiCol.WindowBg, ImGui.ColorConvertFloat4ToU32(bgColor));

            //主菜单
            m_appMainImGuiView?.DrawMainMenuBar();

            //主窗口 
            var viewport = ImGui.GetMainViewport();
            float lineHight = ImGui.GetTextLineHeight() * 2f;
            s_statusBarSize = new Vector2(viewport.WorkSize.X, lineHight);

            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowSize(viewport.WorkSize - new Vector2(0, s_statusBarSize.Y));

            ImGuiWindowFlags imGuiWindowFlags = s_defaultWindowFlag;

            //没啥用
            //if (s_imGuiViews.Count > 0)
            //{
            //    imGuiWindowFlags |= ImGuiWindowFlags.NoInputs;
            //}

            if (ImGui.Begin("ImGui_AppContextView_Window", imGuiWindowFlags))
            {
                if (s_imGuiTabViews.Count > 0)
                {
                    if (ImGui.BeginTabBar("ImGui_AppContextView_Window_Tabs", ImGuiTabBarFlags.FittingPolicyDefault | ImGuiTabBarFlags.TabListPopupButton | ImGuiTabBarFlags.AutoSelectNewTabs))
                    {
                        for (int i = 0; i < s_imGuiTabViews.Count; i++)
                        {
                            var tabWindow = s_imGuiTabViews[i];
                            bool showTab = true;
                            ImGuiTabItemFlags tabItemFlag = ImGuiTabItemFlags.Trailing | ImGuiTabItemFlags.NoCloseWithMiddleMouseButton;
                            if (tabWindow.Unsave)
                            {
                                tabItemFlag |= ImGuiTabItemFlags.UnsavedDocument;
                            }
                            bool visible = ImGui.BeginTabItem(tabWindow.IconName + tabWindow.Name + $"##tab_{tabWindow.Name}_{i}", ref showTab, tabItemFlag);
                            if (visible)
                            {
                                if (s_lastActiveImGuiTabView != tabWindow)
                                {
                                    if (s_lastActiveImGuiTabView != null)
                                    {
                                        s_lastActiveImGuiTabView.OnDisable();
                                    }
                                    tabWindow.OnEnable();
                                    s_lastActiveImGuiTabView = tabWindow;
                                }
                                tabWindow.OnDraw();
                                ImGui.EndTabItem();
                            }

                            if (!showTab)
                            {
                                Log.Info("Close table window:{0}", tabWindow.Name);
                                s_imGuiTabViewsWaitClose.Add(tabWindow);
                            }
                        }
                        ImGui.EndTabBar();
                    }
                }
                else
                {
                    string adText = "广告位招租";
                    Vector2 adPos = viewport.WorkSize * 0.5f - ImGui.CalcTextSize(adText) * 0.5f;
                    ImGui.SetCursorPos(adPos);
                    ImGui.Text(adText);
                }

                //关闭tabview
                if (s_imGuiTabViewsWaitClose.Count > 0)
                {
                    bool unsaveAsk = false;
                    foreach (var item in s_imGuiTabViewsWaitClose)
                    {
                        if (item.Unsave)
                        {
                            unsaveAsk = true;
                            break;
                        }
                    }

                    if (unsaveAsk)
                    {
                        ImGui.OpenPopup("TabView close popup modal");
                        ImGui.SetNextWindowSize(new Vector2(384, 200),ImGuiCond.FirstUseEver);
                        if (ImGui.BeginPopupModal("TabView close popup modal"))
                        {
                            ImGui.Text("Make sure to close the tabview:");
                            foreach (var item in s_imGuiTabViewsWaitClose)
                            {
                                ImGui.Text($"<{item.Name}>");
                            }

                            if (ImGui.Button("OK"))
                            {
                                foreach (var item in s_imGuiTabViewsWaitClose)
                                {
                                    s_imGuiTabViews.Remove(item);
                                    item.Dispose();
                                }
                                s_imGuiTabViewsWaitClose.Clear();
                                ImGui.CloseCurrentPopup();
                            }
                            ImGui.SameLine();
                            if (ImGui.Button("Cancel"))
                            {
                                s_imGuiTabViewsWaitClose.Clear();
                                ImGui.CloseCurrentPopup();
                            }
                            ImGui.EndPopup();
                        }
                    }
                    else
                    {
                        foreach (var item in s_imGuiTabViewsWaitClose)
                        {
                            s_imGuiTabViews.Remove(item);
                            item.Dispose();
                        }
                        s_imGuiTabViewsWaitClose.Clear();
                    }
                }

                ImGui.End();
            }


            //状态栏
            ImGui.SetNextWindowPos(viewport.WorkPos + new Vector2(0, viewport.WorkSize.Y - s_statusBarSize.Y));
            ImGui.SetNextWindowSize(s_statusBarSize);
            //ImGui.SetNextWindowViewport(viewport.ID);
            if (ImGui.Begin("Main_Status_Window", imGuiWindowFlags))
            {
                DrawStatusBar();
            }
            ImGui.End();

            //其他界面
            for (int i = 0; i < s_imGuiViews.Count; i++)
            {
                s_imGuiViews[i].OnDraw();
            }

        }

        internal void OnWindowFocus(bool lost)
        {
            if (s_lastActiveImGuiTabView != null)
            {
                if (lost)
                {
                    s_lastActiveImGuiTabView.OnDisable();
                }
                else
                {
                    s_lastActiveImGuiTabView.OnEnable();
                }
                //Log.Info("ImGuiTabView {0} Set Active {1}", s_lastActiveImGuiTabView.Name, !lost);
            }
        }

        protected override void OnViewAdd(strange.extensions.mediation.impl.View view)
        {
            if (view is ImGuiTabView tabView)
            {
                bool hasSameTableView = false;
                if (!string.IsNullOrEmpty(tabView.UniqueKey))
                {
                    for (int i = 0; i < s_imGuiTabViews.Count; i++)
                    {
                        if (tabView.UniqueKey.Equals(s_imGuiTabViews[i].UniqueKey))
                        {
                            hasSameTableView = true;
                            break;
                        }
                    }
                }

                if (!hasSameTableView)
                {
                    s_imGuiTabViews.Add(tabView);
                }
                else
                {
                    tabView?.Dispose();
                    view = null;
                }
            }
            else if (view is ImGuiView imguiView)
            {
                s_imGuiViews.Add(imguiView);
            }
            else if (view is AppMainImGuiView appMainGuiView)
            {
                m_appMainImGuiView = appMainGuiView;
            }
            //else
            //{
            //    view.Dispose();
            //}
        }

        protected override void OnViewRemove(strange.extensions.mediation.impl.View view)
        {
            if (view != null)
            {
                if (view is ImGuiTabView tabView)
                {
                    if (s_imGuiTabViews.Contains(tabView))
                    {
                        s_imGuiTabViews.Remove(tabView);
                    }
                }
                else if (view is ImGuiView imguiView)
                {
                    if (s_imGuiViews.Contains(view))
                    {
                        s_imGuiViews.Remove(imguiView);
                    }
                }
                else if (view is AppMainImGuiView)
                {
                    m_appMainImGuiView = null ;
                }
                view.Dispose();
            }
        }

       

        protected override void OnDestroy()
        {
            base.OnDestroy();
            m_appContext = null;
        }

        internal void OnDropFileEvent(string path)
        {
            if (m_appContext != null)
            {
                m_appContext.dispatcher.Dispatch(AppEvent.OpenFile, path);
            }
        }

        private void AddChildContext()
        {
            //Git仓库 
            new GitRepositoryContext(this);

            //文本编辑
            new TextEditorContext(this);
        }

       
        //状态栏
        private void DrawStatusBar()
        {
            ImGui.Text($"FPS:{((int)ImGui.GetIO().Framerate).ToString("D2")} ");
            ImGui.SameLine();
            ImGui.Text(m_fullLog);
        }
    }



    public class AppMainImGuiView: EventView
    {
        [Inject]
        public IDatabaseService database { get; set; }

        int m_styleColors = -1;

        public override void OnAwake()
        {
            base.OnAwake();

           var styleColors = database.GetCustomerData<int>("StyleColors", 1);
            SetStyleColors(styleColors);
        }

        //主菜单
        public void DrawMainMenuBar()
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu(LuaPlugin.GetText("File")))
                {
                    if (ImGui.BeginMenu(LuaPlugin.GetText("New")))
                    {
                        if (ImGui.MenuItem(LuaPlugin.GetText("Open Folder")))
                        {
                            var folders = StandaloneFileBrowser.OpenFolderPanel("Open Folder", "", false);
                            if (folders != null && folders.Length > 0)
                            {
                                //OnOpenFolder.Dispatch(folders[0]);
                            }
                        }

                        if (ImGui.MenuItem(LuaPlugin.GetText("Clone")))
                        {
                            //GitCommandView.RunGitCommandView<CloneGitCommand>();
                        }

                        if (ImGui.MenuItem(LuaPlugin.GetText("Open Repository")))
                        {
                            //mainModel.CreateTab<GitRepoWindow>();
                            StandaloneFileBrowser.OpenFolderPanelAsync("Open Repository", "", false, (folders) => {
                                if (folders != null && folders.Length > 0)
                                {
                                    string gitPath = Path.Combine(folders[0], ".git");
                                    Log.Info("StandaloneFileBrowser.OpenFolderPanel: {0}", gitPath);
                                    if (Directory.Exists(gitPath))
                                    {
                                        //OnOpenRepository?.Invoke(gitPath);
                                        dispatcher.Dispatch(AppEvent.ShowGitRepo, gitPath);
                                    }
                                }
                            });

                        }

                        if (ImGui.MenuItem(LuaPlugin.GetText("Search Repository")))
                        {
                            //mainModel.CreateTab<GitRepoWindow>();
                            StandaloneFileBrowser.OpenFolderPanelAsync("Search Repository", "", false, (folders) => {
                                if (folders != null && folders.Length > 0)
                                {
                                    string searchDirPath = folders[0];
                                    Log.Info("StandaloneFileBrowser.OpenFolderPanel: {0}", searchDirPath);
                                    if (Directory.Exists(searchDirPath))
                                    {
                                        OnSearchRepository(searchDirPath);
                                    }
                                }
                            });
                        }
                        ImGui.EndMenu();
                    }

                    if (ImGui.MenuItem(LuaPlugin.GetText("Preference")))
                    {
                        //m_showPreference = true;
                    }

                    ImGui.Separator();
                    if (ImGui.MenuItem(LuaPlugin.GetText("Exit")))
                    {
                        Environment.Exit(0);
                    }
                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu(LuaPlugin.GetText("Edit")))
                {
                    if (ImGui.BeginMenu(LuaPlugin.GetText("Style")))
                    {
                        var styleIndex = m_styleColors;
                        if (ImGui.MenuItem(LuaPlugin.GetText("Light"), "", styleIndex == 0))
                        {
                            styleIndex = 0;
                        }
                        if (ImGui.MenuItem(LuaPlugin.GetText("Drak"), "", styleIndex == 1))
                        {
                            styleIndex = 1;
                        }
                        if (ImGui.MenuItem(LuaPlugin.GetText("Classic"), "", styleIndex == 2))
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

                if (ImGui.BeginMenu(LuaPlugin.GetText("Window")))
                {
                    if (ImGui.MenuItem(LuaPlugin.GetText("Home")))
                    {
                        AppContextView.AddView<HomeView>();
                        //AddView<HomeView>();
                    }

                    if (ImGui.BeginMenu("Debug"))
                    {
                        if (ImGui.MenuItem(LuaPlugin.GetText("Material Icons")))
                        {
                            AppContextView.AddView<MaterialIconsView>();
                            //AddView<MaterialIconsView>();
                        }

                        ImGui.EndMenu();
                    }

                    if (ImGui.MenuItem("Terminal"))
                    {
                        GitCommandView.ShowTerminal(null);
                    }

                    //if (ImGui.MenuItem("SSH Client"))
                    //{
                    //    ImGuiView.Create<SSHView>(context, 0);
                    //}
                    ImGui.EndMenu();
                }


                string helpText = LuaPlugin.GetText("Help");
                if (!string.IsNullOrEmpty(Application.UpdateDownloadURL))
                {
                    helpText += Icon.Get(Icon.Material_tips_and_updates);
                }

                if (ImGui.BeginMenu(helpText))
                {
                    if (!string.IsNullOrEmpty(Application.UpdateDownloadURL))
                    {
                        if (ImGui.MenuItem("Update" + Icon.Get(Icon.Material_tips_and_updates)))
                        {
                            Process.Start("Explorer", Application.UpdateDownloadURL);
                        }
                    }

                    if (ImGui.MenuItem(LuaPlugin.GetText("About")))
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

                    if (ImGui.BeginMenu(LuaPlugin.GetText("License")))
                    {
                        //mainModel.CreateTab<AboutTabWindow>();
                        if (ImGui.MenuItem(LuaPlugin.GetText("Icon")))
                        {
                            try
                            {
                                //"\"https://www.flaticon.com/free-icon/bee_809154?term=bee&page=1&position=2&origin=search&related_id=809154/\""
                                Process.Start("Explorer", "https://www.flaticon.com/free-icon/bee_809154");
                            }
                            catch (Exception e)
                            {
                                Log.Warn("Contact {0}", e);
                            }
                        }
                        ImGui.EndMenu();
                    }

                    if (ImGui.MenuItem(LuaPlugin.GetText("Issues")))
                    {
                        //https://github.com/coding2233/GitBee/issues
                    }

                    if (ImGui.MenuItem(LuaPlugin.GetText("Report a bug")))
                    {

                    }
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

        }

        private async void OnSearchRepository(string path)
        {
            try
            {
                List<string> dirLists = null;
                await Task.Run(() =>
                {
                    dirLists = GetGitRepoPaths(path, 0);
                });

                if (dirLists != null && dirLists.Count > 0)
                {
                    foreach (var item in dirLists)
                    {
                        dispatcher.Dispatch(AppEvent.SearchGitRepo, item);
                    }
                }

                Log.Info("Search complete: {0}", path);
            }
            catch (Exception e)
            {
                Log.Warn("OnSearchRepository Exception {0}", e);
            }
        }

        private List<string> GetGitRepoPaths(string dir, int index)
        {
            index++;
            Log.Info("Search git repo in {0}", dir);
            List<string> paths = new List<string>();
            if (index < 5 && Directory.Exists(dir))
            {
                try
                {
                    var dirs = Directory.GetDirectories(dir);
                    foreach (var itemDir in dirs)
                    {
                        if (itemDir.EndsWith(".git"))
                        {
                            Log.Info("Search git repo, get -> {0}", itemDir);
                            paths.Add(itemDir);
                        }
                        else
                        {
                            paths.AddRange(GetGitRepoPaths(itemDir, index));
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Log.Warn("GetGitRepoPaths dir:{0} exception:{1}", dir, e);
                }
            }
            else
            {
                Log.Info("藏得太深,拒绝擦查找 {0} {1}", dir, index);
            }

            return paths;
        }

        //设置
        internal void SetStyleColors(int styleColors)
        {
            if (m_styleColors != styleColors)
            {
                m_styleColors = styleColors;
                //OnSetStyleColors?.Invoke(m_styleColors);
                database.SetCustomerData<int>("StyleColors", styleColors);
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


    }

}

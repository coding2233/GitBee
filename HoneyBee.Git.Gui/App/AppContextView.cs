using ImGuiNET;
using strange.extensions.context.api;
using strange.extensions.context.impl;
using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
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

        private const string OpenFileDialogKey = "AppContextViewOpenFileDialogKey";
        private static Action<string> s_showOpenFileDialogCallback;
        private static bool s_showCloseFileDialog;

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

            //文件弹窗
            if (s_showOpenFileDialogCallback!=null)
            {
                if (Application.ImFileDialogRender(OpenFileDialogKey))
                {
                    string result = Application.GetFileDialogResult();
                    s_showOpenFileDialogCallback(result);
                    s_showOpenFileDialogCallback = null;
				}
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

		#region ImFileDialog

		public static void OpenFileDialog(Action<string> callback, string title, string filter, bool isMultiselect, string startingDir)
		{
            if (string.IsNullOrEmpty(startingDir))
            {
                startingDir = Application.UserBasePath;
            }
			s_showOpenFileDialogCallback = callback;
			Application.OpenFileDialog(OpenFileDialogKey, title, filter, isMultiselect, startingDir);
		}

		public static string OpenFileDialog(string title, string filter, bool isMultiselect, string startingDir)
		{
            if (!s_showCloseFileDialog)
            {
				if (string.IsNullOrEmpty(startingDir))
				{
					startingDir = Application.UserBasePath;
				}
				Application.OpenFileDialog(OpenFileDialogKey, title, filter, isMultiselect, startingDir);
                s_showCloseFileDialog = true;
			}
            if (s_showCloseFileDialog)
            {
                if (Application.ImFileDialogRender(OpenFileDialogKey))
                {
                    s_showCloseFileDialog = false;
					string result = Application.GetFileDialogResult();
                    return result;
                }
            }
            return string.Empty;
		}

		#endregion


		#region Extension widget
		public static bool Spinner(bool centered = true,string title= "##Spinner", float radius=12, int thickness=3)
		{
			var oldCurPos = ImGui.GetCursorPos();
			if (centered)
            {
                var newCurPos = ImGui.GetContentRegionAvail() * 0.5f;
                ImGui.SetCursorPos(newCurPos);
            }
            bool result = Application.ImGuiSpinner(title, radius, thickness);
            if (centered)
            {
                ImGui.SetCursorPos(oldCurPos);
            }
			return result;
		}
		#endregion
	}




	public class AppMainImGuiView: EventView
    {
        [Inject]
        public IDatabaseService database { get; set; }

        int m_styleColors = -1;

        private string[] m_languages;
        private int m_language = -1;


        public override void OnAwake()
        {
            base.OnAwake();

           var styleColors = database.GetCustomerData<int>("StyleColors", 1);
            SetStyleColors(styleColors);

            m_languages = new string[] { "English", "简体中文" };
            var languageIndex = database.GetCustomerData<int>("Language", 0);
            SetLanguage(languageIndex);
        }

        //主菜单
        public void DrawMainMenuBar()
        {
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("File"))
                {
                    if (ImGui.BeginMenu("New"))
                    {
                        if (ImGui.MenuItem("Open Folder"))
                        {
                            //var folders = StandaloneFileBrowser.OpenFolderPanel("Open Folder", "", false);
                            //if (folders != null && folders.Length > 0)
                            //{
                            //    //OnOpenFolder.Dispatch(folders[0]);
                            //}
                        }

                        if (ImGui.MenuItem("Clone"))
                        {
                            //GitCommandView.RunGitCommandView<CloneGitCommand>();
                        }

                        if (ImGui.MenuItem("Open Repository"))
                        {
							AppContextView.OpenFileDialog((result) => {
                                string gitPath = result;
                                if (string.IsNullOrEmpty(gitPath))
                                {
                                    return;
                                }
                                if (!gitPath.EndsWith(".git"))
                                {
									gitPath = Path.Combine(gitPath, ".git");
								}
								Log.Info("OpenFileDialog.OpenFolderPanel: {0}", gitPath);
								if (Directory.Exists(gitPath))
								{
									dispatcher.Dispatch(AppEvent.ShowGitRepo, gitPath);
								}
							}, "Open Repository", "", false, "");
                        }

                        if (ImGui.MenuItem("Search Repository"))
                        {
							AppContextView.OpenFileDialog((result) => {
								string searchDirPath = result;
								if (string.IsNullOrEmpty(searchDirPath))
								{
									return;
								}
								Log.Info("OpenFileDialog.OpenFolderPanel: {0}", searchDirPath);
								if (Directory.Exists(searchDirPath))
								{
									OnSearchRepository(searchDirPath);
								}
							}, "Search Repository", "", false, "");
                        }
                        ImGui.EndMenu();
                    }

                    if (ImGui.MenuItem("Preference"))
                    {
                        //m_showPreference = true;
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

                    //if (ImGui.BeginMenu("Language"))
                    //{
                    //    //var languageIndex = m_language;
                    //    //for (int i = 0; i < m_languages.Length; i++)
                    //    //{
                    //    //    if (ImGui.MenuItem(m_languages[i], "", languageIndex == i))
                    //    //    {
                    //    //        languageIndex = i;
                    //    //    }
                    //    //}


                    //    //if (languageIndex != m_language)
                    //    //{
                    //    //    SetLanguage(languageIndex);
                    //    //}
                    //    ImGui.EndMenu();
                    //}

                    ImGui.EndMenu();
                }

                if (ImGui.BeginMenu("Window"))
                {
                    if (ImGui.MenuItem("Home"))
                    {
                        AppContextView.AddView<HomeView>();
                        //AddView<HomeView>();
                    }

                    if (ImGui.BeginMenu("Debug"))
                    {
                        if (ImGui.MenuItem("Material Icons"))
                        {
                            AppContextView.AddView<MaterialIconsView>();
                            //AddView<MaterialIconsView>();
                        }

						if (ImGui.MenuItem("File Dialog"))
						{
                            AppContextView.OpenFileDialog((result) => {
                                Log.Info("OpenFileDialog {0}", result);
                            }, "Open a texture", "Image file (*.png;*.jpg;*.jpeg;*.bmp;*.tga){.png,.jpg,.jpeg,.bmp,.tga},.*", false, "");
						}

						if (ImGui.MenuItem("Folder Dialog"))
						{
							AppContextView.OpenFileDialog((result) => {
								Log.Info("OpenFileDialog {0}", result);
							}, "Open a folder", "", false, "");
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


                string helpText = "Help";
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

                    if (ImGui.MenuItem("About"))
                    {
                        //mainModel.CreateTab<AboutTabWindow>();
                        try
                        {
							Process.Start("Explorer", "https://coding2233.github.io/posts/GitBee/");
						}
						catch (Exception e)
                        {
                            Log.Warn("Contact {0}", e);
                        }
                    }

                    if (ImGui.BeginMenu("License"))
                    {
                        //mainModel.CreateTab<AboutTabWindow>();
                        if (ImGui.MenuItem("Icon"))
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

                    if (ImGui.MenuItem("Issues"))
                    {
                        //https://github.com/coding2233/GitBee/issues
                    }

                    if (ImGui.MenuItem("Report a bug"))
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

        private void SetLanguage(int languageIndex)
        {
            if (languageIndex != m_language)
            {
                database.SetCustomerData<int>("Language", m_language);
                m_language = languageIndex;
            }

        }

    }



	public class PopupImGuiView : ImGuiView
	{
        private const string PopupImGuiViewKey = "PopupImGuiView - {0}";
        private bool m_popup;
        private Action<bool> m_onCallback;
        private string m_name;
        private string m_title;
		private string m_desc;
		private string m_ok;
        private string m_cancel;

        public void Show(Action<bool> callback,string name, string title, string desc, string ok, string cancel)
        {
			m_popup = true;
            m_name = string.Format(PopupImGuiViewKey, name);
			m_title = title;
			m_desc = desc;
			m_onCallback = callback;
			m_ok = ok;
			m_cancel = cancel;
		}

		public override void OnDraw()
		{
            if (m_popup)
            {
				ImGui.OpenPopup(m_name);
                ImGui.SetNextWindowSize(ImGui.GetWindowSize() * 0.35f, ImGuiCond.FirstUseEver);
                if (ImGui.BeginPopupModal(m_name, ref m_popup))
                {
                    DrawPopupItem();
					ImGui.EndPopup();
                }

				if (!m_popup)
				{
					ClosePopup(false);
				}
			}
		}

        private void DrawPopupItem()
        {
            if (!string.IsNullOrEmpty(m_title))
            {
                ImGui.Text(m_title);
            }
			if (!string.IsNullOrEmpty(m_desc))
			{
				ImGui.Text(m_desc);
			}

			bool showOk = false;
            if (!string.IsNullOrEmpty(m_ok))
            {
				ImGui.SetNextItemWidth(100);
                if (ImGui.Button(m_ok))
                {
                    ClosePopup(true);
				}
                showOk = true;
			}

			if (!string.IsNullOrEmpty(m_cancel))
			{
                if (showOk)
                {
                    ImGui.SameLine();
                }
				ImGui.SetNextItemWidth(100);
				if (ImGui.Button(m_cancel))
				{
					ClosePopup(false);
				}
			}

		}

		private void ClosePopup(bool result)
        {
			ImGui.CloseCurrentPopup();
			AppContextView.RemoveView(this);
			m_onCallback?.Invoke(result);
		}

	}
}

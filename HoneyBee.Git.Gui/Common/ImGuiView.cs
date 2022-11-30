using ImGuiNET;
using SFB;
using strange.extensions.context.api;
using strange.extensions.context.impl;
using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App.View;

namespace Wanderer.Common
{
    public abstract class ImGuiView : EventView
    {
        private static List<ImGuiView> s_imGuiViews=new List<ImGuiView>();
        private static List<ImGuiTabView> s_imGuiTabViews=new List<ImGuiTabView>();
        private static ImGuiTabView s_lastActiveImGuiTabView;

        protected static ImGuiWindowFlags s_defaultWindowFlag = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        public static int StyleColors { get; set; }

        public static List<Vector4> Colors { get; } = new List<Vector4>() { new Vector4(0.9803921568627451f, 0.8274509803921569f, 0.5647058823529412f,1.0f),
                                        new Vector4(0.9725490196078431f, 0.7607843137254902f, 0.5686274509803922f,1.0f),
                                        new Vector4(0.4156862745098039f, 0.5372549019607843f, 0.8f,1.0f),
                                        new Vector4(0.5098039215686275f, 0.8f, 0.8274509803921569f,1.0f),
                                        new Vector4(0.7215686274509804f, 0.9137254901960784f, 0.5647058823529412f,1.0f),
                                        new Vector4(0.9647058823529412f, 0.7254901960784314f, 0.2313725490196078f,1.0f),
                                        new Vector4(0.8980392156862745f, 0.3137254901960784f, 0.2235294117647059f,1.0f),
                                        new Vector4(0.2901960784313725f, 0.4117647058823529f, 0.7411764705882353f,1.0f),
                                        new Vector4(0.3764705882352941f, 0.6392156862745098f, 0.7372549019607843f,1.0f),
                                        new Vector4(0.4705882352941176f, 0.8784313725490196f, 0.5607843137254902f,1.0f),
                                        new Vector4(0.9803921568627451f, 0.596078431372549f, 0.2274509803921569f,1.0f),
                                        new Vector4(0.9215686274509804f, 0.1843137254901961f, 0.0235294117647059f,1.0f),
                                        new Vector4(0.1176470588235294f, 0.2156862745098039f, 0.6f,1.0f),
                                        new Vector4(0.2352941176470588f, 0.3882352941176471f, 0.5098039215686275f,1.0f),
                                        new Vector4(0.2196078431372549f, 0.6784313725490196f, 0.6627450980392157f,1.0f),
                                        new Vector4(0.8980392156862745f, 0.5568627450980392f, 0.1490196078431373f,1.0f),
                                        new Vector4(0.7176470588235294f, 0.0823529411764706f, 0.2509803921568627f,1.0f),
                                        new Vector4(0.0470588235294118f, 0.1411764705882353f, 0.3803921568627451f,1.0f),
                                        new Vector4(0.0392156862745098f, 0.2392156862745098f, 0.3843137254901961f,1.0f),
                                        new Vector4(0.0274509803921569f, 0.6f, 0.5725490196078431f,1.0f)};

        protected int m_priority;
        public int priority
        {
            get
            {
                return m_priority;
            }
            set
            {
                m_priority = value;
                //排序
                s_imGuiViews.Sort((x, y) => { 
                    return x.priority - y.priority;
                });
            }
        }

   


        private static AppImGuiView s_appImGuiView;
        private static Vector2 s_statusBarSize;

        public virtual string Name { get; } = "None";

        public virtual string IconName { get; } = Icon.Get(Icon.Material_tab);

        public ImGuiView(IContext context) : base(context)
        {
            
        }

        public virtual void OnDraw()
        { }

        public static void Render()
        {
           
            //主窗口
            if (s_appImGuiView != null)
            {
                s_appImGuiView.DrawMainMenuBar();
            }


            //tabview
            var viewport = ImGui.GetMainViewport();
            if (s_statusBarSize == Vector2.Zero)
            {
                float lineHight = ImGui.GetTextLineHeight() * 2f;
                s_statusBarSize = new Vector2(viewport.WorkSize.X, lineHight);
            }
            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowSize(viewport.WorkSize - new Vector2(0, s_statusBarSize.Y));
            ImGui.SetNextWindowViewport(viewport.ID);

            if (ImGui.Begin("Main_Tab_Window",s_defaultWindowFlag))
            {
                if (ImGui.BeginTabBar("Git window tabs", ImGuiTabBarFlags.FittingPolicyDefault | ImGuiTabBarFlags.TabListPopupButton | ImGuiTabBarFlags.AutoSelectNewTabs))
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
                        bool visible = ImGui.BeginTabItem(tabWindow.IconName + tabWindow.Name+$"##tab_{tabWindow.Name}_{i}", ref showTab, tabItemFlag);
                        if (ImGui.BeginPopupContextItem("TabItem MenuPopup"))
                        {
                            if (ImGui.MenuItem("Close"))
                            {
                                showTab = false;
                            }

                            if (ImGui.MenuItem("Close the other"))
                            {
                                //for (int m = 0; m < tabWindows.Count; m++)
                                //{
                                //    if (m != i)
                                //        _waitCloseTabIndexs.Add(m);
                                //}
                            }
                            if (ImGui.MenuItem("Close to the right"))
                            {
                                //for (int m = i + 1; m < tabWindows.Count; m++)
                                //{
                                //    _waitCloseTabIndexs.Add(m);
                                //}
                            }
                            if (ImGui.MenuItem("Close all"))
                            {
                                //for (int m = 0; m < tabWindows.Count; m++)
                                //{
                                //    _waitCloseTabIndexs.Add(m);
                                //}
                            }
                            ImGui.EndPopup();
                        }
                        if (ImGui.IsItemHovered())
                        {
                            if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                            {
                                ImGui.OpenPopup("TabItem MenuPopup");
                            }
                        }
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

                        //s_imGuiViews[i].OnDraw();
                    }

                    ImGui.EndTabBar();
                }
            }

            ImGui.End();


            ImGui.SetNextWindowPos(viewport.WorkPos+new Vector2(0, viewport.WorkSize.Y - s_statusBarSize.Y));
            ImGui.SetNextWindowSize(s_statusBarSize);
            ImGui.SetNextWindowViewport(viewport.ID);
            if (ImGui.Begin("Main_Status_Window", s_defaultWindowFlag))
            {
                //主窗口
                if (s_appImGuiView != null)
                {
                    s_appImGuiView.DrawStatusBar();
                }
            }
            ImGui.End();

            //其他界面
            for (int i = 0; i < s_imGuiViews.Count; i++)
            {
                s_imGuiViews[i].OnDraw();
            }
        }

        internal static void Focus(bool lost)
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
            }
        }

        public static T Create<T>(IContext context,int defaultPriority, params object[] pArgs) where T: ImGuiView
        {
            object[] args = null;
            if (context != null || pArgs != null)
            {
                List<object> argsList = new List<object>();
                if (context != null)
                {
                    argsList.Add(context);
                }
                if (pArgs != null)
                {
                    argsList.AddRange(pArgs);
                }
                args = argsList.ToArray();
            }

            T view = Activator.CreateInstance(typeof(T), args) as T;

            if (view is AppImGuiView mainImGuiView)
            {
                s_appImGuiView = mainImGuiView;
            }
            else if (view is ImGuiTabView tabView)
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
                    view?.Dispose();
                    view = null;
                }
            }
            else
            {
                s_imGuiViews.Add(view);
            }

            if (view != null && defaultPriority > 0)
            {
                view.priority = defaultPriority;
            }

            return view;
        }

        public static void Destory(ImGuiView view)
        {
            if (view != null)
            {
                if (s_imGuiViews.Contains(view))
                {
                    s_imGuiViews.Remove(view);
                }

                if (view is ImGuiTabView tabView)
                {
                    if (s_imGuiTabViews.Contains(tabView))
                    {
                        s_imGuiTabViews.Remove(tabView);
                    }
                }

                view.Dispose();
                view = null;
            }
        }

        public static void DestoryAll()
        {
            while (s_imGuiTabViews.Count > 0)
            {
                var view = s_imGuiTabViews[0];
                s_imGuiTabViews.RemoveAt(0);
                view?.Dispose();
                view = null;
            }

            while (s_imGuiViews.Count > 0)
            {
                var view = s_imGuiViews[0];
                s_imGuiViews.RemoveAt(0);
                view?.Dispose();
                view = null;
            }
        }


    }
}

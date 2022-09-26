using ImGuiNET;
using SFB;
using strange.extensions.context.api;
using strange.extensions.context.impl;
using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Linq;
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
                s_appImGuiView.OnDraw();
            }

            //tabview
            var viewport = ImGui.GetMainViewport();
            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowSize(viewport.WorkSize);
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

            //其他界面
            for (int i = 0; i < s_imGuiViews.Count; i++)
            {
                s_imGuiViews[i].OnDraw();
            }
        }


        public static T Create<T>(IContext context,int defaultPriority=0) where T: ImGuiView
        {
            object[] args = null;
            if (context != null)
            {
                args = new object[] { context };
            }
            T view = Activator.CreateInstance(typeof(T), args) as T;

            if (view is AppImGuiView mainImGuiView)
            {
                s_appImGuiView = mainImGuiView;
            }
            else if (view is ImGuiTabView tabView)
            {
                s_imGuiTabViews.Add(tabView);
            }
            else
            {
                s_imGuiViews.Add(view);
            }
            if (defaultPriority > 0)
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

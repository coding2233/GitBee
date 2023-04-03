using ImGuiNET;
using LibGit2Sharp;
using strange.extensions.context.api;
using strange.extensions.context.impl;
using strange.extensions.mediation.api;
using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository;
using Wanderer.GitRepository.Common;
using Wanderer.TextCodeEditor;

namespace Wanderer.App
{
    public class AppContextView : ContextView
    {
        private AppContext m_appContext;
        public AppContextView()
        {
            m_appContext = new AppContext(this, ContextStartupFlags.MANUAL_LAUNCH);
            context = m_appContext;
            //添加子Context
            AddChildContext();
            //启动
            context.Launch();
        }


        //public void SetWindowState(WindowState windowState)
        //{
        //    m_graphicsWindow?.SetWindowState(windowState);
        //}

        internal void OnImGuiRender()
        {
            if (ImGui.Begin("Test"))
            {
                var gitRepoType = typeof(GitRepo);
                BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
                var fields = gitRepoType.GetFields(bindingFlags);
                foreach (var item in fields)
                {
                    ImGui.Text(item.Name+" #### "+item.FieldType.FullName);
                }

                var properties = gitRepoType.GetProperties(bindingFlags);
                foreach (var item in properties)
                {
                    ImGui.Text(item.Name + " #### " + item.PropertyType.FullName );
                }

                var members = gitRepoType.GetMembers(bindingFlags);
                foreach (var item in members)
                {
                    ImGui.Text(item.Name+ " #### "+item.MemberType);
                    //ImGui.Text(item.Name + " -- " + item.MemberType.FullName);
                }

                var methods = gitRepoType.GetMethods(bindingFlags);
                foreach (var item in methods)
                {
                    ImGui.Text(item.ToString());
                    //ImGui.Text(item.Name + " -- " + item.MemberType.FullName);
                }
            }
            ImGui.End();
        }

        protected override void OnViewAdd(IView view)
        { }

        protected override void OnViewRemove(IView view)
        { }


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
    }
}

using LibGit2Sharp;
using strange.extensions.context.api;
using strange.extensions.context.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository;
using Wanderer.TextCodeEditor;

namespace Wanderer.App
{
    public class AppContextView : ContextView
    {
        AppContext m_appContext;
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

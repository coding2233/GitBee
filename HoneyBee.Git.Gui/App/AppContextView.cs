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

        public AppContextView()
        {
            context = new AppContext(this, ContextStartupFlags.MANUAL_LAUNCH);
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

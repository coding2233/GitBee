using strange.extensions.context.api;
using strange.extensions.context.impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Wanderer.Common;
using Wanderer.GitRepository;

namespace Wanderer.App
{
    public class AppContextView : ContextView, IGraphicsRender, IMainLoop
    {
        private GraphicsWindow m_graphicsWindow;

        public AppContextView()
        {
            m_graphicsWindow = new GraphicsWindow(new Vector2(1280,720), SDL_WindowFlags.AllowHighDpi | SDL_WindowFlags.Resizable ,this);
            context = new AppContext(this, ContextStartupFlags.MANUAL_LAUNCH);
            //添加子Context
            AddChildContext();
            //启动
            context.Launch();
        }

        public void OnMainLoop()
        {
            m_graphicsWindow?.Loop();
            Dispose();
        }

        public void OnRender()
        {
            ImGuiView.Render();
        }
        public void SetWindowState(WindowState windowState)
        {
            m_graphicsWindow?.SetWindowState(windowState);
        }

        protected override void OnDestroy()
        {
            m_graphicsWindow?.Dispose();
            base.OnDestroy();
        }
       

        private void AddChildContext()
        {
            //Git仓库 
            new GitRepositoryContext(this);
        }
    }
}

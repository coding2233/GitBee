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

namespace Wanderer.App
{
    public class AppContextView : ContextView
    {
        private Sdl2Window m_sdl2Window;
        private GraphicsDevice m_graphicsDevice;
        private CommandList m_commandList;
        private Vector3 m_clearColor = new Vector3(0.45f, 0.55f, 0.6f);
        private ImGuiController m_imGuiController;

        public AppContextView()
        {
            context = new AppContext(this);
            //窗口
            CreateWindowAndGraphicsDevice();
        }

        public void Loop()
        {
            while (m_sdl2Window != null )
            {
                InputSnapshot snapshot = m_sdl2Window.PumpEvents();
                if (!m_sdl2Window.Exists)
                {
                    break;
                }
                m_imGuiController.Update(1/60.0f, snapshot);

                ImGuiView.Render();
                //ImGuiExample.Show();

                m_commandList.Begin();
                m_commandList.SetFramebuffer(m_graphicsDevice.MainSwapchain.Framebuffer);
                m_commandList.ClearColorTarget(0, new RgbaFloat(m_clearColor.X, m_clearColor.Y, m_clearColor.Z, 1f));
                m_imGuiController.Render(m_graphicsDevice, m_commandList);
                m_commandList.End();
                m_graphicsDevice.SubmitCommands(m_commandList);
                m_graphicsDevice.SwapBuffers(m_graphicsDevice.MainSwapchain);
            }
        }

        protected override void OnDispose()
        {
            m_graphicsDevice?.WaitForIdle();

            m_imGuiController?.Dispose();
            m_commandList?.Dispose();
            m_graphicsDevice?.Dispose();

            base.OnDispose();
        }

        private void CreateWindowAndGraphicsDevice()
        {
            // Create window, GraphicsDevice, and all resources necessary for the demo.
            VeldridStartup.CreateWindowAndGraphicsDevice(
                new WindowCreateInfo(50, 50, 1280, 720, WindowState.Normal, "Honey Bee - Git"),
                new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
                GraphicsBackend.Vulkan,
                out m_sdl2Window,
                out m_graphicsDevice);

            m_commandList = m_graphicsDevice.ResourceFactory.CreateCommandList();
            m_imGuiController = new ImGuiController(m_graphicsDevice, m_graphicsDevice.MainSwapchain.Framebuffer.OutputDescription, m_sdl2Window.Width, m_sdl2Window.Height);

            m_sdl2Window.Resized += () =>
            {
                m_graphicsDevice.MainSwapchain.Resize((uint)m_sdl2Window.Width, (uint)m_sdl2Window.Height);
                m_imGuiController.WindowResized(m_sdl2Window.Width, m_sdl2Window.Height);
            };
        }

    }
}

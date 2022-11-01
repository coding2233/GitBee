using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Veldrid;
using Wanderer.Common;

namespace Wanderer
{
    public interface IGraphicsRender
    {
        void SetWindowState(WindowState windowState);
        void OnRender();
    }

    public class GraphicsWindow : IDisposable
    {
        private Sdl2Window m_sdl2Window;
        private GraphicsDevice m_graphicsDevice;
        private CommandList m_commandList;
        private Vector3 m_clearColor = new Vector3(0.45f, 0.55f, 0.6f);
        private ImGuiController m_imGuiController;
        private IGraphicsRender m_graphicsRender;
        private Vector2 m_lastWindowSize;

        public GraphicsWindow(Vector2 size,SDL_WindowFlags flags, IGraphicsRender graphicsRender)
        {
            m_graphicsRender = graphicsRender;
            //窗口
            m_lastWindowSize = size;
            CreateWindowAndGraphicsDevice(size,flags);
        }

        public void Dispose()
        {
            m_graphicsDevice?.WaitForIdle();

            m_imGuiController?.Dispose();
            m_commandList?.Dispose();
            m_graphicsDevice?.Dispose();
        }

        public void Loop()
        {
            while (m_sdl2Window != null)
            {
                InputSnapshot snapshot = m_sdl2Window.PumpEvents();
                if (!m_sdl2Window.Exists)
                {
                    break;
                }
                m_imGuiController.Update(1 / 30.0f, snapshot);

                if (m_graphicsRender != null)
                {
                    m_graphicsRender.OnRender();
                }

                m_commandList.Begin();
                m_commandList.SetFramebuffer(m_graphicsDevice.MainSwapchain.Framebuffer);
                m_commandList.ClearColorTarget(0, new RgbaFloat(m_clearColor.X, m_clearColor.Y, m_clearColor.Z, 1f));
                m_imGuiController.Render(m_graphicsDevice, m_commandList);
                m_commandList.End();
                m_graphicsDevice.SubmitCommands(m_commandList);
                m_graphicsDevice.SwapBuffers(m_graphicsDevice.MainSwapchain);
            }
        }

        internal void SetWindowState(WindowState windowState)
        {
            if (m_sdl2Window != null)
            {
                m_sdl2Window.WindowState = windowState;
            }
        }

        internal void SetWindowSize(Vector2 size)
        {
            if (m_lastWindowSize != size)
            {
                Sdl2Native.SDL_SetWindowSize(m_sdl2Window.SdlWindowHandle,(int)size.X, (int)size.Y);
                size = m_lastWindowSize;
            }
        }

        private void CreateWindowAndGraphicsDevice(Vector2 size, SDL_WindowFlags flags )
        {
            
            m_sdl2Window = new Sdl2Window("Honybee diff launch", Sdl2Native.SDL_WINDOWPOS_CENTERED, Sdl2Native.SDL_WINDOWPOS_CENTERED, (int)size.X, (int)size.Y, flags, false); //

            m_graphicsDevice = VeldridStartup.CreateGraphicsDevice(m_sdl2Window);
            // Create window, GraphicsDevice, and all resources necessary for the demo.
            //VeldridStartup.CreateWindowAndGraphicsDevice(
            //    new WindowCreateInfo(50, 50, 1280, 720, WindowState.Hidden, "Honey Bee - Git"),
            //    new GraphicsDeviceOptions(true, null, true, ResourceBindingModel.Improved, true, true),
            //    GraphicsBackend.Vulkan,
            //    out m_sdl2Window,
            //    out m_graphicsDevice);


            m_commandList = m_graphicsDevice.ResourceFactory.CreateCommandList();
            m_imGuiController = new ImGuiController(m_graphicsDevice, m_graphicsDevice.MainSwapchain.Framebuffer.OutputDescription, m_sdl2Window.Width, m_sdl2Window.Height);
            m_sdl2Window.Resized += () =>
            {
                m_graphicsDevice.MainSwapchain.Resize((uint)m_sdl2Window.Width, (uint)m_sdl2Window.Height);
                m_imGuiController.WindowResized(m_sdl2Window.Width, m_sdl2Window.Height);
            };

            m_sdl2Window.FocusLost += OnSdl2WindowFocusLost;
            m_sdl2Window.FocusGained += OnSdl2WindowFocusGained;
            //m_sdl2Window.BorderVisible = false;
            //m_sdl2Window.Visible = true;
        }

        private void OnSdl2WindowFocusGained()
        {
            ImGuiView.Focus(false);
        }

        private void OnSdl2WindowFocusLost()
        {
            ImGuiView.Focus(true);
        }

    }
}

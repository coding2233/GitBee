using ImGuiNET;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using Wanderer.Common;
using Wanderer;

namespace Wanderer.App
{
    internal unsafe class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var commandArgs = System.Environment.GetCommandLineArgs();
                Log.Info("Hello, GitBee!");
                int result = Create($"GitBee - {Application.version}", OnImGuiInit, OnImGuiDraw, OnWindowEvent);
            }
            catch (System.Exception e)
            {
                Log.Error("Program throw system exception: {0}", e);
            }
            finally
            {
                Log.ShutDown();
            }
        }


        static IntPtr OnImGuiInit()
        {
            var context = ImGui.CreateContext();

            string iniFilePath = Path.Combine(Path.GetDirectoryName(System.Environment.GetCommandLineArgs()[0]), "imgui.ini");
            fixed (byte* iniFileName = System.Text.Encoding.UTF8.GetBytes(iniFilePath))
            {
                ImGui.GetIO().NativePtr->IniFilename = iniFileName;
            }

            //io->ImeWindowHandle = ofGetWin32Window();
            ////Load default font.
            //var defaultFont = ImGui.GetIO().Fonts.AddFontDefault();

            //字体大小
            int fontSize = 14;
            //Load chinese font.
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("wqy-microhei.ttc"))
            {
                if (stream.Length > 0)
                {
                    byte[] buffer = new byte[stream.Length];
                    stream.Read(buffer, 0, buffer.Length);
                    var fontIntPtr = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0);
                    ImGui.GetIO().Fonts.AddFontFromMemoryTTF(fontIntPtr, fontSize, fontSize, null, ImGui.GetIO().Fonts.GetGlyphRangesChineseFull());
                }
            }

            ImFontConfigPtr imFontConfigPtr = new ImFontConfigPtr(ImGuiNative.ImFontConfig_ImFontConfig())
            {
                OversampleH = 1,
                OversampleV = 1,
                RasterizerMultiply = 1f,
                MergeMode = true,
                PixelSnapH = true,
            };

            ////Load Source code pro font.
            //using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SourceCodePro-Black.ttf"))
            //{
            //    if (stream.Length > 0)
            //    {
            //        byte[] buffer = new byte[stream.Length];
            //        stream.Read(buffer, 0, buffer.Length);
            //        var fontIntPtr = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0);
            //        ImGui.GetIO().Fonts.AddFontFromMemoryTTF(fontIntPtr, fontSize, fontSize, null, ImGui.GetIO().Fonts.GetGlyphRangesDefault());
            //    }
            //}

            //Load icon.
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MaterialIcons-Regular.ttf"))
            {
                if (stream.Length > 0)
                {
                    byte[] buffer = new byte[stream.Length];
                    stream.Read(buffer, 0, buffer.Length);
                    var fontIntPtr = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0);
                    GCHandle rangeHandle = GCHandle.Alloc(new ushort[]
                     {
                            0xe000,
                            0xffff,
                            0
                     }, GCHandleType.Pinned); //0xeb4c
                    var glyphOffset = imFontConfigPtr.GlyphOffset;
                    imFontConfigPtr.GlyphOffset = glyphOffset + new Vector2(0.0f, 3.0f);
                    ImGui.GetIO().Fonts.AddFontFromMemoryTTF(fontIntPtr, fontSize, fontSize, imFontConfigPtr, rangeHandle.AddrOfPinnedObject());
                    if (rangeHandle.IsAllocated)
                    {
                        rangeHandle.Free();
                    }
                    imFontConfigPtr.GlyphOffset = glyphOffset;
                }
            }

            ImGui.GetIO().Fonts.Build();

            //逻辑
            var gitGuiContextView = new AppContextView();
            //字体之类
            return context;
        }


        static void OnImGuiDraw()
        {
            ImGuiView.Render();
        }

        static void OnWindowEvent(int eventType)
        {
            SDL_WindowEventID eventID = (SDL_WindowEventID)eventType;
            if (eventID == SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_GAINED)
            {
                ImGuiView.Focus(false);
            }
            else if (eventID == SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_LOST)
            {
                ImGuiView.Focus(true);
            }
            //Log.Info("OnWindowEvent: {0} {1}", eventID, eventType);
        }

        #region native
        enum SDL_WindowEventID
        {
            SDL_WINDOWEVENT_NONE,           /**< Never used */
            SDL_WINDOWEVENT_SHOWN,          /**< Window has been shown */
            SDL_WINDOWEVENT_HIDDEN,         /**< Window has been hidden */
            SDL_WINDOWEVENT_EXPOSED,        /**< Window has been exposed and should be
                                         redrawn */
            SDL_WINDOWEVENT_MOVED,          /**< Window has been moved to data1, data2
                                     */
            SDL_WINDOWEVENT_RESIZED,        /**< Window has been resized to data1xdata2 */
            SDL_WINDOWEVENT_SIZE_CHANGED,   /**< The window size has changed, either as
                                         a result of an API call or through the
                                         system or user changing the window size. */
            SDL_WINDOWEVENT_MINIMIZED,      /**< Window has been minimized */
            SDL_WINDOWEVENT_MAXIMIZED,      /**< Window has been maximized */
            SDL_WINDOWEVENT_RESTORED,       /**< Window has been restored to normal size
                                         and position */
            SDL_WINDOWEVENT_ENTER,          /**< Window has gained mouse focus */
            SDL_WINDOWEVENT_LEAVE,          /**< Window has lost mouse focus */
            SDL_WINDOWEVENT_FOCUS_GAINED,   /**< Window has gained keyboard focus */
            SDL_WINDOWEVENT_FOCUS_LOST,     /**< Window has lost keyboard focus */
            SDL_WINDOWEVENT_CLOSE,          /**< The window manager requests that the window be closed */
            SDL_WINDOWEVENT_TAKE_FOCUS,     /**< Window is being offered a focus (should SetWindowInputFocus() on itself or a subwindow, or ignore) */
            SDL_WINDOWEVENT_HIT_TEST,       /**< Window had a hit test that wasn't SDL_HITTEST_NORMAL. */
            SDL_WINDOWEVENT_ICCPROF_CHANGED,/**< The ICC profile of the window's display has changed. */
            SDL_WINDOWEVENT_DISPLAY_CHANGED
        }

        delegate IntPtr IMGUI_INIT_CALLBACK();
        delegate void IMGUI_DRAW_CALLBACK();
        delegate void WINDOW_EVENT_CALLBACK(int event_type);
        [DllImport("iiso3.dll")]
        extern static int Create(string title, IMGUI_INIT_CALLBACK imgui_init_cb, IMGUI_DRAW_CALLBACK imgui_draw_cb, WINDOW_EVENT_CALLBACK window_event_type);
    
        #endregion
    }

}
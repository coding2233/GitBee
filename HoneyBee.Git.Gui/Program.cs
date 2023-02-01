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
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace Wanderer.App
{
    internal unsafe class Program
    {

        static void Main(string[] args)
        {
            bool showLaunch = false;
            foreach (string arg in args)
            {
                Log.Info("Hello, GitBee! -------------------- {0}", arg);

                if (arg.Equals("$LAUNCH"))
                {
                    showLaunch = true;
                    break;
                }
            }

            if (showLaunch)
            {
                IAppWindow window = new AppLaunchWindow();
                long sdl_window = 0;
                Create("", (uint)SDLWindowFlag.SDL_WINDOW_BORDERLESS, 600, 380, &sdl_window, window.OnImGuiInit, window.OnImGuiDraw, window.OnWindowEvent);
            }
            else
            {
                //这里做一下保护判断，避免参数错误的无限启动
                if (args.Length > 0)
                {
                    return;
                }


                try
                {
                    Process launchProcess = null;
                    if (System.OperatingSystem.IsWindows())
                    {
                        string appExecName = $"{Application.DataPath}/{Assembly.GetExecutingAssembly().GetName().Name}.exe";
                        if (File.Exists(appExecName))
                        {
                            ProcessStartInfo processStartInfo = new ProcessStartInfo();
                            processStartInfo.FileName = appExecName;
                            processStartInfo.WorkingDirectory = Application.DataPath;
                            processStartInfo.Arguments = "$LAUNCH";
                            processStartInfo.UseShellExecute = true;
                            launchProcess = Process.Start(processStartInfo);
                        }
                    }

                    var commandArgs = System.Environment.GetCommandLineArgs();
                    Log.Info("Hello, GitBee! \n{0}", commandArgs[0]);
                    LuaPlugin.Enable();
                    long sdl_window = 0;
                    IAppWindow window = new AppMainWindow(launchProcess, &sdl_window);
                    int result = Create($"GitBee - {Application.version}", (uint)SDLWindowFlag.SDL_WINDOW_HIDDEN, 0, 0, &sdl_window,window.OnImGuiInit, window.OnImGuiDraw, window.OnWindowEvent);
                    LuaPlugin.Disable();
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
        }

        #region native
        enum SDLWindowFlag:uint
        {
            SDL_WINDOW_HIDDEN = 0x00000008,             /**< window is not visible */
            SDL_WINDOW_BORDERLESS = 0x00000010,         /**< no window decoration */
        }

        delegate IntPtr IMGUI_INIT_CALLBACK();
        delegate void IMGUI_DRAW_CALLBACK();
        delegate void WINDOW_EVENT_CALLBACK(int event_type);
        [DllImport("iiso3.dll")]
        extern static int Create(string title,uint flags, int window_width, int window_height, void* sdl_window, IMGUI_INIT_CALLBACK imgui_init_cb, IMGUI_DRAW_CALLBACK imgui_draw_cb, WINDOW_EVENT_CALLBACK window_event_type);

        #endregion
    }


    internal interface IAppWindow
    {
        IntPtr OnImGuiInit();
        void OnImGuiDraw();
        void OnWindowEvent(int eventType);

    }

    internal unsafe class AppMainWindow: IAppWindow
    {
        Process m_launchProcess;
        long* m_sdlWindow;
        public AppMainWindow(Process launchProcess,long* sdl_window)
        {
            m_sdlWindow = sdl_window;
            m_launchProcess = launchProcess;
            if (m_launchProcess != null)
            {

            }
        }

        public IntPtr OnImGuiInit()
        {
            var context = ImGui.CreateContext();

            string iniFilePath = Path.Combine(Application.TempPath, "imgui.ini");
            fixed (byte* iniFileName = System.Text.Encoding.UTF8.GetBytes(iniFilePath))
            {
                ImGui.GetIO().NativePtr->IniFilename = iniFileName;
            }

            string logFilePath = Path.Combine(Application.TempPath, "log.ini");
            fixed (byte* logFileName = System.Text.Encoding.UTF8.GetBytes(logFilePath))
            {
                ImGui.GetIO().NativePtr->LogFilename = logFileName;
            }

            //io->ImeWindowHandle = ofGetWin32Window();
            ////Load default font.
            //var defaultFont = ImGui.GetIO().Fonts.AddFontDefault();

            string fontPath = LuaPlugin.GetString("Style", "Font");
            if (string.IsNullOrEmpty(fontPath))
            {
                fontPath = "lua/style/fonts/wqy-microhei.ttc";
            }

            if (!File.Exists(fontPath))
            {
                //windows字体保险
                fontPath = @"C:\\Windows\\Fonts\\msyh.ttc";
            }

            //字体大小
            int fontSize = (int)LuaPlugin.GetNumber("Style", "FontSize");
            if (fontSize <= 0)
            {
                fontSize = 14;
            }

            if (File.Exists(fontPath))
            {
                var glyphRanges = LuaPlugin.GetString("Style", "GlyphRanges");
                IntPtr glyphRangesPtr = IntPtr.Zero;
                switch (glyphRanges)
                {
                    case "GetGlyphRangesDefault":
                        glyphRangesPtr = ImGui.GetIO().Fonts.GetGlyphRangesDefault();
                        break;
                    case "GetGlyphRangesChineseFull":
                        glyphRangesPtr = ImGui.GetIO().Fonts.GetGlyphRangesChineseFull();
                        break;
                    case "GetGlyphRangesChineseSimplifiedCommon":
                        glyphRangesPtr = ImGui.GetIO().Fonts.GetGlyphRangesChineseSimplifiedCommon();
                        break;
                    case "GetGlyphRangesCyrillic":
                        glyphRangesPtr = ImGui.GetIO().Fonts.GetGlyphRangesCyrillic();
                        break;
                    case "GetGlyphRangesJapanese":
                        glyphRangesPtr = ImGui.GetIO().Fonts.GetGlyphRangesJapanese();
                        break;
                    case "GetGlyphRangesKorean":
                        glyphRangesPtr = ImGui.GetIO().Fonts.GetGlyphRangesKorean();
                        break;
                    case "GetGlyphRangesThai":
                        glyphRangesPtr = ImGui.GetIO().Fonts.GetGlyphRangesThai();
                        break;
                    case "GetGlyphRangesVietnamese":
                        glyphRangesPtr = ImGui.GetIO().Fonts.GetGlyphRangesVietnamese();
                        break;
                    default:
                        if (!string.IsNullOrEmpty(glyphRanges) && File.Exists(glyphRanges))
                        {
                            var imFontGlyphRangesBuilder = ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder();
                            var textBytes = File.ReadAllBytes(glyphRanges);
                            if (textBytes != null && textBytes.Length > 0)
                            {
                                ImVector outRanges;
                                fixed (byte* text = textBytes)
                                {
                                    //默认字符
                                    ImGuiNative.ImFontGlyphRangesBuilder_AddRanges(imFontGlyphRangesBuilder, (ushort*)ImGui.GetIO().Fonts.GetGlyphRangesDefault());
                                    ImGuiNative.ImFontGlyphRangesBuilder_AddText(imFontGlyphRangesBuilder, text, text + textBytes.Length);
                                    ImGuiNative.ImFontGlyphRangesBuilder_BuildRanges(imFontGlyphRangesBuilder, &outRanges);
                                    glyphRangesPtr = outRanges.Data;
                                }
                            }
                        }
                        break;
                }
                if (glyphRangesPtr == IntPtr.Zero)
                {
                    glyphRangesPtr = ImGui.GetIO().Fonts.GetGlyphRangesChineseFull();
                }

                ImGui.GetIO().Fonts.AddFontFromFileTTF(fontPath, fontSize, null, glyphRangesPtr);
            }
            else
            {
                ImGui.GetIO().Fonts.AddFontDefault();
            }

            ImFontConfigPtr imFontConfigPtr = new ImFontConfigPtr(ImGuiNative.ImFontConfig_ImFontConfig())
            {
                OversampleH = 1,
                OversampleV = 1,
                RasterizerMultiply = 1f,
                MergeMode = true,
                PixelSnapH = true,
            };

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

            Console.WriteLine((int)m_sdlWindow);
            if (m_launchProcess != null)
            {
                Thread.Sleep(5000);
                m_launchProcess.Kill();
                m_launchProcess = null;
                SDLSetWindowShow(new IntPtr(m_sdlWindow));
            }

            //字体之类
            return context;
        }

        public void OnImGuiDraw()
        {
            ImGuiView.Render();
        }

        public void OnWindowEvent(int eventType)
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

        [DllImport("iiso3.dll")]
        static extern void SDLSetWindowShow(IntPtr sdl_window);
        #endregion

    }


    internal unsafe class AppLaunchWindow : IAppWindow
    {
        public void OnImGuiDraw()
        {
            ImGui.Text("GitBee - A Lightweight Git interface management tool");
        }

        public IntPtr OnImGuiInit()
        {
            var context = ImGui.CreateContext();
            return context;
        }

        public void OnWindowEvent(int eventType)
        {
        }
    }

}


















////Load chinese font.
//using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("wqy-microhei.ttc"))
//{
//    if (stream.Length > 0)
//    {
//        byte[] buffer = new byte[stream.Length];
//        stream.Read(buffer, 0, buffer.Length);
//        var fontIntPtr = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0);
//        //ImGui.GetIO().Fonts.GetGlyphRangesChineseFull() 内存占用太高 占用200+MB
//        //ImGui.GetIO().Fonts.GetGlyphRangesChineseSimplifiedCommon 文字不全 40-50MB
//        IntPtr glyphRanges = IntPtr.Zero;
//        //使用自定义文本字符集
//        string chineseText = "lua/style/chinese.txt";
//        if (File.Exists(chineseText))
//        {
//            var imFontGlyphRangesBuilder = ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder();
//            var textBytes = File.ReadAllBytes(chineseText);
//            if (textBytes != null && textBytes.Length > 0)
//            {
//                fixed (byte* text = textBytes)
//                {
//                    //默认字符
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddRanges(imFontGlyphRangesBuilder, (ushort*)ImGui.GetIO().Fonts.GetGlyphRangesDefault());
//                    //默认中文符号
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '，');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '：');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '、');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '！');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '（');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '）');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '￥');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '“');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '”');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '；');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '。');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '？');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '【');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '】');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '《');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddChar(imFontGlyphRangesBuilder, '》');
//                    ImGuiNative.ImFontGlyphRangesBuilder_AddText(imFontGlyphRangesBuilder, text, text + textBytes.Length);
//                    ImVector outRanges;
//                    ImGuiNative.ImFontGlyphRangesBuilder_BuildRanges(imFontGlyphRangesBuilder, &outRanges);
//                    glyphRanges = outRanges.Data;
//                }
//            }
//        }
//        //默认选择
//        if (glyphRanges == IntPtr.Zero)
//        {
//            glyphRanges = ImGui.GetIO().Fonts.GetGlyphRangesChineseFull();
//        }
//        ImGui.GetIO().Fonts.AddFontFromMemoryTTF(fontIntPtr, fontSize, fontSize, null, glyphRanges);
//    }
//}
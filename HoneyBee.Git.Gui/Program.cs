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

            if (args != null && args.Length > 0)
            {
                switch (args[0])
                {
                    case "$LAUNCH":
                        showLaunch = true;
                        break;
                    case "--version":
                        LuaPlugin.UpdateVersion();
                        Console.Write(Application.GetVersion().ToString());
                        return;
                    default:
                        break;
                }
            }
            
            if (showLaunch)
            {
                 IAppWindow window = new AppLaunchWindow();
                //SDLWindowFlag.SDL_WINDOW_ALWAYS_ON_TOP|
                var sdlWindow = CreateSdlWindow("", 600, 360, (uint)(SDLWindowFlag.SDL_WINDOW_BORDERLESS| SDLWindowFlag.SDL_WINDOW_SKIP_TASKBAR));
                CreateRender(sdlWindow, window.OnImGuiInit, window.OnImGuiDraw, window.OnSDLEvent);
            }
            else
            {
                //这里做一下保护判断，避免参数错误的无限启动
                if (args.Length > 0)
                {
                    Console.Write("args error.");
                    return;
                }


                try
                {
                    Process launchProcess = null;
#if !DEBUG
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
#endif
                    var commandArgs = System.Environment.GetCommandLineArgs();
                    Log.Info("Hello, GitBee! \n{0}", commandArgs[0]);
                    LuaPlugin.Enable();

                    uint windowFlag = launchProcess == null ? 0 : (uint)SDLWindowFlag.SDL_WINDOW_HIDDEN;
                    var sdlWindow = CreateSdlWindow($"GitBee - {Application.GetVersion().ToFullString()}", 0, 0, windowFlag);
                    IAppWindow window = new AppMainWindow(launchProcess, sdlWindow);

                    CreateRender(sdlWindow, window.OnImGuiInit, window.OnImGuiDraw, window.OnSDLEvent);

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
            SDL_WINDOW_ALWAYS_ON_TOP = 0x00008000,   /**< window should always be above others */
            SDL_WINDOW_SKIP_TASKBAR = 0x00010000,   /**< window should not be added to the taskbar */
        }

        delegate IntPtr IMGUI_INIT_CALLBACK();
        delegate void IMGUI_DRAW_CALLBACK();
        delegate void SDL_EVENT_CALLBACK(int type,void * data);
        [DllImport("iiso3.dll")]
        extern static IntPtr CreateSdlWindow(string title, int window_width, int window_height, uint flags);
        [DllImport("iiso3.dll")]
        extern static int CreateRender(IntPtr sdl_window, IMGUI_INIT_CALLBACK imgui_init_cb, IMGUI_DRAW_CALLBACK imgui_draw_cb, SDL_EVENT_CALLBACK sdl_event_type);
        #endregion
    }


    internal unsafe interface IAppWindow
    {
        IntPtr OnImGuiInit();
        void OnImGuiDraw();
        void OnSDLEvent(int type,void* data);

    }

    internal unsafe class AppMainWindow : IAppWindow
    {
        Process m_launchProcess;
        IntPtr m_sdlWindow;
        AppContextView m_appContextView;

        public AppMainWindow(Process launchProcess, IntPtr sdlWindow)
        {
            m_sdlWindow = sdlWindow;
            m_launchProcess = launchProcess;
            if (m_launchProcess != null)
            {

            }
        }

        public IntPtr OnImGuiInit()
        {
            var context = ImGui.CreateContext();

            //string iniFilePath = Path.Combine(Application.TempDataPath, "imgui.ini");
            //fixed (byte* iniFileName = System.Text.Encoding.UTF8.GetBytes(iniFilePath))
            //{
            //    ImGui.GetIO().NativePtr->IniFilename = iniFileName;
            //}

            //string logFilePath = Path.Combine(Application.TempDataPath, "log.ini");
            //fixed (byte* logFileName = System.Text.Encoding.UTF8.GetBytes(logFilePath))
            //{
            //    ImGui.GetIO().NativePtr->LogFilename = logFileName;
            //}

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

            //普通的AsciiFont
            string asciiFontPath = LuaPlugin.GetString("Style", "AsciiFont");
            if (!string.IsNullOrEmpty(asciiFontPath) && File.Exists(asciiFontPath))
            {
                ImGui.GetIO().Fonts.AddFontFromFileTTF(asciiFontPath, fontSize, null, ImGui.GetIO().Fonts.GetGlyphRangesDefault());
            }

            ImGui.GetIO().Fonts.Build();

            //逻辑
            m_appContextView = new AppContextView();

            if (m_launchProcess != null)
            {
                Thread.Sleep(500);
                m_launchProcess.Kill();
                m_launchProcess = null;
                SDLSetWindowShow(m_sdlWindow);
            }

            //字体之类
            return context;
        }

        public void OnImGuiDraw()
        {
            ImGuiView.Render();
        }

     
        public void OnSDLEvent(int type , void* data)
        {
            SDL_EventType sdlType = (SDL_EventType)type;
            if (sdlType == SDL_EventType.SDL_WINDOWEVENT)
            {
                SDL_WindowEvent* windowEvent = (SDL_WindowEvent*)(data);
                SDL_WindowEventID eventID = (SDL_WindowEventID)windowEvent->window_event;
                if (eventID == SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_GAINED)
                {
                    ImGuiView.Focus(false);
                }
                else if (eventID == SDL_WindowEventID.SDL_WINDOWEVENT_FOCUS_LOST)
                {
                    ImGuiView.Focus(true);
                }
            }
            else if (sdlType == SDL_EventType.SDL_DROPFILE)
            {
                SDL_DropEvent* dropEvent = (SDL_DropEvent*)(data);
                string file = Util.StringFromPtr(dropEvent->file);
                if (m_appContextView != null && !string.IsNullOrEmpty(file))
                {
                    Log.Info("drop file {0}", file);
                    m_appContextView.OnDropFileEvent(file);
                }
            }

            //Log.Info("OnWindowEvent: {0} {1}", eventID, eventType);
        }


        #region native

        enum SDL_EventType
        {
            SDL_FIRSTEVENT = 0,     /**< Unused (do not remove) */

            /* Application events */
            SDL_QUIT = 0x100, /**< User-requested quit */

            /* These application events have special meaning on iOS, see README-ios.md for details */
            SDL_APP_TERMINATING,        /**< The application is being terminated by the OS
                                     Called on iOS in applicationWillTerminate()
                                     Called on Android in onDestroy()
                                */
            SDL_APP_LOWMEMORY,          /**< The application is low on memory, free memory if possible.
                                     Called on iOS in applicationDidReceiveMemoryWarning()
                                     Called on Android in onLowMemory()
                                */
            SDL_APP_WILLENTERBACKGROUND, /**< The application is about to enter the background
                                     Called on iOS in applicationWillResignActive()
                                     Called on Android in onPause()
                                */
            SDL_APP_DIDENTERBACKGROUND, /**< The application did enter the background and may not get CPU for some time
                                     Called on iOS in applicationDidEnterBackground()
                                     Called on Android in onPause()
                                */
            SDL_APP_WILLENTERFOREGROUND, /**< The application is about to enter the foreground
                                     Called on iOS in applicationWillEnterForeground()
                                     Called on Android in onResume()
                                */
            SDL_APP_DIDENTERFOREGROUND, /**< The application is now interactive
                                     Called on iOS in applicationDidBecomeActive()
                                     Called on Android in onResume()
                                */

            SDL_LOCALECHANGED,  /**< The user's locale preferences have changed. */

            /* Display events */
            SDL_DISPLAYEVENT = 0x150,  /**< Display state change */

            /* Window events */
            SDL_WINDOWEVENT = 0x200, /**< Window state change */
            SDL_SYSWMEVENT,             /**< System specific event */

            /* Keyboard events */
            SDL_KEYDOWN = 0x300, /**< Key pressed */
            SDL_KEYUP,                  /**< Key released */
            SDL_TEXTEDITING,            /**< Keyboard text editing (composition) */
            SDL_TEXTINPUT,              /**< Keyboard text input */
            SDL_KEYMAPCHANGED,          /**< Keymap changed due to a system event such as an
                                     input language or keyboard layout change.
                                */
            SDL_TEXTEDITING_EXT,       /**< Extended keyboard text editing (composition) */

            /* Mouse events */
            SDL_MOUSEMOTION = 0x400, /**< Mouse moved */
            SDL_MOUSEBUTTONDOWN,        /**< Mouse button pressed */
            SDL_MOUSEBUTTONUP,          /**< Mouse button released */
            SDL_MOUSEWHEEL,             /**< Mouse wheel motion */

            /* Joystick events */
            SDL_JOYAXISMOTION = 0x600, /**< Joystick axis motion */
            SDL_JOYBALLMOTION,          /**< Joystick trackball motion */
            SDL_JOYHATMOTION,           /**< Joystick hat position change */
            SDL_JOYBUTTONDOWN,          /**< Joystick button pressed */
            SDL_JOYBUTTONUP,            /**< Joystick button released */
            SDL_JOYDEVICEADDED,         /**< A new joystick has been inserted into the system */
            SDL_JOYDEVICEREMOVED,       /**< An opened joystick has been removed */
            SDL_JOYBATTERYUPDATED,      /**< Joystick battery level change */

            /* Game controller events */
            SDL_CONTROLLERAXISMOTION = 0x650, /**< Game controller axis motion */
            SDL_CONTROLLERBUTTONDOWN,          /**< Game controller button pressed */
            SDL_CONTROLLERBUTTONUP,            /**< Game controller button released */
            SDL_CONTROLLERDEVICEADDED,         /**< A new Game controller has been inserted into the system */
            SDL_CONTROLLERDEVICEREMOVED,       /**< An opened Game controller has been removed */
            SDL_CONTROLLERDEVICEREMAPPED,      /**< The controller mapping was updated */
            SDL_CONTROLLERTOUCHPADDOWN,        /**< Game controller touchpad was touched */
            SDL_CONTROLLERTOUCHPADMOTION,      /**< Game controller touchpad finger was moved */
            SDL_CONTROLLERTOUCHPADUP,          /**< Game controller touchpad finger was lifted */
            SDL_CONTROLLERSENSORUPDATE,        /**< Game controller sensor was updated */

            /* Touch events */
            SDL_FINGERDOWN = 0x700,
            SDL_FINGERUP,
            SDL_FINGERMOTION,

            /* Gesture events */
            SDL_DOLLARGESTURE = 0x800,
            SDL_DOLLARRECORD,
            SDL_MULTIGESTURE,

            /* Clipboard events */
            SDL_CLIPBOARDUPDATE = 0x900, /**< The clipboard or primary selection changed */

            /* Drag and drop events */
            SDL_DROPFILE = 0x1000, /**< The system requests a file open */
            SDL_DROPTEXT,                 /**< text/plain drag-and-drop event */
            SDL_DROPBEGIN,                /**< A new set of drops is beginning (NULL filename) */
            SDL_DROPCOMPLETE,             /**< Current set of drops is now complete (NULL filename) */

            /* Audio hotplug events */
            SDL_AUDIODEVICEADDED = 0x1100, /**< A new audio device is available */
            SDL_AUDIODEVICEREMOVED,        /**< An audio device has been removed. */

            /* Sensor events */
            SDL_SENSORUPDATE = 0x1200,     /**< A sensor was updated */

            /* Render events */
            SDL_RENDER_TARGETS_RESET = 0x2000, /**< The render targets have been reset and their contents need to be updated */
            SDL_RENDER_DEVICE_RESET, /**< The device has been reset and all textures need to be recreated */

            /* Internal events */
            SDL_POLLSENTINEL = 0x7F00, /**< Signals the end of an event poll cycle */

            /** Events ::SDL_USEREVENT through ::SDL_LASTEVENT are for your use,
             *  and should be allocated with SDL_RegisterEvents()
             */
            SDL_USEREVENT = 0x8000,

            /**
             *  This last event is only for bounding internal arrays
             */
            SDL_LASTEVENT = 0xFFFF
        }

        struct SDL_WindowEvent
        {
            public uint type;        /**< ::SDL_WINDOWEVENT */
            public uint timestamp;   /**< In milliseconds, populated using SDL_GetTicks() */
            public uint windowID;    /**< The associated window */
            public byte window_event;        /**< ::SDL_WindowEventID */
            public byte padding1;
            public byte padding2;
            public byte padding3;
            public int data1;       /**< event dependent data */
            public int data2;       /**< event dependent data */
        }
  

        enum SDL_WindowEventID:byte
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

        struct SDL_DropEvent
        {
            public uint type;        /**< ::SDL_DROPBEGIN or ::SDL_DROPFILE or ::SDL_DROPTEXT or ::SDL_DROPCOMPLETE */
            public  uint timestamp;   /**< In milliseconds, populated using SDL_GetTicks() */
            public byte* file;         /**< The file name, which should be freed with SDL_free(), is NULL on begin/complete */
            public uint windowID;    /**< The window that was dropped on, if any */
        }

        [DllImport("iiso3.dll")]
        static extern void SDLSetWindowShow(IntPtr sdl_window);
        #endregion

    }


    internal unsafe class AppLaunchWindow : IAppWindow
    {
        private const string m_showTipText = "GitBee - A Lightweight Git interface management tool";
        public void OnImGuiDraw()
        {
            //tabview
            var viewport = ImGui.GetMainViewport();

            //mainview
            ImGui.SetNextWindowPos(viewport.WorkPos);
            ImGui.SetNextWindowSize(viewport.WorkSize);
            //ImGui.SetNextWindowViewport(viewport.ID);
            if (ImGui.Begin("AppLaunchWindow", ImGuiWindowFlags.NoMove| ImGuiWindowFlags.NoResize| ImGuiWindowFlags.NoTitleBar| ImGuiWindowFlags.NoInputs))
            {
                float cursorPosX = 0;
                var glTexture = Application.LoadTextureFromFile("lua/style/launch.png");
                if (glTexture.Image != IntPtr.Zero)
                {
                    Vector2 textureSize = Vector2.One * 128;
                    cursorPosX = (viewport.WorkSize.X - textureSize.X) * 0.5f;
                    float cursorPosY = (viewport.WorkSize.Y - (textureSize.Y+ ImGui.GetTextLineHeight()*3)) * 0.5f;
                    ImGui.SetCursorPosX(cursorPosX);
                    ImGui.SetCursorPosY(cursorPosY);
                    ImGui.Image(glTexture.Image, textureSize);
                }
                var textSize = ImGui.CalcTextSize(m_showTipText);
                cursorPosX = (viewport.WorkSize.X - textSize.X) * 0.5f;
                ImGui.SetCursorPosX(cursorPosX);
                ImGui.Text(m_showTipText);
            }
            ImGui.End();
        }

        public IntPtr OnImGuiInit()
        {
            var context = ImGui.CreateContext();
            return context;
        }

        public void OnSDLEvent(int eventType, void* data)
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
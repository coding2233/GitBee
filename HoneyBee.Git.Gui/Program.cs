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
                Log.Info("Hello, Honey Bee - Git!");
                int result = Create($"Honybee Git - {Application.version}", OnImGuiInit, OnImGuiDraw);
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

        #region native
        delegate IntPtr IMGUI_INIT_CALLBACK();
        delegate void IMGUI_DRAW_CALLBACK();
        [DllImport("iiso3.dll")]
        extern static int Create(string title, IMGUI_INIT_CALLBACK imgui_init_cb, IMGUI_DRAW_CALLBACK imgui_draw_cb);
    
        #endregion
    }

}
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;

namespace Wanderer
{
    internal unsafe class ImGuiMarkDown
    {
        private static MarkdownImageData s_imageData;

        private static byte[] s_textBuffer;
        private static string s_mdPathRoot;
        private static Dictionary<long, GLTexture> s_textures = new Dictionary<long, GLTexture>();
        private static float s_oldFontSacle = 1.0f;

        internal static bool IsValid
        {
            get
            {
                return s_textBuffer != null;
            }
        }

        internal static void SetMarkdownPath(string mdPath)
        {
            s_textBuffer = null;
            s_mdPathRoot = null;
            s_textures.Clear();

            if (!string.IsNullOrEmpty(mdPath) && File.Exists(mdPath))
            {
                s_mdPathRoot = Path.GetDirectoryName(mdPath);
                s_textBuffer = File.ReadAllBytes(mdPath);
                s_imageData = new MarkdownImageData();
            }
        }

        internal static unsafe void Render()
        {
            if (s_textBuffer == null)
            {
                return;
            }

            fixed (byte* text = s_textBuffer)
            {
                MarkDown(text, s_textBuffer.Length, OnImageCallback, OnHeadingCallback);
            }
        }

        private static void OnHeadingCallback(int level, bool start)
        {
            //s_oldFontSacle = ImGui.GetFont().Scale;
            //if (start)
            //{
            //    ImGui.GetFont().Scale = 1.5f - 0.1f * level;
            //    ImGui.PushFont(ImGui.GetFont());
            //}
            //else
            //{
            //    ImGui.GetFont().Scale = 1.0f;
            //    ImGui.PopFont();
            //}
        }


        private static MarkdownImageData OnImageCallback(MarkdownLinkCallbackData imageLinkData)
        {
            long linkPtr = (long)imageLinkData.link;
            GLTexture glTexture;
            if (!s_textures.TryGetValue(linkPtr, out glTexture))
            {
                string linkPath = System.Text.Encoding.UTF8.GetString(imageLinkData.link, imageLinkData.linkLength);
                linkPath = Path.Combine(s_mdPathRoot, linkPath);
                glTexture = Application.LoadTextureFromFile(linkPath);
                s_textures.Add(linkPtr, glTexture);
            }

            s_imageData.isValid = glTexture.Image != IntPtr.Zero;
            if (s_imageData.isValid)
            {
                s_imageData.user_texture_id = glTexture.Image;
                s_imageData.size = ResizeImage(glTexture.Size, ImGui.GetWindowWidth() * 0.35f * Vector2.One);
                //s_imageData.uv0 = Vector2.Zero;
                //s_imageData.uv1 = Vector2.One;
                //s_imageData.tint_col = Vector4.One;
                //s_imageData.border_col = Vector4.Zero;
            }
            return s_imageData;
        }


        private static Vector2 ResizeImage(Vector2 textureSize, Vector2 targetSize)
        {
            if (textureSize == Vector2.Zero)
            {
                return targetSize;
            }

            var scaleSize = targetSize / textureSize;
            float scale = Math.Min(scaleSize.X, scaleSize.Y);
            targetSize = textureSize * scale;
            return targetSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MarkdownImageData
        {
            public bool isValid ;
            public bool useLinkCallback ;
            public IntPtr user_texture_id;
            public Vector2 size;
            public Vector2 uv0;
            public Vector2 uv1;
            public Vector4 tint_col;
            public Vector4 border_col;

            public MarkdownImageData()
            {
                isValid = false;
                useLinkCallback = false;
                user_texture_id= IntPtr.Zero;
                size = Vector2.One;
                uv0 = Vector2.Zero;
                uv1 = Vector2.One;
                tint_col = Vector4.One;
                border_col = Vector4.Zero;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MarkdownLinkCallbackData
        {
            public byte* text;                               // text between square brackets []
            public int textLength;
            public byte* link;                               // text between brackets ()
            public int linkLength;
            public void* userData;
            public bool isImage;
        }

        delegate MarkdownImageData MarkdownImageCallback(MarkdownLinkCallbackData data);
        delegate void MARKDOWN_HEADING_CALLBACK(int level, bool start);

        [DllImport("iiso3.dll")]
        static extern void MarkDown(byte* markdown_text, int size, MarkdownImageCallback image_callback, MARKDOWN_HEADING_CALLBACK heading_callback);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.Common
{
    public unsafe static class Application
    {
        private static Version s_version;
        public static Version GetVersion()
        {
            if (string.IsNullOrEmpty(s_version.PreVersion))
            {
                s_version= new Version() { Major = 0, Minor = 1, Patch = 16, PreVersion = "alpha" };
            }
            return s_version;
        }

        private static Dictionary<string, GLTexture> s_glTextures = new Dictionary<string, GLTexture>();
        private static Dictionary<string, string> s_networkGLTextures = new Dictionary<string, string>();

        private static string m_dataPath;
        private static string m_userPath;
        private static string m_tempPath;
        private static string m_tempDataPath;

        internal static string UpdateDownloadURL;

        internal const float FontOffset = 3.0f;

        public static string DataPath
        {
            get
            {
                if (string.IsNullOrEmpty(m_dataPath))
                {
                    var args = System.Environment.GetCommandLineArgs();
                    m_dataPath = Path.GetDirectoryName(args[0]);
                    Log.Info("DataPath: {0}", m_dataPath);
                }
                if (!Directory.Exists(m_dataPath))
                {
                    Directory.CreateDirectory(m_dataPath);
                }
                return m_dataPath;
            }
        }

        public static string UserPath
        {
            get
            {
                if (string.IsNullOrEmpty(m_userPath))
                {
                    m_userPath = Environment.GetEnvironmentVariable("USERPROFILE");
                    if (string.IsNullOrEmpty(m_userPath))
                    {
                        m_userPath = "./";
                    }
                    m_userPath = Path.Combine(m_userPath, $".{AppDomain.CurrentDomain.FriendlyName}");
                    Log.Info("UserPath: {0}",m_userPath);
                }
                if (!Directory.Exists(m_userPath))
                {
                    Directory.CreateDirectory(m_userPath);
                }
                return m_userPath;
            }
        }

        public static string TempDataPath
        {
            get
            {
                if (string.IsNullOrEmpty(m_tempDataPath))
                {
                    m_tempDataPath = Path.Combine(DataPath,"temp");
                    if (!Directory.Exists(m_tempDataPath))
                    {
                        Directory.CreateDirectory(m_tempDataPath);
                    }
                }
                return m_tempDataPath;
            }
        }

        public static string TempPath
        {
            get
            {
                if (string.IsNullOrEmpty(m_tempPath))
                {
                    m_tempPath = Path.Combine(UserPath, "temp");
                    if (!Directory.Exists(m_tempPath))
                    {
                        Directory.CreateDirectory(m_tempPath);
                    }
                }
                return m_tempPath;
            }
        }

        public static string GetStringMd5(string str)
        {
            string fileMD5 =  GetBytesMd5(System.Text.Encoding.UTF8.GetBytes(str));
            return fileMD5;
        }

        public static string GetBytesMd5(byte[] data)
        {
            System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
            byte[] toData = md5.ComputeHash(data);
            string fileMD5 = BitConverter.ToString(toData).Replace("-", "").ToLower();
            return fileMD5;
        }

      
        //internal static GLTexture GetIcon(string name, bool folder)
        //{

        //}

        [DllImport("iiso3.dll")]
        internal extern static void SetClipboard(string text);

        [DllImport("iiso3.dll")]
        extern static bool LoadTextureFromFile(string file_name, uint* out_texture, int* out_width, int* out_height);

        internal static GLTexture LoadTextureFromFile(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                fileName = fileName.Replace("\\", "/");
                GLTexture glTexture;
                if (!s_glTextures.TryGetValue(fileName, out glTexture))
                {
                    string texturePath = fileName;
                    if (fileName.StartsWith("http"))
                    {
                        if (!s_networkGLTextures.TryGetValue(fileName, out texturePath))
                        {
                            string urlMD5 = Application.GetStringMd5(fileName);
                            texturePath = Path.Combine(TempDataPath, $"{urlMD5}{Path.GetExtension(fileName)}");
                            if (!File.Exists(texturePath))
                            {
                                TaskQueue.DownloadNetworkTexture(fileName, texturePath);
                            }
                          
                            s_networkGLTextures.Add(fileName, texturePath);
                        }
                        
                    }

                    if (File.Exists(texturePath))
                    {
                        glTexture = new GLTexture();
                        uint outTexture;
                        int width;
                        int height;
                        if (LoadTextureFromFile(texturePath, &outTexture, &width, &height))
                        {
                            glTexture.Image = new IntPtr(outTexture);
                            glTexture.Size = new Vector2(width, height);
                        }
                        s_glTextures.Add(fileName, glTexture);
                    }
                }
                return glTexture;
            }

            return default(GLTexture);
        }
        [DllImport("iiso3.dll")]
        extern static bool LoadTextureFromMemory(byte* buffer,int size, uint* out_texture, int* out_width, int* out_height);
        internal static GLTexture LoadTextureFromMemory(byte[] buffer)
        {
            if (buffer!=null && buffer.Length>0)
            {
                GLTexture glTexture = new GLTexture();
                uint outTexture;
                int width;
                int height;
                fixed (byte* bufferPtr = buffer)
                {
                    if (LoadTextureFromMemory(bufferPtr, buffer.Length, & outTexture, &width, &height))
                    {
                        glTexture.Image = new IntPtr(outTexture);
                        glTexture.Size = new Vector2(width, height);
                    }
                }
                return glTexture;
            }

            return default(GLTexture);
        }
        [DllImport("iiso3.dll")]
        extern static void DeleteTexture(uint* out_texture);
        internal static void DeleteTexture(GLTexture glTexture)
        {
            if (glTexture.Image != IntPtr.Zero)
            {
                HashSet<string> textures = new HashSet<string>();
                foreach (var item in s_glTextures)
                {
                    if (item.Value.Image == glTexture.Image)
                    {
                        textures.Add(item.Key);
                    }
                }

                foreach (var item in textures)
                {
                    s_glTextures.Remove(item);
                }

                uint outTexture = (uint)glTexture.Image;
                DeleteTexture(&outTexture);
            }
        }
    }

    public struct GLTexture
    {
        public IntPtr Image;
        public Vector2 Size;
    }

    public struct Version
    {
        public int Major;
        public int Minor;
        public int Patch;
        public string PreVersion;

        public override string ToString()
        {
            return $"{Major}.{Minor}.{Patch}";
        }

        public string ToFullString()
        {
            return $"{Major}.{Minor}.{Patch}-{PreVersion}";
        }

    }
}

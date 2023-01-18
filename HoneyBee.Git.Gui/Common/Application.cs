using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.Common
{
    public unsafe static class Application
    {
        public static string version => "1.0.alpha4";

        private static Dictionary<string, GLTexture> s_glTextures = new Dictionary<string, GLTexture>();
        public static Version GetVersion()
        {
            return new Version() { Major = 1, Minor = 0, Patch = 4, PreVersion = "alpha" };
        }

        private static string m_dataPath;
        private static string m_userPath;

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

        internal static GLTexture GetIcon(string name, bool folder)
        {
            
        }

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
                    if (File.Exists(fileName))
                    {
                        glTexture = new GLTexture();
                        uint outTexture;
                        int width;
                        int height;
                        if (LoadTextureFromFile(fileName, &outTexture, &width, &height))
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
        extern static void DeleteTexture(IntPtr out_texture);
        internal static void DeleteTexture(GLTexture glTexture)
        {
            if (glTexture.Image != IntPtr.Zero)
            {
                DeleteTexture(glTexture.Image);
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
    }
}

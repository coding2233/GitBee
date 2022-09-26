using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.Common
{
    public static class Application
    {
        private static string m_dataPath;
        public static string DataPath
        {
            get
            {
                if (!string.IsNullOrEmpty(m_dataPath))
                {
                    m_dataPath = Path.GetDirectoryName(System.Environment.GetCommandLineArgs()[0]);
                    Log.Info("DataPath: {0}", m_dataPath);
                }
                if (!Directory.Exists(m_dataPath))
                {
                    Directory.CreateDirectory(m_dataPath);
                }
                return m_dataPath;
            }
        }

        private static string m_userPath;

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
            System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] toData = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(str));
            string fileMD5 = BitConverter.ToString(toData).Replace("-", "").ToLower();
            return fileMD5;
        }

    }
}

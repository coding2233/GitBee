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
                if (!string.IsNullOrEmpty(m_userPath))
                {
                    m_userPath = Environment.GetEnvironmentVariable("USERPROFILE");
                    if (string.IsNullOrEmpty(m_userPath))
                    {
                        m_userPath = "./";
                    }
                    m_userPath = Path.Combine(m_userPath, AppDomain.CurrentDomain.FriendlyName);
                }
                if (!Directory.Exists(m_userPath))
                {
                    Directory.CreateDirectory(m_userPath);
                }
                return m_userPath;
            }
        }

    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.Common
{
    public class LuaPlugin
    {
        private static LuaEnv s_luaEnv;

        internal static void Enable()
        {
            Reload();
        }

        internal static void Disable()
        {
            if (s_luaEnv != null)
            {
                s_luaEnv.Dispose();
                s_luaEnv = null;
            }
        }

        internal static void Reload()
        {
            if (s_luaEnv != null)
            {
                s_luaEnv.Dispose();
                s_luaEnv = null;
            }
            s_luaEnv = new LuaEnv();
            RegisterMethod();
            //package.cpath = "../ybslib/bin/?.so;"..package.cpathpackage.cpath = "../ybslib/bin/?.so;"..package.cpath
            s_luaEnv.DoString("package.path=\"custom/lua/?.lua;lua/?.lua;lua/common/?.lua;lua/core/?.lua;lua/style/?.lua;\"..package.path");
            string customLua = "custom/lua/init.lua";
            if (File.Exists(customLua))
            {
                s_luaEnv.DoFile(customLua);
            }
            else
            {
                s_luaEnv.DoFile("lua/init.lua");
            }
        }

        public static string GetString(string tableName, string field)
        {
            s_luaEnv.GetGlobal(tableName);
            s_luaEnv.GetField(2, field);
            string value = s_luaEnv.ToString(-1);
            return value;
        }

        public static int GetInt(string tableName, string filed,int defaultValue= 0)
        {
            return defaultValue;
        }


        private static void RegisterMethod()
        {
            //m_luaEnv.Register("RunViewCommand", RunViewCommand);
        }
    }
}

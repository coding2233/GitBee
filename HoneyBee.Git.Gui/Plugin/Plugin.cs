using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer
{
    public class Plugin
    {
        private static LuaEnv s_luaEnv =new LuaEnv();


        public static void Load(string pluginName)
        {
            if (s_luaEnv != null)
            {
                s_luaEnv.LoadLua(pluginName);
            }
        }

        public static void Call(string funcName)
        {
            if (s_luaEnv != null)
            {
                s_luaEnv.CallLua(funcName);
            }
        }

        public static void Close()
        {
            if (s_luaEnv != null)
            {
                s_luaEnv.Dispose();
                s_luaEnv = null;
            }
        }
    }

   
}

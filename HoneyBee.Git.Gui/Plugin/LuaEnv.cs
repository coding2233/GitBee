using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer
{
    internal class LuaEnv : IDisposable
    {
        IntPtr m_luaState;
        internal LuaEnv()
        {
            m_luaState = luaL_newstate();
            luaL_openlibs(m_luaState);
        }

        internal void LoadLua(string scriptName)
        {
            try
            {
                if (string.IsNullOrEmpty(scriptName))
                {
                    Log.Warn("Script name is null");
                    return;
                }

                if (!File.Exists(scriptName))
                {
                    Log.Warn("File.Exists is false; {0}", scriptName);
                    return;
                }

                if (m_luaState != IntPtr.Zero)
                {
                    luaL_loadfile(m_luaState, scriptName);
                    lua_call(m_luaState, 0, 0);
                }
                else
                {
                    Log.Warn("LuaPlugin CallLua m_luaState is null!");
                }
            }
            catch (Exception e)
            {
                Log.Warn("LuaPlugin LoadLua exception:{0}", e);
            }
        }

        internal void CallLua(string funcName)
        {
            //try
            //{
            //    if (string.IsNullOrEmpty(funcName))
            //    {
            //        Log.Warn("func name is null");
            //        return;
            //    }


            //    if (m_luaState != IntPtr.Zero)
            //    {
            //        CallLuaFunction(m_luaState, funcName);
            //    }
            //    else
            //    {
            //        Log.Warn("LuaPlugin CallLua m_luaState is null!");
            //    }
            //}
            //catch (Exception e)
            //{
            //    Log.Warn("LuaPlugin CallLua exception:{0}", e);
            //}
        }

        public void Dispose()
        {
            if (m_luaState != IntPtr.Zero)
            {
                lua_close(m_luaState);
                m_luaState = IntPtr.Zero;
            }
        }

        [DllImport("iiso3.dll")]
        extern static IntPtr luaL_newstate();

        [DllImport("iiso3.dll")]
        extern static void luaL_openlibs(IntPtr lua_state);
        [DllImport("iiso3.dll")]
        extern static void lua_close(IntPtr lua_state);
        [DllImport("iiso3.dll")]
        extern static int luaL_loadfile(IntPtr lua_state, string file_name);
        [DllImport("iiso3.dll")]
        extern static void lua_call(IntPtr lua_state, int nargs, int nresult);
    }
}

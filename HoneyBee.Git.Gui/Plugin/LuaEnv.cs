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
        private const int LUA_REGISTRYINDEX = -10000;
        private const int LUA_ENVIRONINDEX = -10001;
        private const int LUA_GLOBALSINDEX = -10002;

        IntPtr m_luaState;

        internal delegate int LuaFucntion(IntPtr lua_state);
        internal LuaEnv()
        {
            m_luaState = luaL_newstate();
            luaL_openlibs(m_luaState);
        }

        internal void Loadfile(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    Log.Warn("Script name is null");
                    return;
                }

                if (!File.Exists(fileName))
                {
                    Log.Warn("File.Exists is false; {0}", fileName);
                    return;
                }

                if (m_luaState != IntPtr.Zero)
                {
                    luaL_loadfile(m_luaState, fileName);
                    //lua_call(m_luaState, 0, 0);
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

        internal void Register(string name,LuaFucntion luaFucntion)
        {
            lua_pushcclosure(m_luaState, luaFucntion,0);
            SetGlobal(name);
        }

        internal void SetGlobal(string name)
        {
            lua_setfield(m_luaState, LUA_GLOBALSINDEX, name);

        }
        internal void GetGlobal(IntPtr lua_state, string name)
        {
            lua_getfield(m_luaState, LUA_GLOBALSINDEX, name);
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

        [DllImport("iiso3.dll")]
        extern static void lua_setfield(IntPtr lua_state, int idx, string k);
        [DllImport("iiso3.dll")]
        extern static void lua_getfield(IntPtr lua_state, int idx, string k);
        [DllImport("iiso3.dll")]
        extern static void lua_pushcclosure(IntPtr lua_state, LuaFucntion luaFucntion, int n);
    }
}

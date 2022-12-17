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
        private static LuaPlugin s_luaPlugin =new LuaPlugin();


        public static void Load(string pluginName)
        {
            if (s_luaPlugin != null)
            {
                s_luaPlugin.LoadLua(pluginName);
            }
        }

        public static void Call(string funcName)
        {
            if (s_luaPlugin != null)
            {
                s_luaPlugin.CallLua(funcName);
            }
        }

        public static void Close()
        {
            if (s_luaPlugin != null)
            {
                s_luaPlugin.Dispose();
                s_luaPlugin = null;
            }
        }
    }

    internal class LuaPlugin:IDisposable
    {
        IntPtr m_luaState;
        internal LuaPlugin()
        {
            m_luaState = CreateLuaState();
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
                    Log.Warn("File.Exists is false; {0}",scriptName);
                    return;
                }

                if (m_luaState != IntPtr.Zero)
                {
                    CallLuaScript(m_luaState, scriptName);
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
            try
            {
                if (string.IsNullOrEmpty(funcName))
                {
                    Log.Warn("func name is null");
                    return;
                }


                if (m_luaState != IntPtr.Zero)
                {
                    CallLuaFunction(m_luaState, funcName);
                }
                else
                {
                    Log.Warn("LuaPlugin CallLua m_luaState is null!");
                }
            }
            catch (Exception e)
            {
                Log.Warn("LuaPlugin CallLua exception:{0}", e);
            }
        }

        public void Dispose()
        {
            if (m_luaState != IntPtr.Zero)
            {
                CloseLuaState(m_luaState);
                m_luaState = IntPtr.Zero;
            }
        }

        [DllImport("iiso3.dll")]
        extern static IntPtr CreateLuaState();
        [DllImport("iiso3.dll")]
        extern static void CallLuaScript(IntPtr lua_state, string script_name);
        [DllImport("iiso3.dll")]
        extern static void CloseLuaState(IntPtr lua_state);

        [DllImport("iiso3.dll")]
        extern static void CallLuaFunction(IntPtr lua_state, string function_name);

    }
}

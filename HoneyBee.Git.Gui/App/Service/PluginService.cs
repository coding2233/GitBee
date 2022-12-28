using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.GitRepository.View;

namespace Wanderer.App.Service
{
    public interface IPluginService
    {
        void Reload();

        void CallPopupContextItem(string method);
    }


    public unsafe class PluginService: IPluginService
    {
        LuaEnv m_luaEnv;
        public PluginService()
        {
        }

        public void Reload()
        {
            if (m_luaEnv != null)
            {
                m_luaEnv.Dispose();
                m_luaEnv = null;
            }
            m_luaEnv = new LuaEnv();
            RegisterMethod();
            //package.cpath = "../ybslib/bin/?.so;"..package.cpathpackage.cpath = "../ybslib/bin/?.so;"..package.cpath
            m_luaEnv.DoString("package.path=\"lua/?.lua;plugin/?.lua;\"..package.path");
            m_luaEnv.DoFile("lua/init.lua");
            LoadViewCommand();
        }

        public void CallPopupContextItem(string method)
        {
            m_luaEnv.Call(method);
        }

        private void LoadViewCommand()
        {
            
        }

        private void RegisterMethod()
        {
            m_luaEnv.Register("AddViewCommand",AddViewCommand);
        }

        private static int AddViewCommand(IntPtr lua_state)
        {
            var str = LuaEnv.lua_tolstring(lua_state,1);
            return 0;
        }

    }
}

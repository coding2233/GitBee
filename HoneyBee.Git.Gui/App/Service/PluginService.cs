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
    }


    public class PluginService: IPluginService
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
            //m_luaEnv.Register();
            //package.cpath = "../ybslib/bin/?.so;"..package.cpathpackage.cpath = "../ybslib/bin/?.so;"..package.cpath
            m_luaEnv.DoString("package.path=\"lua/?.lua;plugin/?.lua;\"..package.path");
            m_luaEnv.DoFile("lua/init.lua");
            LoadViewCommand();
        }

        private void LoadViewCommand()
        {
            
        }

    }
}

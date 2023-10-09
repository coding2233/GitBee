//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Wanderer.Common;
//using Wanderer.GitRepository.View;

//namespace Wanderer.App.Service
//{
//    public interface IPluginService
//    {
//        void Reload();

//        void CallPopupContextItem(string method);
//    }


//    public unsafe class PluginService: IPluginService
//    {
//        public PluginService()
//        {
//        }

//        public void Reload()
//        {
//            LuaPlugin.Reload();
           
//            //LoadViewCommand();
//        }

//        public void CallPopupContextItem(string method)
//        {
//            //m_luaEnv?.Call(method);
//        }

//        private void LoadViewCommand()
//        {
            
//        }

//        private void RegisterMethod()
//        {
//            //m_luaEnv.Register("RunViewCommand", RunViewCommand);
//        }

//        private static int RunViewCommand(IntPtr lua_state)
//        {
//            var str = LuaEnv.lua_tolstring(lua_state,1);
//            //GitCommandView.RunGitCommandView<LuaProcessGitCommand>();
//            return 0;
//        }

//    }
//}

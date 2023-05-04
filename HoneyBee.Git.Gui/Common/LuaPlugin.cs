using ImGuiNET;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer.Common
{
    public class LuaPlugin
    {
        private static LuaEnv s_luaEnv;
        private static Dictionary<string, string> s_language;
        private static Dictionary<string, GLTexture> s_folderIcons;
        private static Dictionary<string, GLTexture> s_fileIcons;
        private static Dictionary<string, GLTexture> s_normalIcons;
        private static Dictionary<string, Vector4> s_colors;

        internal static void Enable()
        {
            UpdateVersion();
            Reload();
        }

        internal static void UpdateVersion()
        {
            string luaVersionPath = "lua/version.lua";
            string versionText = Application.GetVersion().ToString();
            luaVersionPath = Path.Combine(Application.DataPath, luaVersionPath);
            File.WriteAllText(luaVersionPath,$"version = \"{versionText}\"");
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
            s_language = new Dictionary<string, string>();
            s_folderIcons= new Dictionary<string, GLTexture>();
            s_fileIcons= new Dictionary<string, GLTexture>();
            s_normalIcons = new Dictionary<string, GLTexture>();
            s_colors = new Dictionary<string, Vector4>();
            //
            RegisterMethod();
            //package.cpath = "../ybslib/bin/?.so;"..package.cpathpackage.cpath = "../ybslib/bin/?.so;"..package.cpath
            //s_luaEnv.DoString("package.cpath=\"lua/debug/?.dll;\"..package.cpath");
            string execPath = Application.DataPath.Replace("\\","/");
            //string luaPath = $"\"{execPath}/custom/lua/?.lua;{execPath}/lua/?.lua;{execPath}/lua/?/?.lua;\"";
            s_luaEnv.DoString($"package.cpath=package.cpath..\"{execPath}/lua/clibs/?/?.lua;\"");
            s_luaEnv.DoString($"package.path=package.path..\"{execPath}/lua/?/?.lua;\"");
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
            s_luaEnv.PushString(field);
            s_luaEnv.GetTable(-2);
            string value = s_luaEnv.ToString(-1);
            return value;
        }

        public static double GetNumber(string tableName, string field)
        {
            s_luaEnv.GetGlobal(tableName);
            s_luaEnv.PushString(field);
            s_luaEnv.GetTable(-2);
            double value = s_luaEnv.ToNumber(-1);
            return value;
        }

        public static string GetText(string key)
        {
            string value;
            if (!s_language.TryGetValue(key, out value))
            {
                try
                {
                    s_luaEnv.GetGlobal("Style");
                    s_luaEnv.PushString("Language");
                    s_luaEnv.GetTable(-2);
                    s_luaEnv.PushString(key);
                    s_luaEnv.GetTable(-2);
                    value = s_luaEnv.ToString(-1);
                    if (string.IsNullOrEmpty(value))
                    {
                        value = key;
                    }
                    else
                    {
                        s_language.Add(key, value);
                    }
                }
                catch (Exception e)
                {
                    Log.Info("Language not find key: {0}  {1}",key,e);
                    value = key;
                    s_language.Add(key, value);
                }
            }
            return value;
        }

        public static GLTexture GetFolderIcon(string folderName)
        {
            try
            {
                GLTexture folderIcon;
                if (!s_folderIcons.TryGetValue(folderName, out folderIcon))
                {
                    s_luaEnv.Call("GetFolderIcon", 1, folderName);
                    string iconPath = s_luaEnv.ToString(-1);
                    folderIcon = Application.LoadTextureFromFile(iconPath);
                    s_folderIcons.Add(folderName, folderIcon);
                }
                return folderIcon;
            }
            catch (Exception e)
            {
                Log.Info("GetFolderIcon error. {0} {1}", folderName,e);
            }
            return default(GLTexture);
        }

        public static GLTexture GetFileIcon(string fileName)
        {
            try
            {
                string fileExtension = Path.GetExtension(fileName);
                if (fileExtension == null)
                {
                    fileExtension = "";
                }

                GLTexture fileIcon;
                if (!s_fileIcons.TryGetValue(fileExtension, out fileIcon))
                {
                    s_luaEnv.Call("GetFileIcon", 1, fileExtension);
                    string iconPath = s_luaEnv.ToString(-1);
                    fileIcon = Application.LoadTextureFromFile(iconPath);
                    s_fileIcons.Add(fileExtension, fileIcon);
                }
                return fileIcon;
            }
            catch (Exception e)
            {
                Log.Info("GetFileIcon error. {0} {1}", fileName, e);
            }

            return default(GLTexture);
        }

        public static GLTexture GetIcon(string name)
        {
            try
            {
               
                GLTexture icon;
                if (!s_normalIcons.TryGetValue(name, out icon))
                {
                    s_luaEnv.Call("GetIcon", 1, name);
                    string iconPath = s_luaEnv.ToString(-1);
                    icon = Application.LoadTextureFromFile(iconPath);
                    s_normalIcons.Add(name, icon);
                }
                return icon;
            }
            catch (Exception e)
            {
                Log.Info("GetIcon error. {0} {1}", name, e);
            }

            return default(GLTexture);
        }

        public unsafe static Vector4 GetColor(string key, bool fromBg = true)
        {
            try
            {
                Vector4 value;
                if (!s_colors.TryGetValue(key, out value))
                {
                    s_luaEnv.GetGlobal("Style");
                    s_luaEnv.PushString("Color");
                    s_luaEnv.GetTable(-2);
                    s_luaEnv.PushString(key);
                    s_luaEnv.GetTable(-2);

                    uint colorU32 = (uint)s_luaEnv.ToNumber(-1);
                    value = ImGui.ColorConvertU32ToFloat4(colorU32);
                    //value = new Vector4();
                    //var r = (float)s_luaEnv.ToNumber(-1);
                    //var g = (float)s_luaEnv.ToNumber(-2);
                    //var b = (float)s_luaEnv.ToNumber(-3);
                    //var a = (float)s_luaEnv.ToNumber(-4);

                    s_colors.Add(key, value);
                }
                //Vector4 formColor = fromBg ? (*ImGui.GetStyleColorVec4(ImGuiCol.WindowBg)) : (*ImGui.GetStyleColorVec4(ImGuiCol.Text));
                Vector4 formColor = (*ImGui.GetStyleColorVec4(ImGuiCol.WindowBg));
                value = value + formColor * 0.5f;

                //bool isHalf = (formColor.X + formColor.Y + formColor.Z) * 0.3333f > 0.5f;
                //if (isHalf)
                //{
                //    value = value * formColor;
                //}
                //else
                //{
                //}
                //value =( value + formColor) * 0.5f;

                return value;
            }
            catch (Exception e)
            {
                Log.Info("Language not find key: {0}  {1}", key, e);

            }
            return Vector4.One;
        }

        public static uint GetColorU32(string key,bool fromBg=true)
        {
            return ImGui.GetColorU32(GetColor(key, fromBg));
        }

        private static void RegisterMethod()
        {
            //m_luaEnv.Register("RunViewCommand", RunViewCommand);
        }
    }
}

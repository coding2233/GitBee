using ImGuiNET;
using SFB;
using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;

namespace Wanderer.App.View
{
    public class MaterialIconsView: ImGuiTabView
    {
        public override string Name => LuaPlugin.GetText("Material Icons");

        public override string UniqueKey => Name;

        private Dictionary<string, int> m_iconMaps;

        private float m_fontScale = 1.5f;
        private string m_searchText = "";

        public MaterialIconsView()
        {
            m_iconMaps=new Dictionary<string, int>();
            var iconType = typeof(Icon);
            var fields = iconType.GetFields();
            if (fields != null)
            {
                foreach (var itemField in fields)
                {
                    string key = itemField.Name;
                    int value = (int)itemField.GetValue(null);
                    m_iconMaps.Add(key,value);
                }
            }
        }

          //0xe000,
          //                  0xffff,
        public override void OnDraw()
        {
            if (m_iconMaps == null)
            {
                return;
            }

            ImGui.SliderFloat("Font Scale", ref m_fontScale, 1.0f, 2.5f);
            if (ImGui.InputText("Search", ref m_searchText, 1024))
            {
                
            }

            ImGui.BeginChild("MaterialIconsView-Table-Child");

            float oldFontScale = ImGui.GetFont().Scale;
            ImGui.GetFont().Scale *= m_fontScale;
            ImGui.PushFont(ImGui.GetFont());

            ImGui.Columns(4);
            foreach (var item in m_iconMaps)
            {
                if (item.Key.Contains(m_searchText))
                {
                    if (ImGui.Button(char.ConvertFromUtf32(item.Value) + item.Key))
                    {
                        string code = $"Icon.Get(Icon.{item.Key})";
                        Application.SetClipboard(code);
                    }
                    ImGui.NextColumn();
                }
            }

            ImGui.GetFont().Scale = oldFontScale;
            ImGui.PopFont();

            ImGui.EndChild();
        }
    }
}

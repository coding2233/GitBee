using ImGuiNET;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using SFB;
using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Wanderer.App;
using Wanderer.App.Service;
using Wanderer.Common;


namespace Wanderer.TextCodeEditor
{
    public class TextEditorView : ImGuiTabView
    {
        public override string Name => m_fileName;

        public override string UniqueKey => m_filePath;

        private string m_filePath;
        private string m_fileName;
        private TextEditor m_textEditor;

        [Inject]
        public IPluginService plugin { get; set; }

       
        public TextEditorView(IContext context, string filePath) : base(context)
        {
            m_textEditor = new TextEditor();
            if (string.IsNullOrEmpty(filePath))
            {
                m_fileName = "*";
                m_filePath = Guid.NewGuid().ToString();
            }
            else
            {
                m_filePath = filePath;
                m_fileName = Path.GetFileName(m_filePath);
                if (File.Exists(m_filePath))
                {
                    m_textEditor.text = File.ReadAllText(m_filePath);
                }
            }
        }

       
        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

        public override void OnEnable()
        {
            base.OnEnable();
           
        }


        public override void OnDisable()
        {
            //m_gitRepo?.Dispose();
            //m_gitRepo = null;
            base.OnDisable();
        }

        public override void OnDraw()
        {
            m_textEditor.Render(m_fileName,ImGui.GetWindowSize());
        }

        protected void OnToolbarDraw()
        {
            
            ImGui.Separator();
        }

        protected bool DrawToolItem(string icon, string tip, bool active)
        {
            bool buttonClick = ImGui.Button(icon);
            var p1 = ImGui.GetItemRectMin();
            var p2 = ImGui.GetItemRectMax();
            p1.Y = p2.Y;
            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.PushTextWrapPos(ImGui.GetFontSize() * 35.0f);
                ImGui.TextUnformatted(LuaPlugin.GetText(tip));
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            if (active)
                ImGui.GetWindowDrawList().AddLine(p1, p2, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            return buttonClick;
        }

      

       
    }
}

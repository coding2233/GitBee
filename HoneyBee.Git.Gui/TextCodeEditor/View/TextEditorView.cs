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
using Wanderer.TextCodeEditor.View;

namespace Wanderer.TextCodeEditor
{
    public class TextEditorView : ImGuiTabView
    {
        public override string Name => m_fileName;

        public override string UniqueKey => m_filePath;

        public override bool Unsave => m_textEditor != null ? m_textEditor.IsTextChanged : false;
            

        private string m_filePath;
        private string m_fileName;
        private TextEditor m_textEditor;
        private string m_folderPath;


        [Inject]
        public IPluginService plugin { get; set; }


        private SplitView m_splitView = new SplitView(SplitView.SplitType.Horizontal, 200);

        private TextEditorFolderView m_folderView;

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
                if (File.Exists(filePath))
                {
                    m_filePath = filePath;
                    m_fileName = Path.GetFileName(m_filePath);
                    m_folderPath = Path.GetDirectoryName(m_filePath);
                    if (File.Exists(m_filePath))
                    {
                        m_textEditor.text = File.ReadAllText(m_filePath);
                    }
                }
                else
                {
                    m_fileName = "*";
                    m_filePath = Guid.NewGuid().ToString();
                    m_folderPath = filePath;

                    m_folderView = new TextEditorFolderView(context, m_folderPath);
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
            if (m_folderView == null)
            {
                OnDrawTextEditor();
            }
            else
            {
                m_splitView.Begin();
                OnDrawFolder();
                m_splitView.Separate();
                OnDrawTextEditor();
            }
        }

        private void OnDrawFolder()
        {
            m_folderView.OnDraw();
        }

        private void OnDrawTextEditor()
        {
            m_textEditor.Render(m_fileName,ImGui.GetContentRegionAvail());

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

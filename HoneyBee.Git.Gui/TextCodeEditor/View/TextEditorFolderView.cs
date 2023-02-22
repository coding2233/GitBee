using ImGuiNET;
using strange.extensions.context.api;
using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository.Common;

namespace Wanderer.TextCodeEditor.View
{
    internal class TextEditorFolderView : EventView
    {
        string m_folderPath;

        FolderTreeViewNode m_folderTreeView;
        private ImGuiTreeNodeFlags m_nodeDefaultFlags;
        private FolderTreeViewNode m_nodeSelected;

        public TextEditorFolderView(IContext context,string folderPath) : base(context)
        {
            m_nodeDefaultFlags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.FramePadding;
            m_folderPath = folderPath;
            m_folderTreeView = BuildFolderNode(folderPath);
            m_folderTreeView.NodeOpened = true;
            //m_folderTreeView.ad
        }


        public void OnDraw()
        {
            if (m_folderTreeView != null)
            {
                DrawNode(m_folderTreeView);
            }
        }

        private void DrawNode(FolderTreeViewNode node)
        {
            var nodeFlag = m_nodeSelected==node ? m_nodeDefaultFlags | ImGuiTreeNodeFlags.Selected : m_nodeDefaultFlags;

            if (node.Children != null && node.Children.Count > 0)
            {
                var folderIconPos = ImGui.GetWindowPos() + ImGui.GetCursorPos() + new Vector2(ImGui.GetTextLineHeight(), -ImGui.GetScrollY() + Application.FontOffset);
                var folderIconPosMax = folderIconPos + new Vector2(ImGui.GetTextLineHeight() * 2, ImGui.GetTextLineHeight());
                node.NodeOpened = ImGui.TreeNodeEx($"\t\t{node.Name}", nodeFlag);
                //文件夹图标
                ImGui.GetWindowDrawList().AddImage(node.NodeOpened ? LuaPlugin.GetFolderIcon("default_folder_opened").Image : LuaPlugin.GetFolderIcon("default_folder").Image, folderIconPos, folderIconPosMax);

                if (node.NodeOpened)
                {
                    foreach (var item in node.Children)
                    {
                        DrawNode(item);
                    }
                    ImGui.TreePop();

                }
            }
            else
            {
                nodeFlag = nodeFlag | ImGuiTreeNodeFlags.Leaf;
                var fileIconPos = ImGui.GetWindowPos() + ImGui.GetCursorPos() + new Vector2(ImGui.GetTextLineHeight(), -ImGui.GetScrollY() + Application.FontOffset);
                var fileIconPosMax = fileIconPos + new Vector2(ImGui.GetTextLineHeight() * 2, ImGui.GetTextLineHeight());
                if (ImGui.TreeNodeEx($"\t\t{node.Name}", nodeFlag))
                {
                    ImGui.TreePop();
                }
                ImGui.GetWindowDrawList().AddImage(LuaPlugin.GetFileIcon(node.Name).Image, fileIconPos, fileIconPosMax);

                if (ImGui.IsItemClicked())
                {
                    m_nodeSelected = node;

                    dispatcher.Dispatch("OpenFile", node.FullName);
                }
            }

           
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

        private FolderTreeViewNode BuildFolderNode(string path)
        {
            FolderTreeViewNode node = new FolderTreeViewNode();
            node.FullName = path;
            node.Name = Path.GetFileName(path);
            if (Directory.Exists(path))
            {
                //node.Data = LuaPlugin.GetFolderIcon("default_folder");
                node.Children = new List<FolderTreeViewNode>();
                
                var dirs = Directory.GetDirectories(path);
                foreach (var item in dirs)
                {
                    node.Children.Add(BuildFolderNode(item));
                }
                var files = Directory.GetFiles(path);
                foreach (var item in files)
                {
                    node.Children.Add(BuildFolderNode(item));
                }
            }
            else
            {
                //node.Data = LuaPlugin.GetFileIcon(path);
            }
            return node;
        }
        
    }

    public class FolderTreeViewNode : TreeViewNode<FolderTreeViewNode,GLTexture>
    {
        
    }
}

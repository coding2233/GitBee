﻿using ImGuiNET;
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

        public TextEditorFolderView(string folderPath)
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
                var folderIconPos = ImGui.GetWindowPos() + ImGui.GetCursorPos() + new Vector2(ImGui.GetTextLineHeight()*1.5f, -ImGui.GetScrollY() + Application.FontOffset);
                var folderIconPosMax = folderIconPos + Application.IconSize;
                node.NodeOpened = ImGui.TreeNodeEx($"\t\t{node.Name}", nodeFlag);
				//文件夹图标
				//var folderGLTexture = Application.LoadTextureFromFile(node.NodeOpened ? "Resources/icons/default_folder_opened.png" : "Resources/icons/default_folder.png");
                var folderGLTexture = Application.GetFileIcon(node.FullName,false);
				ImGui.GetWindowDrawList().AddImage(folderGLTexture.Image, folderIconPos, folderIconPosMax);
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
                var fileIconPosMax = fileIconPos + Application.IconSize;
                if (ImGui.TreeNodeEx($"\t{node.Name}", nodeFlag))
                {
                    ImGui.TreePop();
                }
				GLTexture fileIcon = Application.GetFileIcon(node.FullName);
				//Application.LoadTextureFromFile($"Resources/icons/default_file.png")
				ImGui.GetWindowDrawList().AddImage(fileIcon.Image, fileIconPos, fileIconPosMax);

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

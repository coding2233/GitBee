using ImGuiNET;
using LibGit2Sharp;
using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository.Common;

namespace Wanderer
{
	public class FileTreeNode
	{
		public string FullName { get; private set; }
		public string Name { get; private set; }
		public bool NodeOpened { get; set; }
		public bool IsFile { get; private set; }
		public List<FileTreeNode> Children { get; private set; }
		public FileTreeNode(string path, bool isFile = true)
		{
			FullName = path.Replace("\\","/");
			Name = Path.GetFileName(path);
			NodeOpened = false;
			IsFile = isFile;
			Children = new List<FileTreeNode>();
		}
	}

    public class DrawWorkTreeView: DrawSubView
	{
        private SplitView m_horizontalSplitView = new SplitView(SplitView.SplitType.Horizontal);

        private GitRepo m_gitRepo;


        private ImGuiTreeNodeFlags m_nodeDefaultFlags;

		private List<FileTreeNode> m_fileTreeNodes;

		public override string Name => "Work Tree";
		public DrawWorkTreeView(GitRepo gitRepo)
        {
            m_nodeDefaultFlags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.FramePadding;
            m_gitRepo = gitRepo;
        }

        public override void OnEnable()
        {
            base.OnEnable();

			if (m_fileTreeNodes == null)
			{
				m_fileTreeNodes = GetFileTreeNodes(".");
			}
        }

		List<FileTreeNode> GetFileTreeNodes(string path)
		{
			List<FileTreeNode> fileTreeNodes = new List<FileTreeNode>();
			var dirs = Directory.GetDirectories(path);
			foreach ( var dir in dirs) 
			{
				string dirPath = dir.Replace("\\", "/").Replace("./", "");
				if (dirPath.Equals(".git"))
				{
					continue;
				}

				if (m_gitRepo.Repo.Ignore.IsPathIgnored(dirPath))
				{
					continue;
				}
				var dirFileTreeNode = new FileTreeNode(dirPath, false);
				dirFileTreeNode.Children.AddRange(GetFileTreeNodes(dirPath));
				fileTreeNodes.Add(dirFileTreeNode);
			}
			var files = Directory.GetFiles(path);
			foreach (var file in files)
			{
				string filePath = file.Replace("\\", "/").Replace("./", "");
				if (m_gitRepo.Repo.Ignore.IsPathIgnored(filePath))
				{
					continue;
				}
				fileTreeNodes.Add(new FileTreeNode(filePath));
			}
			return fileTreeNodes;
		}

		public override void OnDraw()
        {
            ImGui.BeginChild("WorkTreeView_Content", ImGui.GetWindowSize() - new Vector2(0, 0));
			m_horizontalSplitView.Begin();
			if (m_fileTreeNodes != null)
			{
				foreach (var item in m_fileTreeNodes)
				{
					DrawStatusEntryTreeNode(item);
				}
			}
			m_horizontalSplitView.Separate();
			m_horizontalSplitView.End();
			ImGui.EndChild();
        }

     
		private void DrawStatusEntryTreeNode(FileTreeNode node)
		{

			bool selected = false;

			if (node.Children != null && node.Children.Count > 0)
			{
				var nodeFlag = selected ? m_nodeDefaultFlags | ImGuiTreeNodeFlags.Selected : m_nodeDefaultFlags;

				var folderIconPos = ImGui.GetWindowPos() + ImGui.GetCursorPos() + new Vector2(ImGui.GetTextLineHeight() * 1.5f, -ImGui.GetScrollY() + Application.FontOffset);
				var folderIconPosMax = folderIconPos + Application.IconSize;

				node.NodeOpened = ImGui.TreeNodeEx($"\t\t{node.Name}", nodeFlag);
				if (ImGui.IsItemClicked())
				{
					
				}
				

				//文件夹图标
				//var folderGLTexture =  Application.LoadTextureFromFile(node.NodeOpened ? "Resources/icons/default_folder_opened.png" : "Resources/icons/default_folder.png");
				var folderGLTexture = Application.GetFileIcon(node.FullName, false);
				ImGui.GetWindowDrawList().AddImage(folderGLTexture.Image, folderIconPos, folderIconPosMax);

				if (node.NodeOpened)
				{
					foreach (var item in node.Children)
					{
						DrawStatusEntryTreeNode(item);
					}
					ImGui.TreePop();
				}
			}
			else
			{
			
				uint popTextColor = ImGui.GetColorU32(ImGuiCol.Text);
				ImGui.PushStyleColor(ImGuiCol.Text, popTextColor);

				var fileIconPos = ImGui.GetWindowPos() + ImGui.GetCursorPos() + new Vector2(ImGui.GetTextLineHeight(), -ImGui.GetScrollY() + Application.FontOffset);
				var fileIconPosMax = fileIconPos + Application.IconSize;

				var nodeFlag = selected ? m_nodeDefaultFlags | ImGuiTreeNodeFlags.Selected | ImGuiTreeNodeFlags.Leaf : m_nodeDefaultFlags | ImGuiTreeNodeFlags.Leaf;

				if (ImGui.TreeNodeEx($"\t{node.Name}", nodeFlag))
				{


					ImGui.TreePop();
				}


				


				//文件图标
				//node.Name
				GLTexture fileIcon = Application.GetFileIcon(node.FullName);
				//Application.LoadTextureFromFile($"Resources/icons/default_file.png")
				ImGui.GetWindowDrawList().AddImage(fileIcon.Image, fileIconPos, fileIconPosMax);
				//var statusIconPos = fileIconPosMax;
				//statusIconPos.Y = fileIconPos.Y;
				//var statusIconPosMax = statusIconPos + Vector2.One * ImGui.GetTextLineHeight();
				//ImGui.GetWindowDrawList().AddImage(LuaPlugin.GetIcon(statusIcon).Image, statusIconPos, statusIconPosMax);


				ImGui.PopStyleColor();

				if (ImGui.IsItemHovered())
				{
				}

				if (ImGui.IsItemClicked())
				{
					
				}

			}

			if (ImGui.BeginPopupContextItem(node.FullName))
			{
				//if (ImGui.MenuItem("Edit"))
				//{
				//}

				if (ImGui.MenuItem("Copy Path"))
				{
					ImGui.SetClipboardText(node.FullName);
					//Application.SetClipboard(branchNode.FullName);
				}
				if (ImGui.MenuItem("Open"))
				{
                    System.Diagnostics.Process.Start("Explorer", node.FullName.Replace("/", "\\"));
				}
				ImGui.EndPopup();
			}
		}
    }

   
}

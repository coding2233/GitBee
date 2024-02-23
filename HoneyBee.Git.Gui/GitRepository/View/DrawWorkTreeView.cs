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
using Wanderer.App;
using Wanderer.Common;
using Wanderer.GitRepository.Common;

namespace Wanderer
{
	public class FileTreeNode
	{
		public string FullName { get; private set; }
		public string Name { get; private set; }
		public bool NodeOpened { get; set; }
		public bool Delete { get; set; }
		public bool IsFile { get; private set; }
		public FileTreeNode Parent { get; private set; }
		public List<FileTreeNode> Children { get; private set; }
		public FileTreeNode(string path,FileTreeNode parent, bool isFile = true)
		{
			FullName = path;
			Name = Path.GetFileName(path);
			Parent = parent; 
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
		private Dictionary<string,FileTreeNode> m_fileNodeMap;

		public override string Name => "Work Tree";

		private FileSystemWatcher m_fileSystemWatcher;

		public DrawWorkTreeView(GitRepo gitRepo)
        {
            m_nodeDefaultFlags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.FramePadding;
            m_gitRepo = gitRepo;
			m_fileNodeMap = new Dictionary<string, FileTreeNode>();
			m_fileTreeNodes = new List<FileTreeNode>();
			FileNodeInit();
		}

		private async void FileNodeInit()
		{
			string rootPath = m_gitRepo.RootPath;
			await Task.Run(() => {
				m_fileTreeNodes = GetFileTreeNodes(rootPath,null);
			});

			m_fileSystemWatcher = new FileSystemWatcher(rootPath);
			m_fileSystemWatcher.Changed += OnFileWatcherChanged;
		}



		protected override void OnDestroy()
		{
            if (m_fileSystemWatcher!=null)
            {
				m_fileSystemWatcher.Changed -= OnFileWatcherChanged;
				m_fileSystemWatcher.Dispose();
				m_fileSystemWatcher = null;
			}
            base.OnDestroy();
		}

		public override void OnEnable()
        {
            base.OnEnable();

			
		}

		private void OnFileWatcherChanged(object sender, FileSystemEventArgs e)
		{
			switch (e.ChangeType)
			{
				case WatcherChangeTypes.Created:
				case WatcherChangeTypes.Renamed:
					string newPath = ToRepoPath(e.FullPath);
					bool isFile = File.Exists(e.FullPath);
					string parentPath = Path.GetDirectoryName(newPath);
					FileTreeNode parent = null;
					m_fileNodeMap.TryGetValue(parentPath, out parent);
					var newFileTreeNode = new FileTreeNode(newPath, parent, isFile);
					if (parent != null)
					{
						parent.Children.Add(newFileTreeNode);
					}
					m_fileNodeMap.Add(newPath, newFileTreeNode);
					break;
				case WatcherChangeTypes.Deleted:
					string oldPath = ToRepoPath(e.FullPath);
					if (m_fileNodeMap.TryGetValue(oldPath, out FileTreeNode fileTreeNode))
					{
						fileTreeNode.Delete = true;
						m_fileNodeMap.Remove(oldPath);
					}
					break;
				case WatcherChangeTypes.All:
				case WatcherChangeTypes.Changed:
					break;
				default:
					break;
			}
		}

		List<FileTreeNode> GetFileTreeNodes(string path, FileTreeNode parent)
		{
			List<FileTreeNode> fileTreeNodes = new List<FileTreeNode>();
			var dirs = Directory.GetDirectories(path);
			foreach ( var dir in dirs) 
			{
				string dirPath = ToRepoPath(dir);
				if (dirPath.Equals(".git"))
				{
					continue;
				}

				if (m_gitRepo != null && m_gitRepo.Repo != null 
					&& m_gitRepo.Repo.Ignore.IsPathIgnored(dirPath))
				{
					continue;
				}
				var dirFileTreeNode = new FileTreeNode(dirPath, parent, false);
				dirFileTreeNode.Children.AddRange(GetFileTreeNodes(dir, dirFileTreeNode));
				fileTreeNodes.Add(dirFileTreeNode);
				m_fileNodeMap.Add(dirPath, dirFileTreeNode);
			}
			try
			{
				var files = Directory.GetFiles(path);
				foreach (var file in files)
				{
					string filePath = ToRepoPath(file);
					if (m_gitRepo != null && m_gitRepo.Repo != null 
						&& m_gitRepo.Repo.Ignore.IsPathIgnored(filePath))
					{
						continue;
					}
					var fileNode = new FileTreeNode(filePath, parent);
					fileTreeNodes.Add(fileNode);
					m_fileNodeMap.Add(filePath, fileNode);
				}
			}
			catch (System.Exception e)
			{
				Log.Warn("GetFileTreeNodes exception: {0}",e.Message);
			}
			return fileTreeNodes;
		}


		private string ToRepoPath(string path)
		{
			path = path.Replace("\\", "/");
			int rootLength = m_gitRepo.RootPath.Length + 1;
			path = path.Substring(rootLength, path.Length - rootLength);
			return path;
		}

		public override void OnDraw()
        {
			ImGui.BeginChild("WorkTreeView_Content", ImGui.GetWindowSize() - new Vector2(0, 0));
			m_horizontalSplitView.Begin();
			if (m_fileTreeNodes != null && m_fileTreeNodes.Count > 0)
			{
				foreach (var item in m_fileTreeNodes)
				{
					if (item.Delete)
					{
						m_fileTreeNodes.Remove(item);
						break;
					}
					DrawStatusEntryTreeNode(item);
				}
			}
			else
			{
				AppContextView.Spinner();
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
						if (item.Delete)
						{
							node.Children.Remove(item);
							break;
						}
						DrawStatusEntryTreeNode(item);
					}
					ImGui.TreePop();
				}
			}
			else
			{
			
				uint popTextColor = ImGui.GetColorU32(ImGuiCol.Text);
				ImGui.PushStyleColor(ImGuiCol.Text, popTextColor);

				var fileIconPos = ImGui.GetWindowPos() + ImGui.GetCursorPos() + new Vector2(ImGui.GetTextLineHeight() * 1.5f, -ImGui.GetScrollY() + Application.FontOffset);
				var fileIconPosMax = fileIconPos + Application.IconSize;

				var nodeFlag = selected ? m_nodeDefaultFlags | ImGuiTreeNodeFlags.Selected | ImGuiTreeNodeFlags.Leaf : m_nodeDefaultFlags | ImGuiTreeNodeFlags.Leaf;

				if (ImGui.TreeNodeEx($"\t\t{node.Name}", nodeFlag))
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

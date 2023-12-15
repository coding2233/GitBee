using ImGuiNET;
using LibGit2Sharp;
using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository.Common;
using static System.Net.WebRequestMethods;

namespace Wanderer
{
    public class DrawWorkTreeView:View
    {
        private SplitView m_horizontalSplitView = new SplitView(SplitView.SplitType.Horizontal);
        private SplitView m_verticalSplitView = new SplitView(SplitView.SplitType.Vertical);

        private string m_submitMessage= "";

        private GitRepo m_gitRepo;

        private DiffShowView m_diffShowView;

        private ImGuiTreeNodeFlags m_nodeDefaultFlags;

        private List<StatusEntryTreeViewNode> m_stageTreeView=new List<StatusEntryTreeViewNode>();
        private List<StatusEntryTreeViewNode> m_unstageTreeView=new List<StatusEntryTreeViewNode>();

        private HashSet<StatusEntryTreeViewNode> m_stageSelectedNodes = new HashSet<StatusEntryTreeViewNode>();
        private HashSet<StatusEntryTreeViewNode> m_unstageSelectedNodes = new HashSet<StatusEntryTreeViewNode>();

        private List<StatusEntryTreeViewNode> m_stageMultipleSelectionNodes =new List<StatusEntryTreeViewNode>();
        private List<StatusEntryTreeViewNode> m_unstageMultipleSelectionNodes =new List<StatusEntryTreeViewNode>();

        public Action<string> OnEditorText;

        public DrawWorkTreeView(GitRepo gitRepo)
        {
            m_nodeDefaultFlags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.FramePadding;
            m_gitRepo = gitRepo;
            m_diffShowView = new DiffShowView();
            UpdateStatus();
        }

        public override void OnEnable()
        {
            base.OnEnable();

            UpdateStatus();
        }

        public void Draw()
        {
            //ImGui.ShowStyleEditor();
            ImGui.BeginChild("WorkTreeView_Content", ImGui.GetWindowSize() - new Vector2(0, 100));
            m_horizontalSplitView.Begin();
            DrawStageStatus();
            m_horizontalSplitView.Separate();
            m_diffShowView?.Draw();
            m_horizontalSplitView.End();
            ImGui.EndChild();

            ImGui.BeginChild("WorkTreeView_Commit");
            //绘制提交
            DrawSubmit();
            ImGui.EndChild();
        }

        /// <summary>
        /// 缓存以及状态
        /// </summary>
        private void DrawStageStatus()
        {
            m_verticalSplitView.Begin();
            DrawStageFilesStatus();
            m_verticalSplitView.Separate();
            DrawUnstageFileStatus();
            m_verticalSplitView.End();
        }

     
        private void DrawStageFilesStatus()
        {
            if (ImGui.Button("Unstage All"))
            {
                m_gitRepo.Unstage(UpdateStatus);
            }
            ImGui.SameLine();
            if (ImGui.Button("Unstage Selected"))
            {
                if (m_stageSelectedNodes.Count > 0)
                {
                    var selectPath = TreeNodesToPaths(m_stageSelectedNodes);
                    m_gitRepo.Unstage(UpdateStatus,selectPath);
                }
            }

            if (m_stageTreeView.Count > 0)
            {
                ImGui.BeginChild("DrawStageFilesStatus-TreeView");
                foreach (var item in m_stageTreeView)
                {
                    DrawStatusEntryTreeNode(item, true);
                }
                ImGui.EndChild();
            }
        }

        private void DrawUnstageFileStatus()
        {
            if (ImGui.Button("Stage All"))
            {
                m_gitRepo.Stage(UpdateStatus);
             
            }
            ImGui.SameLine();
            if (ImGui.Button("Stage Selected"))
            {
                if (m_unstageSelectedNodes.Count > 0)
                {
                    var selectPath = TreeNodesToPaths(m_unstageSelectedNodes);
                    m_gitRepo.Stage(UpdateStatus, selectPath);
                    UpdateStatus();
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Discard Selected"))
            {
                if (m_unstageSelectedNodes.Count > 0)
                {
                    var selectPath = TreeNodesToPaths(m_unstageSelectedNodes);
                    m_gitRepo.Restore(selectPath);
                    UpdateStatus();
                }
            }

            if (m_unstageTreeView.Count > 0)
            {
                ImGui.BeginChild("DrawUnstageFileStatus-TreeView");
                foreach (var item in m_unstageTreeView)
                {
                    DrawStatusEntryTreeNode(item, false);
                }
                ImGui.EndChild();
            }
        }

        /// <summary>
        /// 提交模块
        /// </summary>
        private void DrawSubmit()
        {
            ImGui.InputTextMultiline("", ref m_submitMessage, 500, new Vector2(ImGui.GetWindowWidth(), 70));
            ImGui.Text($"{m_gitRepo.SignatureAuthor.Name}<{m_gitRepo.SignatureAuthor.Email}>");
            ImGui.SameLine();
            if (ImGui.Button("Commit"))
            {
                if (!string.IsNullOrEmpty(m_submitMessage))
                {
                    try
                    {
                        m_gitRepo.Commit(m_submitMessage);
                    }
                    catch (Exception e)
                    {
                        Log.Warn("DrawSubmit exception: {0}",e);
                    }
                    UpdateStatus();
                }
                m_submitMessage = "";
            }
        }


        private void DrawStatusEntryTreeNode(StatusEntryTreeViewNode node, bool isStage)
        {
            var selectNodes = isStage ? m_stageSelectedNodes : m_unstageSelectedNodes;
            var multipSelectNodes = isStage ? m_stageMultipleSelectionNodes : m_unstageMultipleSelectionNodes;
            bool selected = selectNodes.Contains(node);

            if (node.Children != null && node.Children.Count > 0)
            {
                var nodeFlag = selected ? m_nodeDefaultFlags | ImGuiTreeNodeFlags.Selected : m_nodeDefaultFlags;

                var folderIconPos =ImGui.GetWindowPos()+ ImGui.GetCursorPos()+new Vector2(ImGui.GetTextLineHeight(),-ImGui.GetScrollY() + Application.FontOffset);
                var folderIconPosMax = folderIconPos + new Vector2(ImGui.GetTextLineHeight()*2, ImGui.GetTextLineHeight());

                node.NodeOpened = ImGui.TreeNodeEx($"\t\t{node.Name}", nodeFlag);
                if (ImGui.IsItemClicked())
                {
                    if (!ImGui.IsItemToggledOpen())
                    {
                        if (ImGui.GetIO().KeyCtrl)
                        {
                            if (selected)
                            {
                                selectNodes.Remove(node);
                            }
                            else
                            {
                                selectNodes.Add(node);
                            }
                        }
                        else if (ImGui.GetIO().KeyShift)
                        {
                            selectNodes.Add(node);
                            List<int> indexs = new List<int>();
                            if (selectNodes.Count > 1)
                            {
                                //这里还需要更富在的多选计算
                                foreach (var itemNode in selectNodes)
                                {
                                    indexs.Add(multipSelectNodes.IndexOf(itemNode));
                                }
                                indexs.Sort();
                                int minIndex = indexs[0];
                                int maxIndex = indexs[indexs.Count - 1];
                                selectNodes.Clear();
                                for (int i = 0; i < multipSelectNodes.Count; i++)
                                {
                                    if (i >= minIndex && i <= maxIndex)
                                    {
                                        selectNodes.Add(multipSelectNodes[i]);
                                    }
                                }
                            }
                        }
                        else
                        {
                            selectNodes.Clear();
                            selectNodes.Add(node);
                        }
                    }
                }

                //文件夹图标
                //var folderGLTexture =  Application.LoadTextureFromFile(node.NodeOpened ? "Resources/icons/default_folder_opened.png" : "Resources/icons/default_folder.png");
				var folderGLTexture = Application.GetFileIcon(node.FullName,false);
				ImGui.GetWindowDrawList().AddImage(folderGLTexture.Image, folderIconPos, folderIconPosMax);

                if (node.NodeOpened)
                {
                    foreach (var item in node.Children)
                    {
                        DrawStatusEntryTreeNode(item, isStage);
                    }
                    ImGui.TreePop();
                }
            }
            else
            {
                var statusEntry = node.Data;
                string stateStr = statusEntry.State.ToString();
                string statusIcon = "unknown";
                uint popTextColor = ImGui.GetColorU32(ImGuiCol.Text);
                switch (statusEntry.State)
                {
                    case FileStatus.Conflicted:
                        statusIcon = "warn";
                        popTextColor = 0xFF0FC4F1;
                        break;
                    case FileStatus.NewInIndex:
                    case FileStatus.NewInWorkdir:
                        statusIcon = "add";
                        popTextColor = 0xFF71CC2E;
                        break;
                    case FileStatus.DeletedFromIndex:
                    case FileStatus.DeletedFromWorkdir:
                        statusIcon = "delete";
                        popTextColor = 0xFF3C4CE7;
                        break;

                    case FileStatus.ModifiedInIndex:
                    case FileStatus.ModifiedInWorkdir:
                    case FileStatus.RenamedInIndex:
                    case FileStatus.RenamedInWorkdir:
                    case FileStatus.TypeChangeInIndex:
                    case FileStatus.TypeChangeInWorkdir:
                        statusIcon = "modified";
                        break;
                    
                    default:
                        if (CheckFileStatus(statusEntry.State, FileStatus.ModifiedInIndex)
                            || CheckFileStatus(statusEntry.State, FileStatus.RenamedInIndex)
                            || CheckFileStatus(statusEntry.State, FileStatus.TypeChangeInIndex))
                        {
                            statusIcon = "modified";
                        }
                        break;
                }

                ImGui.PushStyleColor(ImGuiCol.Text, popTextColor);

                var fileIconPos = ImGui.GetWindowPos() + ImGui.GetCursorPos() + new Vector2(ImGui.GetTextLineHeight(), -ImGui.GetScrollY() + Application.FontOffset);
                var fileIconPosMax = fileIconPos + Application.IconSize;

                var nodeFlag = selected ? m_nodeDefaultFlags | ImGuiTreeNodeFlags.Selected | ImGuiTreeNodeFlags.Leaf : m_nodeDefaultFlags| ImGuiTreeNodeFlags.Leaf;

                if (ImGui.TreeNodeEx($"\t\t{node.Name}", nodeFlag))
                {
                   

                    ImGui.TreePop();
                }


                if (ImGui.BeginPopupContextItem())
                {
                    if (ImGui.MenuItem("Edit"))
                    {
                        OnEditorText?.Invoke(Path.Combine(m_gitRepo.RootPath, node.FullName));
                    }
                    ImGui.EndPopup();
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
                    ImGui.SetTooltip(stateStr);
                }

                if (ImGui.IsItemClicked())
                {
                    var patch = m_gitRepo.Diff.Compare<Patch>(m_gitRepo.Repo.Head.Tip == null ? null : m_gitRepo.Repo.Head.Tip.Tree, DiffTargets.Index | DiffTargets.WorkingDirectory, new List<string>() { statusEntry.FilePath });
                    m_diffShowView.Build(patch.FirstOrDefault(), m_gitRepo);

                    if (ImGui.GetIO().KeyCtrl)
                    {
                        if (selected)
                        {
                            selectNodes.Remove(node);
                        }
                        else
                        {
                            selectNodes.Add(node);
                        }
                    }
                    else if (ImGui.GetIO().KeyShift)
                    {
                        selectNodes.Add(node);
                        List<int> indexs = new List<int>();
                        if (selectNodes.Count > 1)
                        {
                            //这里还需要更富在的多选计算
                            foreach (var itemNode in selectNodes)
                            {
                                indexs.Add(multipSelectNodes.IndexOf(itemNode));
                            }
                            indexs.Sort();
                            int minIndex = indexs[0];
                            int maxIndex = indexs[indexs.Count - 1];
                            selectNodes.Clear();
                            for (int i = 0; i < multipSelectNodes.Count; i++)
                            {
                                if (i >= minIndex && i <= maxIndex)
                                {
                                    selectNodes.Add(multipSelectNodes[i]);
                                }
                            }
                        }
                    }
                    else
                    {
                        selectNodes.Clear();
                        selectNodes.Add(node);
                    }
                }

            }

        }
        internal void UpdateStatus()
        {
            try
            {
                RepositoryStatus statuses = null;
                if (m_gitRepo != null && m_gitRepo.Repo != null)
                {
                    statuses = m_gitRepo.Repo.RetrieveStatus();
                }

                m_stageTreeView.Clear();
                m_unstageTreeView.Clear();
                m_stageSelectedNodes.Clear();
                m_unstageSelectedNodes.Clear();
                m_stageMultipleSelectionNodes.Clear();
                m_unstageMultipleSelectionNodes.Clear();

                if (statuses == null)
                {
                    return;
                }
        
                foreach (var item in statuses)
                {
                    if (item.State == FileStatus.Ignored)
                    {
                        continue;
                    }

                    //FileStatus.xxxxInIndex index ~= stage
                    if (CheckFileStatus(item.State, FileStatus.NewInIndex) || CheckFileStatus(item.State, FileStatus.ModifiedInIndex)
                        || CheckFileStatus(item.State, FileStatus.RenamedInIndex) || CheckFileStatus(item.State, FileStatus.TypeChangeInIndex)
                        || CheckFileStatus(item.State, FileStatus.DeletedFromIndex))
                    {
                        StatusEntryTreeViewNode.JoinTreeViewNode(m_stageTreeView, item.FilePath, item);
                    }
                    else
                    {
                        StatusEntryTreeViewNode.JoinTreeViewNode(m_unstageTreeView, item.FilePath, item);
                    }
                }

                foreach (var item in m_stageTreeView)
                {
                    BuildMultipleSelectionNodes(m_stageMultipleSelectionNodes, item);
                }

                foreach (var item in m_unstageTreeView)
                {
                    BuildMultipleSelectionNodes(m_unstageMultipleSelectionNodes, item);
                }

                
            }
            catch (Exception e)
            {
                Log.Warn("DrawWorkTreeView exception: {0}",e);
            }
        }

        private bool CheckFileStatus(FileStatus fileStatus, FileStatus checkStatus)
        {
            return (fileStatus & checkStatus) == checkStatus;
        }

        private void BuildMultipleSelectionNodes(List<StatusEntryTreeViewNode> nodes, StatusEntryTreeViewNode node)
        {
            nodes.Add(node);
            if (node.Children != null && node.Children.Count > 0)
            {
                foreach (var item in node.Children)
                {
                    BuildMultipleSelectionNodes(nodes, item);
                }
            }
        }


        //节点转路径
        private HashSet<string> TreeNodesToPaths(IEnumerable<StatusEntryTreeViewNode> nodes)
        {
            HashSet<string> filePaths = new HashSet<string>();
            foreach (var item in nodes)
            {
                if (item.Data != null)
                {
                    filePaths.Add(item.Data.FilePath);
                }
                else
                {
                    if (item.Children != null && item.Children.Count > 0)
                    {
                        var childFilePaths = TreeNodesToPaths(item.Children);
                        foreach (var childFilePath in childFilePaths)
                        {
                            filePaths.Add(childFilePath);
                        }
                    }
                }
            }
            return filePaths;
        }
    }

   
}

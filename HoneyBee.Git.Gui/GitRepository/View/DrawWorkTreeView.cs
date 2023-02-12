using ImGuiNET;
using LibGit2Sharp;
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
    public class DrawWorkTreeView
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

        public DrawWorkTreeView(GitRepo gitRepo)
        {
            m_nodeDefaultFlags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.FramePadding;
            m_gitRepo = gitRepo;
            m_diffShowView = new DiffShowView();
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
                m_gitRepo.Unstage();
                UpdateStatus();
            }
            ImGui.SameLine();
            if (ImGui.Button("Unstage Selected"))
            {
                if (m_stageSelectedNodes.Count > 0)
                {
                    var selectPath = TreeNodesToPaths(m_stageSelectedNodes);
                    m_gitRepo.Unstage(selectPath);
                    UpdateStatus();
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
                m_gitRepo.Stage();
                UpdateStatus();
            }
            ImGui.SameLine();
            if (ImGui.Button("Stage Selected"))
            {
                if (m_unstageSelectedNodes.Count > 0)
                {
                    var selectPath = TreeNodesToPaths(m_unstageSelectedNodes);
                    m_gitRepo.Stage(selectPath);
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
                ImGui.GetWindowDrawList().AddImage(node.NodeOpened? LuaPlugin.GetFolderIcon("default_folder_opened").Image: LuaPlugin.GetFolderIcon("default_folder").Image, folderIconPos, folderIconPosMax);

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
                switch (statusEntry.State)
                {
                    case FileStatus.NewInIndex:
                    case FileStatus.NewInWorkdir:
                        statusIcon = "add";
                        break;
                    case FileStatus.DeletedFromIndex:
                    case FileStatus.DeletedFromWorkdir:
                        statusIcon = "delete";
                        break;
                    case FileStatus.RenamedInIndex:
                    case FileStatus.RenamedInWorkdir:
                        statusIcon = "modified";
                        break;
                    case FileStatus.ModifiedInIndex:
                    case FileStatus.ModifiedInWorkdir:
                        statusIcon = "modified";
                        break;
                    case FileStatus.TypeChangeInIndex:
                    case FileStatus.TypeChangeInWorkdir:
                        statusIcon = "modified";
                        break;
                    case FileStatus.Conflicted:
                        statusIcon = "warn";
                        break;
                    default:
                        break;
                }

                var fileIconPos = ImGui.GetWindowPos() + ImGui.GetCursorPos() + new Vector2(ImGui.GetTextLineHeight(), -ImGui.GetScrollY() + Application.FontOffset);
                var fileIconPosMax = fileIconPos + new Vector2(ImGui.GetTextLineHeight() * 2, ImGui.GetTextLineHeight());
                bool selectableSelected = selected;

                var nodeFlag = selected ? m_nodeDefaultFlags | ImGuiTreeNodeFlags.Selected | ImGuiTreeNodeFlags.Leaf : m_nodeDefaultFlags| ImGuiTreeNodeFlags.Leaf;

                if (ImGui.TreeNodeEx($"\t\t\t{node.Name}", nodeFlag))
                {
                    ImGui.TreePop();
                }

                //文件图标
                ImGui.GetWindowDrawList().AddImage(LuaPlugin.GetFileIcon(node.Name).Image, fileIconPos, fileIconPosMax);
                var statusIconPos = fileIconPosMax;
                statusIconPos.Y = fileIconPos.Y;
                var statusIconPosMax = statusIconPos + Vector2.One * ImGui.GetTextLineHeight();
                ImGui.GetWindowDrawList().AddImage(LuaPlugin.GetIcon(statusIcon).Image, statusIconPos, statusIconPosMax);

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
                var statuses = m_gitRepo.Repo.RetrieveStatus();

                m_stageTreeView.Clear();
                m_unstageTreeView.Clear();
                m_stageSelectedNodes.Clear();
                m_unstageSelectedNodes.Clear();
                m_stageMultipleSelectionNodes.Clear();
                m_unstageMultipleSelectionNodes.Clear();

        

                //foreach (var item in statuses.Staged)
                //{
                //    StatusEntryTreeViewNode.JoinTreeViewNode(m_stageTreeView, item.FilePath, item);
                //}


                foreach (var item in statuses)
                {
                    if (item.State == FileStatus.Ignored)
                    {
                        continue;
                    }

                    if (item.State == FileStatus.NewInIndex || item.State == FileStatus.ModifiedInIndex || item.State == FileStatus.RenamedInIndex || item.State == FileStatus.TypeChangeInIndex)
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

                //foreach (var item in statuses.Staged)
                //{
                //    StatusEntryTreeViewNode.JoinTreeViewNode(m_stageTreeView,item.FilePath,item);
                //}

                //foreach (var item in statuses.Added)
                //{
                //    StatusEntryTreeViewNode.JoinTreeViewNode(m_stageTreeView, item.FilePath, item);
                //}

                //foreach (var item in statuses.Removed)
                //{
                //    StatusEntryTreeViewNode.JoinTreeViewNode(m_stageTreeView, item.FilePath, item);
                //}

                //foreach (var item in statuses.RenamedInIndex)
                //{
                //    StatusEntryTreeViewNode.JoinTreeViewNode(m_stageTreeView, item.FilePath, item);
                //}

                //foreach (var item in m_stageTreeView)
                //{
                //    BuildMultipleSelectionNodes(m_stageMultipleSelectionNodes,item);
                //}

                //foreach (var item in statuses.Missing)
                //{
                //    StatusEntryTreeViewNode.JoinTreeViewNode(m_unstageTreeView, item.FilePath, item);
                //}

                //foreach (var item in statuses.Modified)
                //{
                //    StatusEntryTreeViewNode.JoinTreeViewNode(m_unstageTreeView, item.FilePath, item);
                //}

                //foreach (var item in statuses.Untracked)
                //{
                //    StatusEntryTreeViewNode.JoinTreeViewNode(m_unstageTreeView, item.FilePath, item);
                //}

                //foreach (var item in statuses.RenamedInWorkDir)
                //{
                //    StatusEntryTreeViewNode.JoinTreeViewNode(m_unstageTreeView, item.FilePath, item);
                //}

                //foreach (var item in m_unstageTreeView)
                //{
                //    BuildMultipleSelectionNodes(m_unstageMultipleSelectionNodes, item);
                //}
            }
            catch (Exception e)
            {
                Log.Warn("DrawWorkTreeView exception: {0}",e);
            }
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

using ImGuiNET;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
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

        private ShowDiffText m_showDiffText;

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
            m_showDiffText = new ShowDiffText();
            UpdateStatus();
        }

        public void Draw()
        {
            //ImGui.ShowStyleEditor();
            ImGui.BeginChild("WorkTreeView_Content", ImGui.GetWindowSize() - new Vector2(0, 100));
            m_horizontalSplitView.Begin();
            DrawStageStatus();
            m_horizontalSplitView.Separate();
            m_showDiffText.Draw();
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

            foreach (var item in m_stageTreeView)
            {
                DrawStatusEntryTreeNode(item,true);
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

            foreach (var item in m_unstageTreeView)
            {
                DrawStatusEntryTreeNode(item,false);
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


        private void DrawStatusEntryTreeNode(StatusEntryTreeViewNode node,bool isStage)
        {
            var selectNodes = isStage ? m_stageSelectedNodes : m_unstageSelectedNodes;
            var multipSelectNodes = isStage ? m_stageMultipleSelectionNodes : m_unstageMultipleSelectionNodes;
            bool selected = selectNodes.Contains(node);

            if (node.Children != null && node.Children.Count > 0)
            {
                var nodeFlag = selected ? m_nodeDefaultFlags | ImGuiTreeNodeFlags.Selected : m_nodeDefaultFlags;
                node.NodeOpened = ImGui.TreeNodeEx(node.Name, nodeFlag);
                if (ImGui.IsItemClicked())
                {
                    if (!ImGui.IsItemToggledOpen() )
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
                                int maxIndex = indexs[indexs.Count-1];
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
                string statusIcon = Icon.Get(Icon.Material_question_mark);
                switch (statusEntry.State)
                {
                    case FileStatus.NewInIndex:
                    case FileStatus.NewInWorkdir:
                        statusIcon = Icon.Get(Icon.Material_fiber_new);
                        break;
                    case FileStatus.DeletedFromIndex:
                    case FileStatus.DeletedFromWorkdir:
                        statusIcon = Icon.Get(Icon.Material_delete);
                        break;
                    case FileStatus.RenamedInIndex:
                    case FileStatus.RenamedInWorkdir:
                        statusIcon = Icon.Get(Icon.Material_edit_note);
                        break;
                    case FileStatus.ModifiedInIndex:
                    case FileStatus.ModifiedInWorkdir:
                        statusIcon = Icon.Get(Icon.Material_update);
                        break;
                    case FileStatus.TypeChangeInIndex:
                    case FileStatus.TypeChangeInWorkdir:
                        statusIcon = Icon.Get(Icon.Material_change_circle);
                        break;
                    case FileStatus.Conflicted:
                        statusIcon = Icon.Get(Icon.Material_warning);
                        break;
                    default:
                        break;
                }

                bool selectableSelected = selected;
                if (ImGui.Selectable(statusIcon+node.Name, ref selectableSelected))
                {
                    var patch = m_gitRepo.Diff.Compare<Patch>(m_gitRepo.Repo.Head.Tip.Tree, DiffTargets.Index | DiffTargets.WorkingDirectory, new List<string>() { statusEntry.FilePath });
                    var diffContext = patch.Content;
                    m_showDiffText.BuildDiffTexts(diffContext);

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
        private void UpdateStatus()
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

                foreach (var item in statuses.Staged)
                {
                    StatusEntryTreeViewNode.JoinTreeViewNode(m_stageTreeView,item.FilePath,item);
                }

                foreach (var item in m_stageTreeView)
                {
                    BuildMultipleSelectionNodes(m_stageMultipleSelectionNodes,item);
                }

                foreach (var item in statuses)
                {
                    if (statuses.Staged.Contains(item) || statuses.Ignored.Contains(item))
                        continue;

                    StatusEntryTreeViewNode.JoinTreeViewNode(m_unstageTreeView, item.FilePath, item);
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

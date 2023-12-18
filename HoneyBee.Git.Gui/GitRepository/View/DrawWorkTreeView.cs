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
    public class DrawWorkTreeView: DrawSubView
	{
        private SplitView m_horizontalSplitView = new SplitView(SplitView.SplitType.Horizontal);

        private GitRepo m_gitRepo;


        private ImGuiTreeNodeFlags m_nodeDefaultFlags;

        private List<StatusEntryTreeViewNode> m_stageTreeView=new List<StatusEntryTreeViewNode>();


        public DrawWorkTreeView(GitRepo gitRepo)
        {
            m_nodeDefaultFlags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.OpenOnDoubleClick | ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.FramePadding;
            m_gitRepo = gitRepo;
            UpdateStatus();
        }

        public override void OnEnable()
        {
            base.OnEnable();

            UpdateStatus();
        }

        public override void OnDraw()
        {
            ImGui.BeginChild("WorkTreeView_Content", ImGui.GetWindowSize() - new Vector2(0, 100));
           
            ImGui.EndChild();
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
                    }
                }

                //foreach (var item in m_stageTreeView)
                //{
                //    BuildMultipleSelectionNodes(m_stageMultipleSelectionNodes, item);
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

using HoneyBee.Diff.Gui;
using ImGuiNET;
using strange.extensions.context.api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository.Common;
using Wanderer.GitRepository.Mediator;

namespace Wanderer.GitRepository.View
{
    public class GitRepoView : ImGuiTabView
    {
        private enum WorkSpaceRadio
        {
            WorkTree,
            CommitHistory,
        }
        
        private GitRepo m_gitRepo;
        private WorkSpaceRadio m_workSpaceRadio;

        private SplitView m_splitView = new SplitView(SplitView.SplitType.Horizontal, 2, 200);

        private GitRepoMediator m_gitRepoMediator;

        #region 子模块
        private DrawCommitHistoryView m_drawCommitHistoryView;
        #endregion

        public GitRepoView(IContext context) : base(context)
        {
            m_workSpaceRadio = WorkSpaceRadio.CommitHistory;
        }

        public void SetGitRepoPath(string repoPath)
        {
            m_gitRepoMediator = mediator as GitRepoMediator;
            m_gitRepo = m_gitRepoMediator.GetGitRepo(repoPath);
            if (m_gitRepo != null)
            {
                m_drawCommitHistoryView = new DrawCommitHistoryView(m_gitRepo);
            }
            m_gitRepo.SyncGitRepoToDatabase(() => {
                Log.Info("SyncGitRepoToDatabase complete");
            });
        }

        public override void OnDraw()
        {
            if (m_gitRepo == null)
                return;

            m_splitView.Begin();
            OnRepoKeysDraw();
            m_splitView.Separate();
            OnRepoContentDraw();
            m_splitView.End();
        }

        private void OnRepoKeysDraw()
        {
            DrawTreeNodeHead("Workspace", () => {
                if (ImGui.RadioButton("Work tree", m_workSpaceRadio == WorkSpaceRadio.WorkTree))
                {
                    m_workSpaceRadio = WorkSpaceRadio.WorkTree;
                    //_git.Status();
                }

                if (ImGui.RadioButton("Commit history", m_workSpaceRadio == WorkSpaceRadio.CommitHistory))
                {
                    m_workSpaceRadio = WorkSpaceRadio.CommitHistory;
                }
            });

            DrawTreeNodeHead("Branch", () => {
                foreach (var item in m_gitRepo.LocalBranchNodes)
                {
                    DrawBranchTreeNode(item);
                }
            });

            DrawTreeNodeHead("Tag", () => {
                foreach (var item in m_gitRepo.Tags)
                {
                    ImGui.Button($"{item.FriendlyName}");
                }
            });

            DrawTreeNodeHead("Remote", () => {
                foreach (var item in m_gitRepo.RemoteBranchNodes)
                {
                    DrawBranchTreeNode(item);
                }
            });

            DrawTreeNodeHead("Submodule", () => {
                foreach (var item in m_gitRepo.Submodules)
                {
                    ImGui.Button($"{item.Name}");
                }
            });
        }

        private void OnRepoContentDraw()
        {
            if (m_workSpaceRadio == WorkSpaceRadio.CommitHistory)
            {
                OnDrawCommitHistory();
            }
            else
            {
                OnDrawWorkTree();
            }
        }

        private void OnDrawWorkTree()
        {
            //_workTreeView.OnDraw(_git, _git.CurrentStatuses, _git.Diff);
        }

    

        private void OnDrawCommitHistory()
        {
            if (m_drawCommitHistoryView != null)
            {
                m_drawCommitHistoryView.Draw();
            }
        }


        private void DrawTreeNodeHead(string name, Action onDraw)
        {
            string key = $"TreeNode_{name}";
            bool oldTreeNodeOpen = m_gitRepoMediator.GetUserData<bool>(key);
            ImGui.SetNextItemOpen(oldTreeNodeOpen);
            bool treeNodeOpen = ImGui.TreeNode(name);
            if (treeNodeOpen)
            {
                onDraw();
                ImGui.TreePop();
            }
            if (treeNodeOpen != oldTreeNodeOpen)
            {
                m_gitRepoMediator.SetUserData<bool>(key, treeNodeOpen);
            }
        }

        private void DrawBranchTreeNode(GitBranchNode branchNode)
        {
            bool treeNodeEx = false;

            if (branchNode.Children != null && branchNode.Children.Count > 0)
            {
                treeNodeEx = ImGui.TreeNode(branchNode.Name);
                if (treeNodeEx)
                {
                    foreach (var item in branchNode.Children)
                    {
                        DrawBranchTreeNode(item);
                    }
                    ImGui.TreePop();
                }
            }
            else
            {
                Vector2 textSize = ImGui.CalcTextSize(branchNode.Name);
                uint textColor = ImGui.GetColorU32(ImGuiCol.Text);
                if (branchNode.Branch.IsCurrentRepositoryHead)
                {
                    textColor = ImGui.GetColorU32(ImGuiCol.HeaderActive);
                }
                ImGui.TextColored(ImGui.ColorConvertU32ToFloat4(textColor), $"\t{branchNode.Name}");
            }

            if (!treeNodeEx || branchNode.Branch != null)
            {
                var pos = ImGui.GetItemRectMax();
                pos.Y -= 15;

                if (branchNode.BehindBy > 0)
                {
                    string showTipText = $"{Icon.Get(Icon.Material_arrow_downward)}{branchNode.BehindBy}";
                    var textSize = ImGui.CalcTextSize(showTipText);
                    ImGui.GetWindowDrawList().AddText(pos, ImGui.GetColorU32(ImGuiCol.Text), showTipText);
                    pos.X += textSize.X;
                }

                if (branchNode.AheadBy > 0)
                {
                    string showTipText = $"{Icon.Get(Icon.Material_arrow_upward)}{branchNode.AheadBy}";
                    //Vector2 textSize = ImGui.CalcTextSize(showTipText);
                    ImGui.GetWindowDrawList().AddText(pos, ImGui.GetColorU32(ImGuiCol.Text), showTipText);
                }
            }
        }

    }
}

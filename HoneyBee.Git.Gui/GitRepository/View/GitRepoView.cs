using ImGuiNET;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
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
using Wanderer.Common;
using Wanderer.GitRepository.Common;
using Wanderer.GitRepository.Mediator;

namespace Wanderer.GitRepository.View
{
    public class GitRepoView : ImGuiTabView
    {
        public override string Name => m_gitRepo==null ? base.Name: m_gitRepo.Name;

        public override string UniqueKey => m_repoPath;

        private GitRepo m_gitRepo;
        private WorkSpaceRadio m_workSpaceRadio;

        private SplitView m_splitView = new SplitView(SplitView.SplitType.Horizontal, 200);

        private GitRepoMediator m_gitRepoMediator;
        private string m_repoPath;
        private Dictionary<string, int> _toolItems = new Dictionary<string, int>();
        private string m_syncDataTip;
        private float m_syncProgress;

        #region 子模块
        private DrawWorkTreeView m_workTreeView;
        private DrawCommitHistoryView m_commitHistoryView;
        #endregion

        public GitRepoView(IContext context, string repoPath) : base(context)
        {
            m_workSpaceRadio = WorkSpaceRadio.CommitHistory;

            m_repoPath = repoPath;
            m_gitRepoMediator = mediator as GitRepoMediator;

            _toolItems = new Dictionary<string, int>();
            _toolItems.Add("Commit", Icon.Material_add);
            _toolItems.Add("Sync", Icon.Material_sync);
            _toolItems.Add("Pull", Icon.Material_download);
            _toolItems.Add("Push", Icon.Material_publish);
            _toolItems.Add("Fetch", Icon.Material_downloading);
            _toolItems.Add("Settings", Icon.Material_settings);
            _toolItems.Add("Terminal", Icon.Material_terminal);
        }

        //public void SetGitRepoPath(string repoPath)
        //{
           
        //}

        private void CreateGitRepo()
        {
            if (m_gitRepo == null)
            {
                m_gitRepo = new GitRepo(m_repoPath);
                if (m_gitRepo != null)
                {
                    m_workTreeView = new DrawWorkTreeView(m_gitRepo);
                    m_commitHistoryView = new DrawCommitHistoryView(m_gitRepo);
                }
            }

            m_gitRepo.SyncGitRepoTask(null, null);
            
            //m_syncDataTip = "正在同步数据";
            //m_gitRepo.SyncGitRepoTask((progress) => {
            //    m_syncProgress = progress;
            //},() => {
            //    m_syncDataTip = null;
            //});
        }

        protected override void OnDestroy()
        {
            m_gitRepo?.Dispose();
            m_gitRepo = null;
            base.OnDestroy();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            CreateGitRepo();
        }


        public override void OnDisable()
        {
            //m_gitRepo?.Dispose();
            //m_gitRepo = null;
            base.OnDisable();
        }

        public override void OnDraw()
        {
            if (m_gitRepo == null)
                return;
            OnToolbarDraw();

            if (!string.IsNullOrEmpty(m_syncDataTip))
            {
                //ImGui.TextColored(new Vector4(0,1,0,1), m_syncDataTip);
                //ImGui.SameLine();
                
                ImGui.ProgressBar(m_syncProgress,new Vector2(ImGui.GetWindowWidth(),0), $"{m_syncDataTip} {m_syncProgress}");
                ImGui.Separator();
            }

            m_splitView.Begin();
            OnRepoKeysDraw();
            m_splitView.Separate();
            OnRepoContentDraw();
            m_splitView.End();
        }

        protected void OnToolbarDraw()
        {
            int itemIndex = 0;
            foreach (var item in _toolItems)
            {
                if (DrawToolItem(Icon.Get(item.Value), item.Key, false))
                {
                    OnClickToolbar(item.Key);
                }
                if (itemIndex >= 0 && itemIndex < _toolItems.Count - 1)
                {
                    ImGui.SameLine();
                }
                itemIndex++;
            }
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
                ImGui.TextUnformatted(tip);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
            if (active)
                ImGui.GetWindowDrawList().AddLine(p1, p2, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            return buttonClick;
        }

        private void OnClickToolbar(string item)
        {
            switch (item)
            {
                case "Sync":
                    //_git.UpdateStatus();
                    break;
                case "Terminal":
                    GitCommandView.ShowTerminal(m_repoPath);
                    break;
                case "Pull":
                    GitCommandView.RunGitCommandView<PullGitCommand>(m_gitRepo);
                    break;
                case "Fetch":
                    GitCommandView.RunGitCommandView<FetchGitCommand>(m_gitRepo);
                    break;
                case "Push":
                    GitCommandView.RunGitCommandView<PushGitCommand>(m_gitRepo);
                    break;
                default:
                    break;
            }

        }

        private void OnRepoKeysDraw()
        {
            DrawTreeNodeHead("Workspace", () => {
                if (ImGui.RadioButton("Work Tree", m_workSpaceRadio == WorkSpaceRadio.WorkTree))
                {
                    m_workSpaceRadio = WorkSpaceRadio.WorkTree;
                    //_git.Status();
                }

                if (ImGui.RadioButton("Commit History", m_workSpaceRadio == WorkSpaceRadio.CommitHistory))
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

            DrawTreeNodeHead("Remote", () => {
                foreach (var item in m_gitRepo.RemoteBranchNodes)
                {
                    DrawBranchTreeNode(item);
                }
            });

            DrawTreeNodeHead("Tag", () => {
                foreach (var item in m_gitRepo.Repo.Tags)
                {
                    if (ImGui.MenuItem(item.FriendlyName))
                    {
                        m_gitRepo.SelectCommit = m_gitRepo.GetCommit(item.Target.Sha);
                    }
                }
            ;
            });

            DrawTreeNodeHead("Submodule", () => {
                foreach (var item in m_gitRepo.Submodules)
                {
                    if (ImGui.MenuItem(item.Name))
                    {

                    }
                }
            });

            DrawTreeNodeHead("Stashes", () => {
                foreach (var item in m_gitRepo.Stashes)
                {
                    ImGui.MenuItem($"{item.Message}");
                }

                //if (ImGui.Button("Save Stashe"))
                //{
                    
                //}
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
            if (m_workTreeView != null)
            {
                m_workTreeView.Draw();
            }
        }

    

        private void OnDrawCommitHistory()
        {
            if (m_commitHistoryView != null)
            {
                m_commitHistoryView.Draw();
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
            Vector2 currentPos = ImGui.GetCursorPos();

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
                bool isCurrentRepositoryHead = branchNode.Branch.IsCurrentRepositoryHead;

                if (isCurrentRepositoryHead)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetColorU32(ImGuiCol.HeaderActive));
                }

                if (ImGui.MenuItem($"\t{branchNode.Name}", "", isCurrentRepositoryHead))
                { 
                    //m_gitRepo.SelectCommit = m_gitRepo.GetCommit(branchNode.Branch.Reference.TargetIdentifier);
                }

                if (isCurrentRepositoryHead)
                {
                    ImGui.PopStyleColor();
                }

                //右键菜单
                if (ImGui.BeginPopupContextItem())
                {
                    if (branchNode.Branch.IsRemote)
                    {
                        ImGui.Text(Icon.Get(Icon.Material_cloud));
                        ImGui.SameLine();
                        ImGui.Text(branchNode.FullName);
                        ImGui.Separator();
                        var viewCommands = GitCommandView.ViewCommands.FindAll(x => x.Target == ViewCommandTarget.Remote);
                        if (viewCommands != null && viewCommands.Count > 0)
                        {
                            foreach (var item in viewCommands)
                            {
                                if (ImGui.MenuItem(item.Name))
                                {
                                    GitCommandView.RunGitCommandView<CommonProcessGitCommand>(m_gitRepo, item);
                                }
                            }
                        }
                    }
                    else
                    {
                        ImGui.Text(Icon.Get(Icon.Material_download_for_offline));
                        ImGui.SameLine();
                        ImGui.Text(branchNode.FullName);
                        ImGui.Separator();
                        if (isCurrentRepositoryHead)
                        {
                            var viewCmds = GitCommandView.ViewCommands.FindAll(x => x.Target == ViewCommandTarget.Head);
                            if (viewCmds != null && viewCmds.Count > 0)
                            {
                                foreach (var item in viewCmds)
                                {
                                    if (ImGui.MenuItem(item.Name))
                                    {
                                        GitCommandView.RunGitCommandView<CommonProcessGitCommand>(m_gitRepo, item);
                                    }
                                }
                            }
                        }
                        var viewCommands = GitCommandView.ViewCommands.FindAll(x => x.Target == ViewCommandTarget.Branch);
                        if (viewCommands != null && viewCommands.Count > 0)
                        {
                            foreach (var item in viewCommands)
                            {
                                if (ImGui.MenuItem(item.Name))
                                {
                                    GitCommandView.RunGitCommandView<CommonProcessGitCommand>(m_gitRepo, item);
                                }
                            }
                        }
                    }
                    ImGui.EndPopup();
                }
            }

            if (!treeNodeEx || branchNode.Branch != null)
            {
                currentPos  = currentPos + ImGui.GetWindowPos() - new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY());
                //currentPos.X += ImGui.CalcTextSize(nodeName).X;
                currentPos.X += ImGui.CalcItemWidth();
                //pos.Y -= 15;

                if (branchNode.BehindBy > 0)
                {
                    string showTipText = $"{Icon.Get(Icon.Material_arrow_downward)}{branchNode.BehindBy}";
                    var textSize = ImGui.CalcTextSize(showTipText);
                    ImGui.GetWindowDrawList().AddText(currentPos, ImGui.GetColorU32(ImGuiCol.Text), showTipText);
                    currentPos.X += textSize.X;
                }

                if (branchNode.AheadBy > 0)
                {
                    string showTipText = $"{Icon.Get(Icon.Material_arrow_upward)}{branchNode.AheadBy}";
                    //Vector2 textSize = ImGui.CalcTextSize(showTipText);
                    ImGui.GetWindowDrawList().AddText(currentPos, ImGui.GetColorU32(ImGuiCol.Text), showTipText);
                }
            }
        }


        private enum WorkSpaceRadio
        {
            WorkTree,
            CommitHistory,
        }
    }
}

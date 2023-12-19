﻿using ImGuiNET;
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
using Wanderer.App;
using Wanderer.App.Service;
using Wanderer.Common;
using Wanderer.GitRepository.Common;

namespace Wanderer
{ 
	public class DrawSubView : strange.extensions.mediation.impl.View
	{
        public virtual string Name { get; }
		public virtual void OnDraw()
		{ }
	}
}

namespace Wanderer.GitRepository.View
{
	public class GitRepoView : ImGuiTabView
    {
        public override string Name => m_gitRepo == null ? base.Name : m_gitRepo.Name;

        public override string UniqueKey => m_repoPath;

        private GitRepo m_gitRepo;
        //private WorkSpaceRadio m_workSpaceRadio;

        private SplitView m_splitView = new SplitView(SplitView.SplitType.Horizontal, 200);

        //private GitRepoMediator m_gitRepoMediator;
        private string m_repoPath;
        private Dictionary<string, int> _toolItems = new Dictionary<string, int>();
        private string m_syncDataTip;
        private float m_syncProgress;

        [Inject]
        public IDatabaseService database { get; set; }

        #region 子模块
  //      private DrawWorkTreeView m_workTreeView;
  //      private DrawWorkSpaceView m_workSpaceView;
		//private DrawCommitHistoryView m_commitHistoryView;
        private List<DrawSubView> m_submoduleViewList;
		private DrawSubView m_submoduleSelectView;
		#endregion

		public Action<string> OnTextEditor;

        public GitRepoView(string repoPath) 
        {
            m_repoPath = repoPath;
            //m_gitRepoMediator = mediator as GitRepoMediator;

            _toolItems = new Dictionary<string, int>();
            //_toolItems.Add("Commit", Icon.Material_add);
            _toolItems.Add("Sync", Icon.Material_sync);
            _toolItems.Add("Pull", Icon.Material_download);
            _toolItems.Add("Push", Icon.Material_publish);
            _toolItems.Add("Fetch", Icon.Material_downloading);
            //_toolItems.Add("Settings", Icon.Material_settings);
            _toolItems.Add("Terminal", Icon.Material_terminal);
            _toolItems.Add("Explorer", Icon.Material_folder_open);

            m_gitRepo = new GitRepo(m_repoPath);

			var workTreeView = AppContextView.AddView<DrawWorkTreeView>(m_gitRepo);
			var workSpaceView = AppContextView.AddView<DrawWorkSpaceView>(m_gitRepo);
            var commitHistoryView = AppContextView.AddView<DrawCommitHistoryView>(m_gitRepo);

            m_submoduleViewList = new List<DrawSubView>() { workTreeView , workSpaceView, commitHistoryView };
            m_submoduleSelectView = workSpaceView;
		}

        //public void SetGitRepoPath(string repoPath)
        //{

        //}

        private void CreateGitRepo()
        {
			m_gitRepo.ReBuildUIData();
            
            //m_syncDataTip = "正在同步数据";
            //m_gitRepo.SyncGitRepoTask((progress) => {
            //    m_syncProgress = progress;
            //},() => {
            //    m_syncDataTip = null;
            //});
        }

        protected override void OnDestroy()
        {
            m_submoduleSelectView = null;

			if (m_submoduleViewList != null)
            {
                foreach (var item in m_submoduleViewList)
                {
                    if (item == null)
                    {
                        continue;
                    }
					AppContextView.RemoveView(item);
				}
				m_submoduleViewList.Clear();
				m_submoduleViewList = null;
			}

            m_gitRepo?.Dispose();
            m_gitRepo = null;
            base.OnDestroy();
        }

        public override void OnEnable()
        {
            base.OnEnable();
            Directory.SetCurrentDirectory(m_gitRepo.RootPath);
            CreateGitRepo();

            if (m_submoduleSelectView != null)
            {
                m_submoduleSelectView.OnEnable();
			}
        }


        public override void OnDisable()
        {
			//m_gitRepo?.Dispose();
			//m_gitRepo = null;
			if (m_submoduleSelectView != null)
			{
				m_submoduleSelectView.OnDisable();
			}
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
                    m_gitRepo?.ReBuildUIData();
                    break;
                case "Terminal":
                    GitCommandView.ShowTerminal(m_repoPath);
                    break;
                case "Pull":
                    AppContextView.AddView<GitView>(m_gitRepo).Pull().Run();
                    break;
                case "Fetch":
					AppContextView.AddView<GitView>(m_gitRepo).Fecth().Run();
                    break;
                case "Push":
					AppContextView.AddView<GitView>(m_gitRepo).Push().Run();
                    break;
                case "Explorer":
                    Process.Start("Explorer", m_gitRepo.RootPath.Replace("/","\\"));
                    break;
                default:
                    break;
            }
        }

        private void OnRepoKeysDraw()
        {
            DrawTreeNodeHead("Workspace", () => {
				DrawSubView submoduleSelectView = m_submoduleSelectView;
				foreach (var item in m_submoduleViewList)
                {
					if (ImGui.RadioButton(item.Name,m_submoduleSelectView == item))
					{
						submoduleSelectView = item;
					}
				}

                if (submoduleSelectView != m_submoduleSelectView)
                {
                    foreach (var item in m_submoduleViewList)
                    {
                        if (item != null && item != submoduleSelectView)
                        {
                            item.OnDisable();
						}
                    }
					m_submoduleSelectView = submoduleSelectView;
					m_submoduleSelectView.OnEnable();
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
            if (m_submoduleSelectView != null)
            {
                m_submoduleSelectView.OnDraw();

			}
        }

        private void DrawTreeNodeHead(string name, Action onDraw)
        {
            string key = $"TreeNode_{name}_{m_gitRepo.RootPath}";
            //bool oldTreeNodeOpen = m_gitRepoMediator.GetUserData<bool>(key);
            bool oldTreeNodeOpen = database ==null ?false: database.GetCustomerData<bool>(key);
            ImGui.SetNextItemOpen(oldTreeNodeOpen);
            bool treeNodeOpen = ImGui.TreeNode(name);
            if (treeNodeOpen)
            {
                onDraw();
                ImGui.TreePop();
            }

            if (treeNodeOpen != oldTreeNodeOpen)
            {
                if (database != null)
                {
                    database.SetCustomerData<bool>(key, treeNodeOpen);
                }
            }
        }

        private void DrawBranchTreeNode(BranchTreeViewNode branchNode)
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
                bool isCurrentRepositoryHead = branchNode.Data.IsCurrentRepositoryHead;

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
                    OnDrawBranchPopupContextItem(branchNode);
                    ImGui.EndPopup();
                }
            }

            if (!treeNodeEx || branchNode.Data != null)
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


        private void OnDrawBranchPopupContextItem(BranchTreeViewNode branchNode)
        {
            string branchIcon = branchNode.Data.IsRemote ? Icon.Get(Icon.Material_cloud) : Icon.Get(Icon.Material_download_for_offline);
            ImGui.Text(branchIcon);
            ImGui.SameLine();
            ImGui.Text(branchNode.FullName);
            ImGui.Separator();

            if (branchNode.Data.IsRemote)
            {
                if (ImGui.MenuItem("Fetch..."))
                {
                    //GitCommandView.RunGitCommandView<HandleGitCommand>(() =>
                    //{
                    //    int friendlyIndex = branchNode.Data.FriendlyName.IndexOf("/")+1;
                    //    string friendlyName = branchNode.Data.FriendlyName.Substring(friendlyIndex, branchNode.Data.FriendlyName.Length- friendlyIndex);
                    //    string fetchCmd = $"fetch {branchNode.Data.RemoteName} {friendlyName}:{friendlyName}";
                    //    ImGui.Text("Confirm whether to fetch the selected branch？");
                    //    ImGui.Text(fetchCmd);

                    //    if (ImGui.Button("OK"))
                    //    {
                    //        GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, fetchCmd);
                    //        return false;
                    //    }

                    //    ImGui.SameLine();
                    //    if (ImGui.Button("Cancel"))
                    //    {
                    //        return false;
                    //    }
                    //    return true;

                    //});
                }

                if (ImGui.MenuItem("Delete..."))
                {
                    //GitCommandView.RunGitCommandView<HandleGitCommand>(() =>
                    //{
                    //    int friendlyIndex = branchNode.Data.FriendlyName.IndexOf("/") + 1;
                    //    string friendlyName = branchNode.Data.FriendlyName.Substring(friendlyIndex, branchNode.Data.FriendlyName.Length - friendlyIndex);
                    //    string deleteCmd = $"push {branchNode.Data.RemoteName} --delete {friendlyName}";
                    //    ImGui.Text("Confirm whether to delete the selected branch？");
                    //    ImGui.Text(deleteCmd);

                    //    if (ImGui.Button("OK"))
                    //    {
                    //        GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, deleteCmd);
                    //        return false;
                    //    }

                    //    ImGui.SameLine();
                    //    if (ImGui.Button("Cancel"))
                    //    {
                    //        return false;
                    //    }
                    //    return true;

                    //});
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Copy Branch Name"))
                {
					Application.SetClipboard(branchNode.FullName);
                }
                ImGui.Separator();
                ImGui.Text("More...");
            }
            else
            {
                if (ImGui.MenuItem("Check Out"))
                {
                    //GitCommandView.RunGitCommandView<HandleGitCommand>( () =>
                    //{
                    //    GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, $"checkout {branchNode.Data.FriendlyName}");
                    //    return false;
                    //});
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Pull..."))
                {
                    //GitCommandView.RunGitCommandView<HandleGitCommand>( () =>
                    //{
                    //    string pullCmd = $"pull {branchNode.Data.RemoteName} {branchNode.Data.FriendlyName}:{branchNode.Data.FriendlyName}";
                    //    ImGui.Text("Confirm whether to pull the selected branch？");
                    //    ImGui.Text(pullCmd);

                    //    if (ImGui.Button("OK"))
                    //    {
                    //        GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, pullCmd);
                    //        return false;
                    //    }

                    //    ImGui.SameLine();
                    //    if (ImGui.Button("Cancel"))
                    //    {
                    //        return false;
                    //    }
                    //    return true;

                    //});
                }

                if (ImGui.MenuItem("Push..."))
                {
                    //GitCommandView.RunGitCommandView<HandleGitCommand>( () =>
                    //{
                    //    string pushCmd = $"push {branchNode.Data.RemoteName} {branchNode.Data.FriendlyName}:{branchNode.Data.FriendlyName}";
                    //    ImGui.Text("Confirm whether to push the selected branch？");
                    //    ImGui.Text(pushCmd);

                    //    if (ImGui.Button("OK"))
                    //    {
                    //        GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, pushCmd);
                    //        return false;
                    //    }

                    //    ImGui.SameLine();
                    //    if (ImGui.Button("Cancel"))
                    //    {
                    //        return false;
                    //    }
                    //    return true;

                    //});
                }

                if (ImGui.MenuItem("Fetch..."))
                {
                    //GitCommandView.RunGitCommandView<HandleGitCommand>( () =>
                    //{
                    //    string fetchCmd = $"fetch {branchNode.Data.RemoteName} {branchNode.Data.FriendlyName}:{branchNode.Data.FriendlyName}";
                    //    ImGui.Text("Confirm whether to fetch the selected branch？");
                    //    ImGui.Text(fetchCmd);

                    //    if (ImGui.Button("OK"))
                    //    {
                    //        GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, fetchCmd);
                    //        return false;
                    //    }

                    //    ImGui.SameLine();
                    //    if (ImGui.Button("Cancel"))
                    //    {
                    //        return false;
                    //    }
                    //    return true;

                    //});
                }

                if (ImGui.MenuItem("Tracking..."))
                {
                    //GitCommandView.RunGitCommandView<HandleGitCommand>( () =>
                    //{
                    //    string fetchCmd = $"fetch {branchNode.Data.RemoteName} {branchNode.Data.FriendlyName}:{branchNode.Data.FriendlyName}";
                    //    ImGui.Text("Confirm whether to fetch the selected branch？");
                    //    ImGui.Text(fetchCmd);

                    //    if (ImGui.Button("OK"))
                    //    {
                    //        GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, fetchCmd);
                    //        return false;
                    //    }

                    //    ImGui.SameLine();
                    //    if (ImGui.Button("Cancel"))
                    //    {
                    //        return false;
                    //    }
                    //    return true;

                    //});
                }

                ImGui.Separator();

                //非当前分支
                if (branchNode.Data.IsCurrentRepositoryHead)
                {
                    
                }
                else
                {
                    var headBranch = m_gitRepo.Repo.Head;
                    if (ImGui.MenuItem($"Merge into '{headBranch.FriendlyName}'..."))
                    {
                        //GitCommandView.RunGitCommandView<HandleGitCommand>( () =>
                        //{
                        //    string mergeCmd = $"merge {branchNode.Data.FriendlyName}";
                        //    ImGui.Text("Confirm whether to merge the selected branch？");
                        //    ImGui.Text(mergeCmd);

                        //    if (ImGui.Button("OK"))
                        //    {
                        //        GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, mergeCmd);
                        //        return false;
                        //    }

                        //    ImGui.SameLine();
                        //    if (ImGui.Button("Cancel"))
                        //    {
                        //        return false;
                        //    }
                        //    return true;
                        //});
                    }

                    if (ImGui.MenuItem($"Rebase '{headBranch.FriendlyName}' on '{branchNode.FullName}'..."))
                    {
                        //GitCommandView.RunGitCommandView<HandleGitCommand>( () =>
                        //{
                        //    string rebaseCmd = $"rebase {branchNode.Data.FriendlyName}";
                        //    ImGui.Text("Confirm whether to rebase the selected branch？");
                        //    ImGui.Text(rebaseCmd);

                        //    if (ImGui.Button("OK"))
                        //    {
                        //        GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, rebaseCmd);
                        //        return false;
                        //    }

                        //    ImGui.SameLine();
                        //    if (ImGui.Button("Cancel"))
                        //    {
                        //        return false;
                        //    }
                        //    return true;
                        //});
                    }
                }

                if (ImGui.MenuItem("New Branch..."))
                {
                    string newBranchName = "";
                    //GitCommandView.RunGitCommandView<HandleGitCommand>( () =>
                    //{
                    //    ImGui.InputText("New Branch Name", ref newBranchName, 200);

                    //    if (ImGui.Button("OK"))
                    //    {
                    //        if (!string.IsNullOrEmpty(newBranchName))
                    //        {
                    //            GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, $"branch -c {branchNode.Data.FriendlyName} {newBranchName}");
                    //        }
                    //        return false;
                    //    }

                    //    ImGui.SameLine();
                    //    if (ImGui.Button("Cancel"))
                    //    {
                    //        return false;
                    //    }
                    //    return true;

                    //});
                }

                if (ImGui.MenuItem("Rename..."))
                {
                    string newBranchName = "";
                    //GitCommandView.RunGitCommandView<HandleGitCommand>( () =>
                    //{
                    //    ImGui.InputText("New Branch Name", ref newBranchName, 200);

                    //    if (ImGui.Button("OK"))
                    //    {
                    //        if (!string.IsNullOrEmpty(newBranchName))
                    //        {
                    //            GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, $"branch -m {branchNode.Data.FriendlyName} {newBranchName}");
                    //        }
                    //        return false;
                    //    }

                    //    ImGui.SameLine();
                    //    if (ImGui.Button("Cancel"))
                    //    {
                    //        return false;
                    //    }
                    //    return true;

                    //});
                }

                if (ImGui.MenuItem("Delete..."))
                {
                    //GitCommandView.RunGitCommandView<HandleGitCommand>( () =>
                    //{
                    //    ImGui.Text("Confirm whether to delete the selected branch？");

                    //    if (ImGui.Button("OK"))
                    //    {
                    //        GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, $"branch -d {branchNode.Data.FriendlyName}");
                    //        return false;
                    //    }

                    //    ImGui.SameLine();
                    //    if (ImGui.Button("Cancel"))
                    //    {
                    //        return false;
                    //    }
                    //    return true;

                    //});
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Copy Branch Name"))
                {
                    Application.SetClipboard(branchNode.FullName);
                }

                ImGui.Separator();

                ImGui.Text("More...");
            }
            // plugin.CallPopupContextItem("OnBranchPopupItem");

        }

        private enum WorkSpaceRadio
        {
			WorkTree,
			WorkSpace,
			CommitHistory,
        }
    }
}

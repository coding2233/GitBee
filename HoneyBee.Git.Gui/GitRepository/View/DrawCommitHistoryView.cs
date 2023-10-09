using ImGuiNET;
using LibGit2Sharp;
using SFB;
using strange.extensions.dispatcher.eventdispatcher.api;
using strange.extensions.mediation.impl;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Wanderer.App;
using Wanderer.App.Service;
using Wanderer.App.View;
using Wanderer.Common;
using Wanderer.GitRepository.Common;


namespace Wanderer.GitRepository.View
{
    internal class DrawCommitHistoryView: strange.extensions.mediation.impl.View
    {
        private GitRepo m_gitRepo;
        private int m_commitAddInterval = 20;
        private int m_commitViewIndex = 0;
        private int m_commitViewMax = 200;
        private float m_lastCommitScrollY = 0.0f;
        private Commit m_selectCommit;
        private Patch m_selectCommitPatch;
        private PatchEntryChanges m_selectCommitPatchEntry;

        private DiffShowView m_diffShowView;
        private SplitView m_contentSplitView;

        private SplitView m_selectCommitDiffSpliteView;
        private SplitView m_selectCommitTreeSpliteView;

        private Range m_cacheRange;
        private IEnumerable<Commit> m_cacheCommits;
        private List<CommitTabInfo> m_tabShowCommits;

        private string[] m_localBranchs=new string[0];
        private int m_selectLocalBranch;
        private string m_searchCommit = "";

        public DrawCommitHistoryView(GitRepo gitRepo)
        {
            m_contentSplitView = new SplitView(SplitView.SplitType.Vertical);
            m_selectCommitDiffSpliteView = new SplitView(SplitView.SplitType.Horizontal);
            m_selectCommitTreeSpliteView = new SplitView(SplitView.SplitType.Vertical);

            m_diffShowView = new DiffShowView();

            m_gitRepo = gitRepo;
        }

        public override void OnEnable()
        {
            base.OnEnable();

            GetHistoryCommits(true);
        }


        public void Draw()
        {
            m_contentSplitView.Begin();
            DrawHistoryCommits();
            m_contentSplitView.Separate();
            DrawSelectCommit();
            m_contentSplitView.End();
        }


        private void DrawHistoryCommits()
        {
            int commitMax = m_gitRepo.CommitCount;

            var itemWidth = ImGui.GetWindowWidth() * 0.2f;
            ImGui.SetNextItemWidth(itemWidth);
            bool gethistoryCommitsForce = false;
            if (ImGui.Combo("Branch", ref m_selectLocalBranch, m_localBranchs, m_localBranchs.Length))
            {
                gethistoryCommitsForce = true;
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(160);
            int newCommitViewIndex = m_commitViewIndex;
            if (ImGui.InputInt($"Commit Range ({m_commitViewIndex}-{m_commitViewIndex + m_commitViewMax}/{commitMax})##Commit-Index-InputInt", ref newCommitViewIndex, 1, 100, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (newCommitViewIndex != m_commitViewIndex)
                {
                    m_commitViewIndex = newCommitViewIndex;
                }
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(itemWidth);
            if (ImGui.InputText("Search", ref m_searchCommit, 200, ImGuiInputTextFlags.EnterReturnsTrue))
            {

            }
            if (ImGui.IsItemHovered() && string.IsNullOrEmpty(m_searchCommit))
            {
                ImGui.SetTooltip("Please input sha/message/author/date time...");
            }

            ImGui.BeginChild("DrawHistoryCommits-TableData");

            if (m_lastCommitScrollY <= 0.0f)
            {
                //float moveInterval = GetScrollInterval(_commitViewIndex - _commitAddInterval >= 0 ? _commitAddInterval : _commitViewIndex - _commitAddInterval);
                m_commitViewIndex -= m_commitAddInterval;
                m_commitViewIndex = Math.Max(m_commitViewIndex, 0);
                if (m_commitViewIndex > 0)
                    ImGui.SetScrollY(GetScrollInterval(m_commitAddInterval));
            }
            else if (m_lastCommitScrollY >= ImGui.GetScrollMaxY())
            {
                if (commitMax >= m_commitViewMax)
                {
                    m_commitViewIndex += m_commitAddInterval;
                    commitMax = commitMax - m_commitViewMax;
                    m_commitViewIndex = Math.Min(m_commitViewIndex, commitMax);
                }
                else
                {
                    m_commitViewIndex = 0;
                }

                if (m_commitViewIndex > 0 && m_commitViewIndex < commitMax)
                    ImGui.SetScrollY(ImGui.GetScrollMaxY() - GetScrollInterval(m_commitAddInterval));
            }
            m_lastCommitScrollY = ImGui.GetScrollY();

            GetHistoryCommits(gethistoryCommitsForce);
            if (m_tabShowCommits==null)
                return;

            if (ImGui.BeginTable("GitRepo-Commits", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable))
            {
                //图谱
                ImGui.TableSetupColumn("Graph", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize);
                ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Date", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Author", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Commit", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize);
                ImGui.TableHeadersRow();

                List<CommitAtlasLine> commitAtlasLines = new List<CommitAtlasLine>();
                int atalsMaxId = -1;
                foreach (var item in m_tabShowCommits)
                {
                    //if (index < m_commitViewIndex)
                    //    continue;
                    //else if (index >= m_commitViewIndex + m_commitViewMax)
                    //    break;

                    //表格
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);

                    //图谱绘制
                    int atlasId = 0;
                    float pointXOffset = 0;
                    var atlasLines = commitAtlasLines.FindAll(x => x.Parent == item.Sha);
                    if (atlasLines != null && atlasLines.Count > 0)
                    {
                        foreach (var itemLine in atlasLines)
                        {
                            if (atlasId == 0)
                            {
                                atlasId = itemLine.AtlasId;
                            }
                            else
                            {
                                atlasId = Math.Min(atlasId, itemLine.AtlasId);
                            }
                            commitAtlasLines.Remove(itemLine);
                        }

                    }
                    else
                    {
                        atlasId = atalsMaxId + 1;
                    }
                    pointXOffset = ImGui.GetTextLineHeight() * atlasId;

                    var atlasPoint = ImGui.GetCursorPos() + ImGui.GetWindowPos() + new Vector2(pointXOffset, ImGui.GetTextLineHeight() * 0.5f - ImGui.GetScrollY());
                    ImGui.GetWindowDrawList().AddCircleFilled(atlasPoint, ImGui.GetTextLineHeight() * 0.25f, ImGui.GetColorU32(ImGuiCol.ButtonActive));
                    if (atlasLines != null && atlasLines.Count > 0)
                    {
                        foreach (var itemLine in atlasLines)
                        {
                            ImGui.GetWindowDrawList().AddLine(itemLine.ChildPoint, atlasPoint, ImGui.GetColorU32(ImGuiCol.ButtonActive));
                            Pool<CommitAtlasLine>.Release(itemLine);
                        }
                    }


                    if (item.Parents != null)
                    {
                        int itemIndex = 0;
                        foreach (var itemParent in item.Parents)
                        {
                            var atlasLine = Pool<CommitAtlasLine>.Get();
                            atlasLine.AtlasId = atlasId + itemIndex;
                            atlasLine.ChildPoint = atlasPoint;
                            atlasLine.Parent = itemParent;
                            itemIndex++;
                            commitAtlasLines.Add(atlasLine);

                            atalsMaxId = Math.Max(atalsMaxId, atlasLine.AtlasId);
                        }
                    }

                    //ImGui.Text("");
                    ImGui.SetCursorPosX(ImGui.GetCursorPosX() + pointXOffset + ImGui.GetTextLineHeight());
                    ImGui.TableSetColumnIndex(1);


                    if (m_gitRepo.CommitNotes.TryGetValue(item.Sha, out List<string> notes))
                    {
                        if (notes != null && notes.Count > 0)
                        {
                            foreach (var itemNote in notes)
                            {
                                var noteRectMin = ImGui.GetWindowPos() - new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY()) + ImGui.GetCursorPos();
                                var noteRectMax = noteRectMin + ImGui.CalcTextSize(itemNote);

                                //ImGuiView.Colors[1]-Vector4.One*0.5f)
                                ImGui.GetWindowDrawList().AddRectFilled(noteRectMin, noteRectMax, ImGui.GetColorU32(ImGuiCol.TextSelectedBg));

                                //int colorIndex = branchIndex % ImGuiView.Colors.Count;
                                //var textColor = ImGuiView.Colors[0];

                                ImGui.Text(itemNote);
                                //ImGui.TextColored(textColor, itemNote);
                                ImGui.SameLine();
                            }
                        }
                    }

                    //ImGui.Text(item.MessageShort);
                    if (ImGui.Selectable(item.Message, m_selectCommit != null && m_selectCommit.Sha == item.Sha, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        m_gitRepo.SetSelectCommit(item.Sha);
                        //m_gitRepo.SelectCommit = m_gitRepo.SetSelectCommit(item.Sha);
                    }

                    //右键菜单 - test
                    if (ImGui.BeginPopupContextItem(item.Sha))
                    {
                        //m_gitRepo.SelectCommit = item;
                        m_gitRepo.SetSelectCommit(item.Sha);

                        if (m_gitRepo.SelectCommit != null)
                        {
                            ImGui.Text(Icon.Get(Icon.Material_commit));
                            ImGui.SameLine();
                            ImGui.Text(item.ShaShort);
                            ImGui.SameLine();
                            ImGui.Text(item.Message);
                            ImGui.Separator();
                            OnCommitPopupContextItem(item);
                        }
                        ImGui.EndPopup();
                    }

                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(item.DateTime);
                    ImGui.TableSetColumnIndex(3);
                    ImGui.Text(item.Author);// [{item.Committer.Email}]
                    ImGui.TableSetColumnIndex(4);
                    ImGui.Text($"{item.ShaShort}");
                }

                Pool<CommitAtlasLine>.Release(commitAtlasLines);

                ImGui.EndTable();
            }

            ImGui.EndChild();
        }


        private void OnCommitPopupContextItem(CommitTabInfo item)
        {
            var headBranch = m_gitRepo.Repo.Head;
            string headBranchwName = $"'{headBranch}'";
            if (ImGui.MenuItem("New Branch..."))
            {
                AppContextView.AddView<GitNewBranchCommandView>(m_gitRepo,item.Sha);
            }
            if (ImGui.MenuItem("New Tag..."))
            {
                AppContextView.AddView<GitNewTagCommandView>(m_gitRepo, item.Sha);
            }
            ImGui.Separator();
            if (ImGui.MenuItem("CheckOut Commit..."))
            {
                //GitCommandView.RunGitCommandView<HandleGitCommand>(() =>
                //{
                //    string checkoutCmd = $"checkout {item.Sha}";
                //    ImGui.Text("Confirm whether to checkout the selected commit？");
                //    ImGui.Text(checkoutCmd);

                //    if (ImGui.Button("OK"))
                //    {
                //        GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, checkoutCmd);
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
            if (ImGui.MenuItem($"Reset {headBranchwName} on this commit..."))
            {
                //GitCommandView.RunGitCommandView<HandleGitCommand>( () =>
                //{
                //    string resetCmd = $"reset --hard {item.Sha}";
                //    ImGui.Text("Confirm whether to reset the selected commit？");
                //    ImGui.Text(resetCmd);

                //    if (ImGui.Button("OK"))
                //    {
                //        GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, resetCmd);
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

            if (ImGui.MenuItem($"Rebase {headBranchwName} on this commit..."))
            {
                //GitCommandView.RunGitCommandView<HandleGitCommand>(() =>
                //{
                //    string rebaseCmd = $"rebase {item.Sha}";
                //    ImGui.Text("Confirm whether to rebase the selected commit？");
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
            if (ImGui.MenuItem("Revert Commit..."))
            {
                bool commitChange = true;
                //GitCommandView.RunGitCommandView<HandleGitCommand>(() =>
                //{
                //    ImGui.Text("Confirm whether to Revert the selected commit？");
                //    ImGui.Checkbox("Commit this change", ref commitChange);

                //    if (ImGui.Button("OK"))
                //    {
                //        string revertCmd = commitChange ? $"revert {item.Sha}" : $"revert --no-commit {item.Sha}";
                //        GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, revertCmd);
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
            if (ImGui.MenuItem("Cherry-Pick Commit..."))
            {
                //git cherry-pick <commmit>
                bool commitChange = true;
                //GitCommandView.RunGitCommandView<HandleGitCommand>(() =>
                //{
                //    ImGui.Text("Confirm whether to cherry-pick the selected commit？");
                //    ImGui.Checkbox("Commit this change", ref commitChange);

                //    if (ImGui.Button("OK"))
                //    {
                //        string cherrypickCmd = commitChange ? $"cherry-pick {item.Sha}" : $"cherry-pick --no-commit {item.Sha}";
                //        GitCommandView.RunGitCommandView<CommonGitCommand>(m_gitRepo, cherrypickCmd);
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
            if (ImGui.MenuItem("Save As Patch"))
            {
                //var commitPatch = GetCommitPatch(item);
                //StandaloneFileBrowser.SaveFilePanelAsync("Save As Path", null, item.Sha, "patch", (savePath) => {
                //    if (!string.IsNullOrEmpty(savePath))
                //    {
                //        File.WriteAllText(savePath, commitPatch);
                //    }
                //});
            }
            ImGui.Separator();
            if (ImGui.MenuItem("Copy Commit Info"))
            {
                Application.SetClipboard($"{item.Sha} {item.Author} {item.DateTime} {item.Message}");
            }
            if (ImGui.MenuItem("Copy Commit Hash"))
            {
                Application.SetClipboard(item.Sha);
            }
            ImGui.Separator();
            ImGui.Text("More...");
        }

        private void DrawSelectCommit()
        {
            m_selectCommitDiffSpliteView.Begin();

            m_selectCommitTreeSpliteView.Begin();
            //提交信息
            DrawSelectCommitInfo();
            m_selectCommitTreeSpliteView.Separate();
            //文件树
            DrawSelectCommitTree();
            m_selectCommitTreeSpliteView.End();

            m_selectCommitDiffSpliteView.Separate();
            //绘制选择文件
            DrawSelectCommitDiff();
            m_selectCommitDiffSpliteView.End();


        }


        private void DrawSelectCommitInfo()
        {
            if (m_selectCommit != m_gitRepo.SelectCommit)
            {
                BuildSelectCommitPatch(m_gitRepo.SelectCommit);
                m_selectCommit = m_gitRepo.SelectCommit;
                return;
            }

            if (m_selectCommit == null)
            {
                return;
            }

            ImGui.Text($"Sha: {m_selectCommit.Sha}");
            ImGui.Text("Parents:");
            if (m_selectCommit.Parents != null)
            {
                foreach (var itemParent in m_selectCommit.Parents)
                {
                    ImGui.SameLine();
                    if (ImGui.Button(itemParent.Sha.Substring(0, 10)))
                    {
                        m_gitRepo.SelectCommit = itemParent;
                        //SelectCommit(itemParent);
                    }
                }
            }
            ImGui.Text($"Author: {m_selectCommit.Author.Name} <{m_selectCommit.Author.Email}>");
            ImGui.Text($"DateTime: {m_selectCommit.Author.When.DateTime.ToString()}");
            //ImGui.Text($"Committer: {m_selectCommit.Author} {m_selectCommit.Email}\n");
            ImGui.Spacing();
            ImGui.Text(m_selectCommit.Message);
        }

        private void DrawSelectCommitTree()
        {
            if (m_selectCommitPatch != null)
            {
                foreach (var item in m_selectCommitPatch)
                {
                    //if (item.IsBinaryComparison)
                    //{
                    //    ImGui.Separator();
                    //    ImGui.Text($"{item.id}");
                    //    ImGui.Text($"{item.OldPath}");
                    //}
                    if (ImGui.RadioButton(item.Path, m_selectCommitPatchEntry == item))
                    {
                        m_selectCommitPatchEntry = item;
                        m_diffShowView.Build(item,m_gitRepo);
                    }
                }
            }
        }

        private void DrawSelectCommitDiff()
        {
            if (m_selectCommitPatchEntry != null)
            {
                m_diffShowView?.Draw();
            }
        }


        private void BuildSelectCommitPatch(Commit gitRepoCommit)
        {
            m_selectCommit = gitRepoCommit;
            m_selectCommitPatch = null;
            m_selectCommitPatchEntry = null;

            //子线程取真正的数据绘制
            Task.Run(() =>
            {
                m_selectCommitPatch = GetCommitPatch(m_selectCommit);
            });
        }

        //获取当前提交的Patch
        private Patch GetCommitPatch(Commit commit)
        {
            Patch commitPatch = null;
            if (commit != null)
            {
                if (commit.Parents != null && commit.Parents.Count() > 0)
                {
                    foreach (var itemParent in commit.Parents)
                    {
                        var diffPatch = m_gitRepo.Diff.Compare<Patch>(itemParent.Tree, commit.Tree);
                        
                        if (commitPatch == null)
                        {
                            commitPatch = diffPatch;
                        }
                        else
                        {
                            foreach (var item in diffPatch)
                            {
                                commitPatch.Append(item);
                            }
                        }
                    }
                }
                else
                {
                    commitPatch = m_gitRepo.Diff.Compare<Patch>(null, commit.Tree);
                }
            }
            return commitPatch;
        }

        private float GetScrollInterval(float size)
        {
            return ImGui.GetScrollMaxY() * (size / m_commitViewMax);
        }

        void GetHistoryCommits(bool force=false)
        {
            if (m_cacheCommits == null && m_gitRepo!=null)
            {
                //这里可以增加更多的条件，方便操作更多的信息
                var range = new Range(m_commitViewIndex, m_commitViewIndex + m_commitViewMax);
                if (!range.Equals(m_cacheRange) || force)
                {
                    m_gitRepo.RunTask(() => {

                        List<string> localBranch = new List<string>();
                        localBranch.Add("Default-All-Branch");
                        foreach (var item in m_gitRepo.Repo.Branches)
                        {
                            if (!item.IsRemote)
                            {
                                localBranch.Add(item.FriendlyName);
                            }
                        }
                        m_localBranchs = localBranch.ToArray();

                        if (m_selectLocalBranch <= 0)
                        {
                            m_cacheCommits = m_gitRepo.Repo.Commits.Take(range);
                        }
                        else
                        {
                            var filter = new CommitFilter
                            {
                                //ExcludeReachableFrom = m_gitRepo.Repo.Branches["master"],       // formerly "Since"
                                IncludeReachableFrom = m_localBranchs[m_selectLocalBranch],  // formerly "Until"
                            };
                            m_cacheCommits = m_gitRepo.Repo.Commits.QueryBy(filter).Take(range);
                        }

                        if (!string.IsNullOrEmpty(m_searchCommit))
                        {
                            m_cacheCommits = m_cacheCommits.Where((commitInfo) =>
                            {
                                if (commitInfo.Message.Contains(m_searchCommit))
                                {
                                    return true;
                                }
                                if (commitInfo.MessageShort.Contains(m_searchCommit))
                                {
                                    return true;
                                }
                                if (commitInfo.Sha.Contains(m_searchCommit))
                                {
                                    return true;
                                }
                                if (commitInfo.Author.Name.Contains(m_searchCommit))
                                {
                                    return true;
                                }
                                if (commitInfo.Committer.Name.Contains(m_searchCommit))
                                {
                                    return true;
                                }
                                if (commitInfo.Author.When.DateTime.ToString().Contains(m_searchCommit))
                                {
                                    return true;
                                }
                                if (commitInfo.Committer.When.DateTime.ToString().Contains(m_searchCommit))
                                {
                                    return true;
                                }
                                return false;
                            });
                        }


                        //更新tab显示数据
                        List<CommitTabInfo> tabShowCommits = new List<CommitTabInfo>();
                        foreach (var item in m_cacheCommits)
                        {
                            CommitTabInfo commitInfo = null;
                            if (m_tabShowCommits != null)
                            {
                                commitInfo = m_tabShowCommits.Find(x => x.Sha.Equals(item.Sha));
                            }

                            if (commitInfo != null)
                            {
                                m_tabShowCommits.Remove(commitInfo);
                            }
                            else
                            {
                                commitInfo = Pool<CommitTabInfo>.Get().SetCommit(item);
                            }

                            tabShowCommits.Add(commitInfo);
                        }

                        if (m_tabShowCommits != null)
                        {
                            Pool<CommitTabInfo>.Release(m_tabShowCommits);
                        }
                        m_tabShowCommits = tabShowCommits;


                    });

                    m_cacheRange = range;
                }
            }
        }

        public class CommitAtlasLine : IPool
        {
            public string Parent;
            public Vector2 ChildPoint;
            public int AtlasId;
            public void OnGet()
            {
            }

            public void OnRelease()
            {
                Parent = null;
                ChildPoint = Vector2.Zero;
                AtlasId = 0;
            }
        }


        public class CommitTabInfo : IPool
        {
            public string Sha { get; private set; }
            public string ShaShort { get; private set; }
            public string Author { get; private set; }
            public string Message { get; private set; }
            public string DateTime { get; private set; }
            public List<string> Parents { get; private set; }
            public void OnGet()
            {
            }

            public void OnRelease()
            {
                Sha = null;
                ShaShort = null;
                Author = null;
                Message = null;
                DateTime = null;
                Parents = null;
            }

            public CommitTabInfo SetCommit(Commit commit)
            {
                Sha = commit.Sha;
                ShaShort = Sha.Substring(0, 10);
                Author = commit.Author.Name;
                Message = commit.MessageShort;
                DateTime = commit.Author.When.DateTime.ToString();
                if (commit.Parents!=null)
                {
                    Parents = new List<string>();
                    foreach (var item in commit.Parents)
                    {
                        Parents.Add(item.Sha);
                    }
                }
                return this;
            }
        }

    }
}

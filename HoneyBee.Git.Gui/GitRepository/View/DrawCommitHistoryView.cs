using ImGuiNET;
using LibGit2Sharp;
using strange.extensions.dispatcher.eventdispatcher.api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository.Common;

namespace Wanderer.GitRepository.View
{
    internal class DrawCommitHistoryView
    {
        private GitRepo m_gitRepo;
        private int m_commitAddInterval = 5;
        private int m_commitViewIndex = 0;
        private int m_commitViewMax = 50;
        private float m_lastCommitScrollY = 0.0f;
        private Commit m_selectCommit;
        private Patch m_selectCommitPatch;
        private PatchEntryChanges m_selectCommitPatchEntry;

        private ShowDiffText m_showDiffText;
        private SplitView m_contentSplitView;

        private SplitView m_selectCommitDiffSpliteView;
        private SplitView m_selectCommitTreeSpliteView;

        private Range m_cacheRange;
        private IEnumerable<Commit> m_cacheCommits;

        private Dictionary<string, int> m_commitForBranchIndex;
        public DrawCommitHistoryView(GitRepo gitRepo)
        {
            m_contentSplitView = new SplitView(SplitView.SplitType.Vertical);
            m_selectCommitDiffSpliteView = new SplitView(SplitView.SplitType.Horizontal);
            m_selectCommitTreeSpliteView = new SplitView(SplitView.SplitType.Vertical);

            m_showDiffText = new ShowDiffText();
            m_commitForBranchIndex = new Dictionary<string, int>();

            m_gitRepo = gitRepo;
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
            int commitMax = m_gitRepo.GetCommitCount();
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

            var historyCommits = GetHistoryCommits();
            if (historyCommits == null)
                return;

            Dictionary<string, CommitBranchDrawIndex> commitBranchDrawIndex=new Dictionary<string, CommitBranchDrawIndex>();

            if (ImGui.BeginTable("GitRepo-Commits", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable))
            {
                ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Date", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Author", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Commit", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize);
                ImGui.TableHeadersRow();

                int index = 0;
                foreach (var item in historyCommits)
                {
                    index++;
                    //if (index < m_commitViewIndex)
                    //    continue;
                    //else if (index >= m_commitViewIndex + m_commitViewMax)
                    //    break;

                    int branchIndex = 0;
                    //if (item.Branchs != null)
                    //{
                    //    bool find = false;
                    //    for (int i = 0; i < m_gitRepo.Branches.Count(); i++)
                    //    {
                    //        foreach (var itemBranch in item.Branchs)
                    //        {
                    //            if (itemBranch.Equals(m_gitRepo.Branches.ElementAt(i).CanonicalName))
                    //            {
                    //                branchIndex = i;
                    //                find = true;
                    //                break;
                    //            }
                    //        }
                    //        if (find)
                    //        {
                    //            break;
                    //        }
                    //    }
                        
                    //}

                    //表格
                    ImGui.TableNextRow();
                 
                    ImGui.TableSetColumnIndex(0);
                  
                    if (m_gitRepo.CommitNotes.TryGetValue(item.Sha, out List<string> notes))
                    {
                        if (notes != null && notes.Count > 0)
                        {
                            foreach (var itemNote in notes)
                            {
                                var noteRectMin = ImGui.GetWindowPos()-new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY())+ImGui.GetCursorPos();
                                var noteRectMax = noteRectMin+ImGui.CalcTextSize(itemNote);

                                //ImGuiView.Colors[1]-Vector4.One*0.5f)
                                ImGui.GetWindowDrawList().AddRectFilled(noteRectMin, noteRectMax,ImGui.GetColorU32(ImGuiCol.TextSelectedBg));

                                int colorIndex = branchIndex % ImGuiView.Colors.Count;
                                var textColor = ImGuiView.Colors[colorIndex];

                                ImGui.Text(itemNote);
                                //ImGui.TextColored(textColor, itemNote);
                                ImGui.SameLine();
                            }
                        }
                    }
                    ImGui.Text(item.MessageShort);

        

                    //CommitBranchDrawIndex commitBranchDrawIndex1 = new CommitBranchDrawIndex();
                    //commitBranchDrawIndex1.BranchIndex = m_gitRepo.GetCommitBranchIndex(item.Commit);
                    //commitBranchDrawIndex1.Point = ImGui.GetWindowPos() + ImGui.GetCursorPos() + new Vector2(5 * commitBranchDrawIndex1.BranchIndex, -ImGui.GetScrollY());
                    //commitBranchDrawIndex1.Parents = item.Parents;
                    //commitBranchDrawIndex.Add(item.Commit, commitBranchDrawIndex1);

                    var rectMin = ImGui.GetItemRectMin();
                    var rectMax = ImGui.GetItemRectMax();
                    rectMax.X = rectMin.X + ImGui.GetColumnWidth();
                    //当前选中的提交
                    if (m_selectCommit != null && m_selectCommit.Sha == item.Sha)
                    {
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.TabActive));
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.GetColorU32(ImGuiCol.TabActive));
                    }
                    else
                    {
                        if (ImGui.IsWindowFocused() && !ImGui.IsMouseDragging(ImGuiMouseButton.Left) && ImGui.IsMouseHoveringRect(rectMin, rectMax))
                        {
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.TabActive));
                            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.GetColorU32(ImGuiCol.TabActive));

                            if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                            {
                                m_gitRepo.SelectCommit = item;
                                //SelectCommit(item);
                            }
                        }
                    }
                  
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(item.Author.When.DateTime.ToString());
                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(item.Author.Name);// [{item.Committer.Email}]
                    ImGui.TableSetColumnIndex(3);
                    ImGui.Text($"{item.Sha.Substring(0, 10)}");
                }
                ImGui.EndTable();

                //右键菜单 - test
                if (ImGui.BeginPopupContextItem())
                {
                    ImGui.MenuItem("new branch...");
                    ImGui.MenuItem("new tag...");
                    ImGui.MenuItem("checkout commit...");
                    ImGui.MenuItem("revert commit...");
                    ImGui.MenuItem("cherry-pick commit...");
                    ImGui.EndPopup();
                }
            }

            //if (commitBranchDrawIndex.Count > 0)
            //{
            //    DrawIndex(historyCommits[0].Commit, commitBranchDrawIndex);
            //}

        }

        private void DrawIndex(string key, Dictionary<string, CommitBranchDrawIndex> commitBranchDrawIndex)
        {
            if (commitBranchDrawIndex.TryGetValue(key, out CommitBranchDrawIndex start))
            {
                ImGui.GetWindowDrawList().AddCircleFilled(start.Point,3, ImGui.ColorConvertFloat4ToU32(new Vector4(0, 1, 0, 1)));

                if (start.Parents != null && start.Parents.Count > 0)
                {
                    foreach (var item in start.Parents)
                    {
                        if (commitBranchDrawIndex.TryGetValue(item, out CommitBranchDrawIndex end))
                        {
                            ImGui.GetWindowDrawList().AddLine(start.Point,end.Point,ImGui.ColorConvertFloat4ToU32(new Vector4(1,0,0,1)));
                            DrawIndex(item, commitBranchDrawIndex);
                        }
                    }
                }
            }
        }

        struct CommitBranchDrawIndex
        {
            public int BranchIndex;
            public Vector2 Point;
            public List<string> Parents;
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
                    if (ImGui.RadioButton(item.Path, m_selectCommitPatchEntry == item))
                    {
                        m_selectCommitPatchEntry = item;
                        m_showDiffText.BuildDiffTexts(item.Patch);
                    }
                }
            }
        }

        private void DrawSelectCommitDiff()
        {
            if (m_selectCommitPatchEntry != null)
            {
                m_showDiffText.Draw();
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
                if (m_selectCommit != null)
                {
                    //CommitFilter commitFilter = new CommitFilter();
                    if (m_selectCommit != null)
                    {
                        if (m_selectCommit.Parents != null && m_selectCommit.Parents.Count() > 0)
                        {
                            foreach (var itemParent in m_selectCommit.Parents)
                            {
                                var diffPatch = m_gitRepo.Diff.Compare<Patch>(itemParent.Tree, m_selectCommit.Tree);
                                if (m_selectCommitPatch == null)
                                {
                                    m_selectCommitPatch = diffPatch;
                                }
                                else
                                {
                                    foreach (var item in diffPatch)
                                    {
                                        m_selectCommitPatch.Append(item);
                                    }
                                }
                            }
                        }

                    }
                }


            });
        }

        private float GetScrollInterval(float size)
        {
            return ImGui.GetScrollMaxY() * (size / m_commitViewMax);
        }

        IEnumerable<Commit> GetHistoryCommits()
        {
            var range = new Range(m_commitViewIndex, m_commitViewIndex + m_commitViewMax);
            if (!range.Equals(m_cacheRange))
            {
                m_cacheCommits = m_gitRepo.Repo.Commits.Take(range);
                m_cacheRange = range;
            }
            return m_cacheCommits;
        }
    }
}

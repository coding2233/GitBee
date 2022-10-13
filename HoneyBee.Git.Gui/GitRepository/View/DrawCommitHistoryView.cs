using ImGuiNET;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Wanderer.GitRepository.Common;

namespace Wanderer.GitRepository.View
{
    internal class DrawCommitHistoryView
    {
        private GitRepo m_gitRepo;
        private int m_commitAddInterval = 5;
        private int m_commitViewIndex = 0;
        private int m_commitViewMax = 100;
        private float m_lastCommitScrollY = 0.0f;
        private GitRepoCommit m_selectCommit;
        private Patch m_selectCommitPatch;
        private PatchEntryChanges m_selectCommitPatchEntry;

        private ShowDiffText m_showDiffText;
        private SplitView m_contentSplitView;

        private SplitView m_selectCommitDiffSpliteView;
        private SplitView m_selectCommitTreeSpliteView;

        private List<GitRepoCommit> m_cacheCommits;

        public DrawCommitHistoryView(GitRepo gitRepo)
        {
            m_contentSplitView = new SplitView(SplitView.SplitType.Vertical);
            m_selectCommitDiffSpliteView = new SplitView(SplitView.SplitType.Horizontal);
            m_selectCommitTreeSpliteView = new SplitView(SplitView.SplitType.Vertical);

            m_showDiffText = new ShowDiffText();

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

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(item.Description);
                    var rectMin = ImGui.GetItemRectMin();
                    var rectMax = ImGui.GetItemRectMax();
                    rectMax.X = rectMin.X + ImGui.GetColumnWidth();
                    //当前选中的提交
                    if (m_selectCommit != null && m_selectCommit.Commit == item.Commit)
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
                                SelectCommit(item);
                            }
                        }
                    }
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(item.Date);
                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(item.Author);// [{item.Committer.Email}]
                    ImGui.TableSetColumnIndex(3);
                    ImGui.Text($"{item.Commit.Substring(0, 10)}");
                }
                ImGui.EndTable();
            }

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
            if (m_selectCommit == null)
            {
                return;
            }

            ImGui.Text($"Sha: {m_selectCommit.Commit}");
            ImGui.Text("Parents:");
            if (m_selectCommit.Parents != null)
            {
                foreach (var item in m_selectCommit.Parents)
                {
                    ImGui.SameLine();
                    if (ImGui.Button(item.Substring(0, 10)))
                    {
                        SelectCommit(m_gitRepo.GetGitCommit(item));

                    }
                }
            }
            ImGui.Text($"Author: {m_selectCommit.Author} {m_selectCommit.Email}");
            ImGui.Text($"DateTime: {m_selectCommit.Date}");
            ImGui.Text($"Committer: {m_selectCommit.Author} {m_selectCommit.Email}\n");

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


        private void SelectCommit(GitRepoCommit gitRepoCommit)
        {
            m_selectCommit = gitRepoCommit;
            m_selectCommitPatch = null;
            m_selectCommitPatchEntry = null;

            //子线程取真正的数据绘制
            Task.Run(() => {
                if (m_selectCommit != null)
                {
                    var commit = m_gitRepo.GetCommit(m_selectCommit.Commit);
                    if (m_selectCommit != null && commit != null && m_selectCommit.Commit.Equals(commit.Sha))
                    {
                        if (commit.Parents != null && commit.Parents.Count() > 0)
                        {
                            foreach (var itemParent in commit.Parents)
                            {
                                var diffPatch = m_gitRepo.Diff.Compare<Patch>(itemParent.Tree, commit.Tree);
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

        List<GitRepoCommit> GetHistoryCommits()
        {
            m_cacheCommits = m_gitRepo.GetCommits(m_commitViewIndex, m_commitViewIndex+m_commitViewMax);
            return m_cacheCommits;
        }
    }
}

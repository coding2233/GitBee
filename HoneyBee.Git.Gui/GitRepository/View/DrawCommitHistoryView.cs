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
        private Commit m_selectCommit;

        private SplitView m_contentSplitView;


        private List<GitRepoCommit> m_cacheCommits;

        public DrawCommitHistoryView(GitRepo gitRepo)
        {
            m_contentSplitView = new SplitView(SplitView.SplitType.Vertical,0.6f);
            m_gitRepo = gitRepo;
        }

        public void Draw()
        {
            m_contentSplitView.Begin();
            DrawHistoryCommits();
            m_contentSplitView.Separate();
            OnDrawSelectCommit();
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
                    if (m_selectCommit != null && m_selectCommit.Sha == item.Commit)
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
                                m_selectCommit = m_gitRepo.GetCommit(item.Commit);
                                //if (index < historyCommits.Count)
                                //{
                                //    _selectParentCommit = historyCommits[index];
                                //}
                                //else
                                //{
                                //    _selectParentCommit = null;
                                //}
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

        private void OnDrawSelectCommit()
        {
            if (m_selectCommit == null)
            {
                return;
            }
            
            ImGui.Text($"Sha: {m_selectCommit.Sha}");
            ImGui.Text("Parents:");
            if (m_selectCommit.Parents != null)
            {
                foreach (var item in m_selectCommit.Parents)
                {
                    ImGui.SameLine();
                    if (ImGui.Button(item.Sha.Substring(0, 10)))
                    {
                        m_selectCommit = m_gitRepo.GetCommit(item.Sha);
                    }
                }
            }
            ImGui.Text($"Author: {m_selectCommit.Author.Name} {m_selectCommit.Author.Email}");
            ImGui.Text($"DateTime: {m_selectCommit.Author.When.ToString()}");
            ImGui.Text($"Committer: {m_selectCommit.Committer.Name} {m_selectCommit.Committer.Email}\n");

            ImGui.Text(m_selectCommit.Message);
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

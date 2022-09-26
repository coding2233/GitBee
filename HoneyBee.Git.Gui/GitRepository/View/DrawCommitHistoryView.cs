using ImGuiNET;
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
        private int m_commitReadIndex = 0;

        private List<GitRepoCommit> m_cacheCommits;

        public DrawCommitHistoryView(GitRepo gitRepo)
        {
            m_gitRepo = gitRepo;
        }

        public void Draw()
        {
            var historyCommits = GetHistoryCommits();
            if (historyCommits == null)
                return;

            //if (_selectCommit != null)
            //{
            //    _contentSplitView.Begin();
            //}

            int commitMax = historyCommits.Count();
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
                    if (index < m_commitViewIndex)
                        continue;
                    else if (index >= m_commitViewIndex + m_commitViewMax)
                        break;

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.Text(item.Description);
                    var rectMin = ImGui.GetItemRectMin();
                    var rectMax = ImGui.GetItemRectMax();
                    rectMax.X = rectMin.X + ImGui.GetColumnWidth();
                    if (ImGui.IsMouseHoveringRect(rectMin, rectMax))
                    {
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(ImGuiCol.TabActive));
                        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.GetColorU32(ImGuiCol.TabActive));
                        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
                        {
                            //_selectCommit = item;
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
                    ImGui.TableSetColumnIndex(1);
                    ImGui.Text(item.Date);
                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(item.Author);// [{item.Committer.Email}]
                    ImGui.TableSetColumnIndex(3);
                    ImGui.Text($"{item.Commit.Substring(0, 10)}");
                }
                ImGui.EndTable();
            }

            //if (_selectCommit != null)
            //{
            //    _contentSplitView.Separate();
            //    OnDrawSelectCommit(_selectCommit, _selectParentCommit);
            //    _contentSplitView.End();
            //}
        }

        private float GetScrollInterval(float size)
        {
            return ImGui.GetScrollMaxY() * (size / m_commitViewMax);
        }

        List<GitRepoCommit> GetHistoryCommits()
        {
            if (m_cacheCommits == null || m_cacheCommits.Count == 0)
            {
                m_cacheCommits = m_gitRepo.GetCommits(0);
            }
            return m_cacheCommits;
        }
    }
}

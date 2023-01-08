using ImGuiNET;
using LibGit2Sharp;
using strange.extensions.dispatcher.eventdispatcher.api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Wanderer.App.Service;
using Wanderer.App.View;
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
        private IPluginService m_plugin;
        private Dictionary<Commit, CommitAtlasInfo> m_commitAltas;
        private float m_drawAltasOffset;

        public DrawCommitHistoryView(GitRepo gitRepo, IPluginService plugin)
        {
            m_contentSplitView = new SplitView(SplitView.SplitType.Vertical);
            m_selectCommitDiffSpliteView = new SplitView(SplitView.SplitType.Horizontal);
            m_selectCommitTreeSpliteView = new SplitView(SplitView.SplitType.Vertical);

            m_showDiffText = new ShowDiffText();
            m_commitForBranchIndex = new Dictionary<string, int>();

            m_gitRepo = gitRepo;
            m_plugin = plugin;
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

            CommitAtlasInfo firstCommitAtlasInfo = null;

            //ImGui.SetCursorPosX(ImGui.GetCursorPosX()); 

            if (ImGui.BeginTable("GitRepo-Commits", 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable))
            {
                ImGui.TableSetupColumn("图谱", ImGuiTableColumnFlags.WidthFixed);
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
                    if (m_commitAltas != null)
                    {
                        if (m_commitAltas.TryGetValue(item, out CommitAtlasInfo atlasInfo))
                        {
                            atlasInfo.Point = ImGui.GetCursorPos() + ImGui.GetWindowPos() + new Vector2(atlasInfo.PointXOffset, ImGui.GetTextLineHeight() * 0.5f - ImGui.GetScrollY());
                            if (firstCommitAtlasInfo == null)
                            {
                                firstCommitAtlasInfo = atlasInfo;
                            }
                        }
                    }
                    ImGui.SetNextItemWidth(m_drawAltasOffset);
                    //ImGui.Text("");
                    ImGui.TableSetColumnIndex(1);

                    

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


                    //ImGui.Text(item.MessageShort);
                    if (ImGui.Selectable(item.MessageShort, m_selectCommit != null && m_selectCommit.Sha == item.Sha, ImGuiSelectableFlags.SpanAllColumns))
                    {
                        m_gitRepo.SelectCommit = item;
                    }

                    //右键菜单 - test
                    if (ImGui.BeginPopupContextItem(item.Sha))
                    {
                        m_gitRepo.SelectCommit = item;
                        if (m_gitRepo.SelectCommit != null)
                        {
                            ImGui.Text(Icon.Get(Icon.Material_commit));
                            ImGui.SameLine();
                            ImGui.Text(item.Sha.Substring(0, 10));
                            ImGui.SameLine();
                            ImGui.Text(item.MessageShort);
                            ImGui.Separator();

                            m_plugin.CallPopupContextItem("OnCommitPopupItem");
                            //var viewCommands = GitCommandView.ViewCommands.FindAll(x => x.Target == ViewCommandTarget.Commit);
                            //foreach (var itemViewCommand in viewCommands)
                            //{
                            //    if (ImGui.MenuItem(itemViewCommand.Name))
                            //    {
                            //        GitCommandView.RunGitCommandView<CommonProcessGitCommand>(m_gitRepo, itemViewCommand);
                            //    }
                            //}
                        }
                        ImGui.EndPopup();
                    }

                    //CommitBranchDrawIndex commitBranchDrawIndex1 = new CommitBranchDrawIndex();
                    //commitBranchDrawIndex1.BranchIndex = m_gitRepo.GetCommitBranchIndex(item.Commit);
                    //commitBranchDrawIndex1.Point = ImGui.GetWindowPos() + ImGui.GetCursorPos() + new Vector2(5 * commitBranchDrawIndex1.BranchIndex, -ImGui.GetScrollY());
                    //commitBranchDrawIndex1.Parents = item.Parents;
                    //commitBranchDrawIndex.Add(item.Commit, commitBranchDrawIndex1);

                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(item.Author.When.DateTime.ToString());
                    ImGui.TableSetColumnIndex(3);
                    ImGui.Text(item.Author.Name);// [{item.Committer.Email}]
                    ImGui.TableSetColumnIndex(4);
                    ImGui.Text($"{item.Sha.Substring(0, 10)}");

                 
                }
                ImGui.EndTable();

              
            }

            //绘制图谱
            DrawCommitAtlas(firstCommitAtlasInfo);

            //if (commitBranchDrawIndex.Count > 0)
            //{
            //    DrawIndex(historyCommits[0].Commit, commitBranchDrawIndex);
            //}

        }

        private void DrawCommitAtlas(CommitAtlasInfo atlasInfo)
        {
            if (atlasInfo != null)
            {
                ImGui.GetWindowDrawList().AddCircleFilled(atlasInfo.Point,ImGui.GetTextLineHeight()*0.25f, atlasInfo.Color);
                if (atlasInfo.Parents != null)
                {
                    foreach (var item in atlasInfo.Parents)
                    {
                        ImGui.GetWindowDrawList().AddLine(atlasInfo.Point, item.Point, item.Color);
                        DrawCommitAtlas(item);
                    }
                }
            }
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

                m_commitAltas = new Dictionary<Commit, CommitAtlasInfo>();
                m_drawAltasOffset = 0;
                if (m_cacheCommits != null && m_cacheCommits.Count() > 0)
                {
                    BuildCommitAltasLines(m_cacheCommits.First(), 0);

                    m_drawAltasOffset += ImGui.GetTextLineHeight();
                }
            }
            return m_cacheCommits;
        }


        private CommitAtlasInfo BuildCommitAltasLines(Commit commit,int atlasId)
        {
            if (commit==null || !m_cacheCommits.Contains(commit))
            {
                return null;
            }

            CommitAtlasInfo commitAtlasInfo ;
            if (!m_commitAltas.TryGetValue(commit, out commitAtlasInfo))
            {
                commitAtlasInfo = new CommitAtlasInfo();
                m_commitAltas.Add(commit, commitAtlasInfo);
            }
            commitAtlasInfo.SetAtlasId(atlasId);

            m_drawAltasOffset = Math.Max(m_drawAltasOffset, commitAtlasInfo.PointXOffset);

            if (commit.Parents != null)
            {
                int atlasIndex = 0;
                foreach (var item in commit.Parents)
                {
                    var parent = BuildCommitAltasLines(item, atlasId + atlasIndex);
                    commitAtlasInfo.SetParent(parent);
                    atlasIndex++;
                }
            }

            return commitAtlasInfo;
        }

    }



    public class CommitAtlasInfo
    {
        //public string Id;
        public Vector2 Point;
        public float PointXOffset;
        public uint Color;
        public List<int> AtlasIds { get; private set; }
        public List<CommitAtlasInfo> Parents { get; private set; }
        //public Commit Commit;

        public void SetAtlasId(int atlasId)
        {
            if (AtlasIds == null)
            {
                AtlasIds = new List<int>();
            }

            if (!AtlasIds.Contains(atlasId))
            {
                AtlasIds.Add(atlasId);
                AtlasIds.Sort();

                int minIndex = AtlasIds[0];
                Color = ImGui.ColorConvertFloat4ToU32(AppImGuiView.Colors[minIndex]);
                PointXOffset = ImGui.GetTextLineHeight() * minIndex;
            }
        }

        public void SetParent(CommitAtlasInfo parent)
        {
            if (Parents == null)
            {
                Parents = new List<CommitAtlasInfo>();
            }
            if (parent != null && !Parents.Contains(parent))
            {
                Parents.Add(parent);
            }
        }

    }

}

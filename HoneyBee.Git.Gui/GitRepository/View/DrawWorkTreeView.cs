using ImGuiNET;
using LibGit2Sharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;
using Wanderer.GitRepository.Common;
using static System.Net.WebRequestMethods;

namespace Wanderer
{
    public class DrawWorkTreeView
    {
        private SplitView m_horizontalSplitView = new SplitView(SplitView.SplitType.Horizontal);
        private SplitView m_verticalSplitView = new SplitView(SplitView.SplitType.Vertical);
        private HashSet<string> _selectStageFiles = new HashSet<string>();
        private HashSet<string> _selectUnstageFiles = new HashSet<string>();
        private HashSet<StatusEntry> m_newIndexAdded = new HashSet<StatusEntry>();
        private string m_submitMessage= "";

        private GitRepo m_gitRepo;

        private RepositoryStatus m_statuses;
        List<DiffText> m_diffTexts = new List<DiffText>();
        //private string m_diffContext;
        private float m_diffNumberWidth;

        public DrawWorkTreeView(GitRepo gitRepo)
        {
            m_gitRepo = gitRepo;
            UpdateStatus();
        }

        public void Draw()
        {
            //ImGui.ShowStyleEditor();
            ImGui.BeginChild("WorkTreeView_Content", ImGui.GetWindowSize() - new Vector2(0, 100));
            m_horizontalSplitView.Begin();
            DrawStageStatus();
            m_horizontalSplitView.Separate();
            DrawDiffStatus();
            m_horizontalSplitView.End();
            ImGui.EndChild();

            ImGui.BeginChild("WorkTreeView_Commit");
            //绘制提交
            DrawSubmit();
            ImGui.EndChild();
        }

        /// <summary>
        /// 缓存以及状态
        /// </summary>
        private void DrawStageStatus()
        {
            IEnumerable<StatusEntry> stageStatusEntries = null;
            m_newIndexAdded.Clear();
            if (m_statuses != null)
            {
                stageStatusEntries = m_statuses.Staged;
                if (m_statuses.Added != null)
                {
                    foreach (var item in m_statuses.Added)
                    {
                        if (m_gitRepo.CheckIndex(item.FilePath))
                        {
                            m_newIndexAdded.Add(item);
                        }
                    }
                }
            }

            m_verticalSplitView.Begin();
            DrawStageFilesStatus(stageStatusEntries);
            m_verticalSplitView.Separate();
            DrawUnstageFileStatus();
            m_verticalSplitView.End();
        }

        /// <summary>
        /// 绘制不同的状态
        /// </summary>
        private void DrawDiffStatus()
        {
            if (m_diffTexts!=null && m_diffTexts.Count>0)
            {
                foreach (var item in m_diffTexts)
                {
                    RenderDiffTextLine(item);
                }

                var min = ImGui.GetWindowPos()+ new Vector2(m_diffNumberWidth * 2, 0);
                var max = min + new Vector2(2, ImGui.GetWindowHeight());
                ImGui.GetWindowDrawList().AddRectFilled(min, max, ImGui.GetColorU32( ImGuiCol.Border));
            }
        }

        //private void RenderTextLine(string line)
        //{
        //    uint col = 0;
        //    if (line.StartsWith("+"))
        //    {
        //        col = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2235f, 0.3607f, 0.2431f, 1f));
        //    }
        //    else if (line.StartsWith("-"))
        //    {
        //        col = ImGui.ColorConvertFloat4ToU32(new Vector4(0.3725f, 0.2705f, 0.3019f, 1f));
        //    }
        //    if (col != 0)
        //    {
        //        //ImGui.GetBackgroundDrawList().AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), col);
        //        //var min = ImGui.GetItemRectMin();
        //        //var max = ImGui.GetItemRectMax();
        //        var min = ImGui.GetWindowPos() + ImGui.GetCursorPos() - new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY());
        //        var max = min + new Vector2(ImGui.GetWindowWidth(), ImGui.GetTextLineHeightWithSpacing());
        //        ImGui.GetWindowDrawList().AddRectFilled(min, max, col);
        //    }
        //    ImGui.Text(line);
        //}

        private void RenderDiffTextLine(DiffText line)
        {
            uint col = 0;
            if (line.Status == 1)
            {
                var styleColorsValue =ImGuiView.StyleColors == 0 ? 0.5f : 0.0f;
                col = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2235f, 0.3607f, 0.2431f, 1f)+Vector4.One* styleColorsValue);
            }
            else if (line.Status == 2)
            {
                var styleColorsValue = ImGuiView.StyleColors == 0 ? 0.5f : 0.0f;
                col = ImGui.ColorConvertFloat4ToU32(new Vector4(0.3725f, 0.2705f, 0.3019f, 1f)+ Vector4.One * styleColorsValue);
            }
            if (col != 0)
            {
                //ImGui.GetBackgroundDrawList().AddRectFilled(ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), col);
                //var min = ImGui.GetItemRectMin();
                //var max = ImGui.GetItemRectMax();
                var min = ImGui.GetWindowPos() + ImGui.GetCursorPos() - new Vector2(ImGui.GetScrollX(), ImGui.GetScrollY());
                var max = min + new Vector2(ImGui.GetWindowWidth(), ImGui.GetTextLineHeightWithSpacing());
                ImGui.GetWindowDrawList().AddRectFilled(min, max, col);
            }

            ImGui.SetCursorPosX(0);
            //ImGui.SetNextItemWidth(50);
            ImGui.Text(line.RemoveText);
            ImGui.SameLine();
            ImGui.SetCursorPosX(m_diffNumberWidth);
            //ImGui.SetNextItemWidth(50);
            ImGui.Text(line.AddText);
            ImGui.SameLine();
            ImGui.SetCursorPosX(m_diffNumberWidth*2+5);
            ImGui.Text(line.Text);
        }

        private void DrawStageFilesStatus(IEnumerable<StatusEntry> statuses)
        {
            if (ImGui.Button("Unstage All"))
            {
                m_gitRepo.Unstage();
                ClearSelectFiles();
                UpdateStatus();
            }
            ImGui.SameLine();
            if (ImGui.Button("Unstage Selected"))
            {
                m_gitRepo.Unstage(_selectStageFiles);
                ClearSelectFiles();
                UpdateStatus();
            }

            if (statuses != null)
            {
                foreach (var item in m_newIndexAdded)
                {
                    DrawStatusFile(item, _selectStageFiles);
                }
                foreach (var item in statuses)
                {
                    DrawStatusFile(item, _selectStageFiles);
                }
            }
        }

        private void DrawUnstageFileStatus()
        {
            if (ImGui.Button("Stage All"))
            {
                m_gitRepo.Stage();
                if (m_statuses.Added != null && m_statuses.Count() > 0)
                {
                    HashSet<string> addedFiles = new HashSet<string>();
                    foreach (var item in m_statuses.Added)
                    {
                        addedFiles.Add(item.FilePath);
                    }
                    m_gitRepo.AddFile(addedFiles);
                }
                ClearSelectFiles();
                UpdateStatus();
            }
            ImGui.SameLine();
            if (ImGui.Button("Stage Selected"))
            {
                m_gitRepo.Stage(_selectUnstageFiles);
                if (m_statuses.Added != null && m_statuses.Count() > 0)
                {
                    HashSet<string> addedFiles = new HashSet<string>();
                    foreach (var item in m_statuses.Added)
                    {
                        if (_selectUnstageFiles.Contains(item.FilePath))
                            addedFiles.Add(item.FilePath);
                    }
                    m_gitRepo.AddFile(addedFiles);
                }
                ClearSelectFiles();
                UpdateStatus();
            }
            ImGui.SameLine();
            if (ImGui.Button("Discard Selected"))
            {
                m_gitRepo.Restore(_selectUnstageFiles);
                ClearSelectFiles();
                UpdateStatus();
            }

            //files
            if (m_statuses != null)
            {
                foreach (var item in m_statuses)
                {
                    if (m_statuses.Staged.Contains(item))
                        continue;
                    //需要忽略NewIndex的
                    if (m_newIndexAdded.Contains(item))
                        continue;

                    DrawStatusFile(item, _selectUnstageFiles);
                }
            }
        }

        /// <summary>
        /// 提交模块
        /// </summary>
        private void DrawSubmit()
        {
            ImGui.InputTextMultiline("", ref m_submitMessage, 500, new Vector2(ImGui.GetWindowWidth(), 70));
            ImGui.Text($"{m_gitRepo.SignatureAuthor.Name}<{m_gitRepo.SignatureAuthor.Email}>");
            ImGui.SameLine();
            if (ImGui.Button("Commit"))
            {
                if (!string.IsNullOrEmpty(m_submitMessage))
                {
                    m_gitRepo.Commit(m_submitMessage);
                    UpdateStatus();
                }
                m_submitMessage = "";
            }
        }

        //绘制单独的文件
        private void DrawStatusFile(StatusEntry statusEntry, HashSet<string> selectFiles)
        {
            if (statusEntry == null || statusEntry.State == FileStatus.Ignored)
                return;

            //?
            string statusIcon = Icon.Get(Icon.Material_question_mark);
            switch (statusEntry.State)
            {
                case FileStatus.NewInIndex:
                case FileStatus.NewInWorkdir:
                    statusIcon = Icon.Get(Icon.Material_fiber_new);
                    break;
                case FileStatus.DeletedFromIndex:
                case FileStatus.DeletedFromWorkdir:
                    statusIcon = Icon.Get(Icon.Material_delete);
                    break;
                case FileStatus.RenamedInIndex:
                case FileStatus.RenamedInWorkdir:
                    statusIcon = Icon.Get(Icon.Material_edit_note);
                    break;
                case FileStatus.ModifiedInIndex:
                case FileStatus.ModifiedInWorkdir:
                    statusIcon = Icon.Get(Icon.Material_update);
                    break;
                case FileStatus.TypeChangeInIndex:
                case FileStatus.TypeChangeInWorkdir:
                    statusIcon = Icon.Get(Icon.Material_change_circle);
                    break;
                case FileStatus.Conflicted:
                    statusIcon = Icon.Get(Icon.Material_warning);
                    break;
                default:
                    break;
            }

            //checkbox 
            bool active = selectFiles.Contains(statusEntry.FilePath);
            if (ImGui.Checkbox($"{statusIcon} {statusEntry.FilePath}", ref active))
            {
                if (active)
                {
                    selectFiles.Add(statusEntry.FilePath);
                }
                else
                {
                    selectFiles.Remove(statusEntry.FilePath);
                }
                var diffContext = m_gitRepo.Diff.Compare<Patch>(new List<string>() { statusEntry.FilePath }, true).Content;
                BuildDiffTexts(diffContext);
            }
        }

        private void UpdateStatus()
        {
            m_statuses = m_gitRepo.RetrieveStatus;
        }

        private void ClearSelectFiles()
        {
            _selectStageFiles.Clear();
            _selectUnstageFiles.Clear();
        }

        private void BuildDiffTexts(string content)
        {
            m_diffNumberWidth = 0;
            m_diffTexts.Clear();
            if (string.IsNullOrEmpty(content))
                return;

            int removeIndex = -1;
            int addIndex = -1;

            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                DiffText diffText = new DiffText();
                diffText.Text = line;
                diffText.RemoveText = " ";
                diffText.AddText = " ";
                if (line.StartsWith("+++") || line.StartsWith("---"))
                { }
                else if (line.StartsWith("+"))
                {
                    if (addIndex >= 0)
                    {
                        diffText.Status = 1;
                        addIndex++;
                        //diffText.RemoveText = " ";
                        diffText.AddText = $"{(addIndex - 1)} ";
                        GetNumberItemWidth(diffText.AddText);
                    }
                }
                else if (line.StartsWith("-"))
                {
                    if (removeIndex >= 0)
                    {
                        diffText.Status = 2;
                        removeIndex++;

                        //diffText.AddText = " ";
                        diffText.RemoveText =$"{(removeIndex - 1)} ";
                        GetNumberItemWidth(diffText.RemoveText);
                    }
                }
                else if (line.StartsWith("@@"))
                {
                    var lineArgs = line.Split(' ');
                    var removeArgs = lineArgs[1].Split(',');
                    var addArgs = lineArgs[2].Split(',');

                    removeIndex = int.Parse(removeArgs[0])*-1;
                    addIndex = int.Parse(addArgs[0]);
                }

                if (diffText.Status == 0)
                {
                    if (!line.StartsWith("@@") && addIndex >= 0 && removeIndex >= 0)
                    {
                        addIndex++;
                        removeIndex++;
                        diffText.AddText = (addIndex - 1).ToString();
                        GetNumberItemWidth(diffText.AddText);
                        diffText.RemoveText = (removeIndex - 1).ToString();
                        GetNumberItemWidth(diffText.RemoveText);
                    }
                    else
                    {
                        //diffText.AddText = " ";
                        //diffText.RemoveText = " ";
                    }
                }


                m_diffTexts.Add(diffText);
            }
        }


        private void GetNumberItemWidth(string text)
        {
            var textSize = ImGui.CalcTextSize(text);
            if (textSize.X > m_diffNumberWidth)
            {
                m_diffNumberWidth = textSize.X;
            }
        }

        struct DiffText
        {
            public string Text;
            public int Status;
            public string RemoveText;
            public string AddText;
        }
    }
}

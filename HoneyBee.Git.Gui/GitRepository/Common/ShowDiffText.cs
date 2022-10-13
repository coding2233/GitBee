using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Wanderer.Common;

namespace Wanderer.GitRepository.Common
{
    public class ShowDiffText
    {
        private struct DiffText
        {
            public string Text;
            public int Status;
            public string RemoveText;
            public string AddText;
        }

        List<DiffText> m_diffTexts = new List<DiffText>();
        //private string m_diffContext;
        private float m_diffNumberWidth;

        public void Draw()
        {
            DrawDiffStatus();
        }

        public void BuildDiffTexts(string content)
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
                        diffText.RemoveText = $"{(removeIndex - 1)} ";
                        GetNumberItemWidth(diffText.RemoveText);
                    }
                }
                else if (line.StartsWith("@@"))
                {
                    var lineArgs = line.Split(' ');
                    var removeArgs = lineArgs[1].Split(',');
                    var addArgs = lineArgs[2].Split(',');

                    removeIndex = int.Parse(removeArgs[0]) * -1;
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


        /// <summary>
        /// 绘制不同的状态
        /// </summary>
        private void DrawDiffStatus()
        {
            if (m_diffTexts != null && m_diffTexts.Count > 0)
            {
                foreach (var item in m_diffTexts)
                {
                    RenderDiffTextLine(item);
                }

                var min = ImGui.GetWindowPos() + new Vector2(m_diffNumberWidth * 2, 0);
                var max = min + new Vector2(2, ImGui.GetWindowHeight());
                ImGui.GetWindowDrawList().AddRectFilled(min, max, ImGui.GetColorU32(ImGuiCol.Border));
            }
        }

        private void RenderDiffTextLine(DiffText line)
        {
            uint col = 0;
            if (line.Status == 1)
            {
                var styleColorsValue = ImGuiView.StyleColors == 0 ? 0.5f : 0.0f;
                col = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2235f, 0.3607f, 0.2431f, 1f) + Vector4.One * styleColorsValue);
            }
            else if (line.Status == 2)
            {
                var styleColorsValue = ImGuiView.StyleColors == 0 ? 0.5f : 0.0f;
                col = ImGui.ColorConvertFloat4ToU32(new Vector4(0.3725f, 0.2705f, 0.3019f, 1f) + Vector4.One * styleColorsValue);
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
            ImGui.SetCursorPosX(m_diffNumberWidth * 2 + 5);
            ImGui.Text(line.Text);
        }


       


        private void GetNumberItemWidth(string text)
        {
            var textSize = ImGui.CalcTextSize(text);
            if (textSize.X > m_diffNumberWidth)
            {
                m_diffNumberWidth = textSize.X;
            }
        }
    }
}

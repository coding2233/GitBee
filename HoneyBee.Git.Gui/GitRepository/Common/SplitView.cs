using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Wanderer
{
    public class SplitView
    {
        public enum SplitType
        {
            Horizontal,
            Vertical
        }

        SplitType m_splitType;
        private float m_defaultSplitWidth;
        private float m_spliteWidth;
        private int m_splitIndex = 0;
        private bool m_draging = false;
        private float m_dragPosition = 0;

        private float m_splitMin = 100;
        private float m_splitMax = 100;

        public SplitView(SplitType splitType=SplitType.Horizontal,float defaultSplitWidth = 0.5f)
        {
            m_splitType = splitType;
            m_defaultSplitWidth = defaultSplitWidth;

            //if (splitCount < 2)
            //{
            //    splitCount = 2;
            //}

            //_splitMin = 10;
            //_splitMax = (splitType==SplitType.Horizontal? ImGui.GetContentRegionAvail().X: ImGui.GetContentRegionAvail().Y)* max;

            //for (int i = 0; i < splitCount-1; i++)
            //{
            //    _splitWidth.Add(min);
            //}
            //ImGui.GetContentRegionAvail();
        }

        public void Begin()
        {
            m_splitIndex = 0;
            ImGui.BeginChild($"SplitView_Child_{m_splitIndex}", GetSplitPosition(ImGui.GetContentRegionAvail()), false);
        }

        public void End()
        {
            ImGui.EndChild();
        }

        public void Separate()
        {
            ImGui.EndChild();

            Vector2 min = ImGui.GetItemRectMin();
            Vector2 max = ImGui.GetItemRectMax();

            Vector2 hoverMin = min;
            Vector2 hoverMax = max;

            if (m_splitType == SplitType.Horizontal)
            {
                min.X = max.X+3.0f;
                max.X += 5.0f;

                hoverMin = min;
                hoverMax = max;
                hoverMin.X -= 2.0f;
                hoverMax.X += 2.0f;
            }
            else
            {
                min.Y = max.Y+1.0f;
                max.Y += 3.0f;

                hoverMin = min;
                hoverMax = max;
                hoverMin.Y -= 2.0f;
                hoverMax.Y += 2.0f;
            }

            if (m_splitType == SplitType.Horizontal)
                ImGui.SameLine();


            bool separatorHovered = true;
            if (m_draging)
            {
                var splitX = m_dragPosition + (m_splitType == SplitType.Horizontal ? ImGui.GetMouseDragDelta().X : ImGui.GetMouseDragDelta().Y);
                splitX = Math.Clamp(splitX, m_splitMin, m_splitMax);
                m_spliteWidth = splitX;

                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    m_draging = false;
                }

            }
            else if (!m_draging && ImGui.IsMouseHoveringRect(hoverMin, hoverMax))
            {
                if (ImGui.IsWindowFocused()&&ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    m_draging = true;
                    m_dragPosition = m_spliteWidth;
                }
            }
            else
            {
                separatorHovered = false;
            }

            ImGui.GetWindowDrawList().AddRectFilled(min, max, ImGui.GetColorU32(separatorHovered ? ImGuiCol.SeparatorHovered : ImGuiCol.Border));

            m_splitIndex++;
            ImGui.BeginChild($"SplitView BeginHorizontal_{m_splitIndex}", Vector2.Zero, false);
        }


        private Vector2 GetSplitPosition(Vector2 size)
        {
            bool isHorizontal = m_splitType == SplitType.Horizontal;
            Vector2 position = Vector2.Zero;
            if (m_spliteWidth == 0.0f)
            {
                if (m_defaultSplitWidth > 1)
                {
                    m_spliteWidth = m_defaultSplitWidth;
                }
                else
                {
                    m_spliteWidth = isHorizontal ? size.X * m_defaultSplitWidth : size.Y * m_defaultSplitWidth;
                }
            }

            if (isHorizontal)
            {
                position.X = m_spliteWidth;
                m_splitMax = size.X - m_splitMin;
            }
            else
            {
                position.Y = m_spliteWidth;
                m_splitMax = size.Y - m_splitMin;
            }
            return position;
        }

    }
}

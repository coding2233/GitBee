using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace HoneyBee.Diff.Gui
{
    public class SplitView
    {
        public enum SplitType
        {
            Horizontal,
            Vertical
        }

        SplitType _splitType;
        private List<float> _splitWidth = new List<float>();
        private int _splitIndex = 0;
        private bool _draging = false;
        private float _dragPosition = 0;
        private int _dragIndex = 0;

        private float _splitMin = 100;
        private float _splitMax = 100;

        public SplitView(SplitType splitType=SplitType.Horizontal,int splitCount =2,float min=100,float max = 0.5f)
        {
            _splitType = splitType;

            if (splitCount < 2)
            {
                splitCount = 2;
            }

            _splitMin = 10;
            _splitMax = (splitType==SplitType.Horizontal? ImGui.GetContentRegionAvail().X: ImGui.GetContentRegionAvail().Y)* max;

            for (int i = 0; i < splitCount-1; i++)
            {
                _splitWidth.Add(min);
            }
            //ImGui.GetContentRegionAvail();
        }

        public void Begin()
        {
            _splitIndex = 0;
            ImGui.BeginChild($"SplitView_Child_{_splitIndex}", GetSplitPosition(), false);
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

            if (_splitType == SplitType.Horizontal)
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

            if (_splitType == SplitType.Horizontal)
                ImGui.SameLine();


            bool separatorHovered = true;
            if (_draging)
            {
                if (_splitIndex == _dragIndex)
                {
                    var splitX = _dragPosition + (_splitType == SplitType.Horizontal?ImGui.GetMouseDragDelta().X: ImGui.GetMouseDragDelta().Y);
                    splitX = Math.Clamp(splitX, _splitMin, _splitMax);
                    _splitWidth[_splitIndex] = splitX;
                }

                if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                {
                    _draging = false;
                }

            }
            else if (!_draging && ImGui.IsMouseHoveringRect(hoverMin, hoverMax))
            {
                if (ImGui.IsWindowFocused()&&ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    _draging = true;
                    _dragPosition = _splitWidth[_splitIndex];
                    _dragIndex = _splitIndex;
                }
            }
            else
            {
                separatorHovered = false;
            }

            ImGui.GetWindowDrawList().AddRectFilled(min, max, ImGui.GetColorU32(separatorHovered ? ImGuiCol.SeparatorHovered : ImGuiCol.Border));

            _splitIndex++;
            ImGui.BeginChild($"SplitView BeginHorizontal_{_splitIndex}", GetSplitPosition(), false);
        }


        private Vector2 GetSplitPosition()
        {
            Vector2 position = Vector2.Zero;
            if (_splitIndex < _splitWidth.Count)
            {
                if (_splitType == SplitType.Horizontal)
                    position.X = _splitWidth[_splitIndex];
                else
                    position.Y = _splitWidth[_splitIndex];
            }
            //_splitIndex++;
            return position;
        }

    }
}

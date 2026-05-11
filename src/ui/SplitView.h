#pragma once

#include <imgui.h>
#include <algorithm>

class SplitView
{
public:
    enum class Type { Horizontal, Vertical };

    SplitView(Type type = Type::Horizontal, float defaultSplit = 0.5f, float minSize = 60.0f)
        : m_type(type)
        , m_defaultSplit(defaultSplit)
        , m_minSize(minSize)
    {
    }

    void Begin()
    {
        m_splitIndex = 0;
        ImGui::BeginChild(m_ChildId(0), GetInitialSize(), false);
    }

    void Separate()
    {
        ImGui::EndChild();

        ImVec2 min = ImGui::GetItemRectMin();
        ImVec2 max = ImGui::GetItemRectMax();

        ImVec2 hoverMin = min, hoverMax = max;
        if (m_type == Type::Horizontal)
        {
            min.x = max.x + 2.0f;
            max.x = min.x + 4.0f;
            hoverMin.x = min.x - 2.0f;
            hoverMax.x = max.x + 2.0f;
        }
        else
        {
            min.y = max.y + 2.0f;
            max.y = min.y + 4.0f;
            hoverMin.y = min.y - 2.0f;
            hoverMax.y = max.y + 2.0f;
        }

        if (m_type == Type::Horizontal)
            ImGui::SameLine();

        if (m_dragging)
        {
            float delta = (m_type == Type::Horizontal) ? ImGui::GetMouseDragDelta().x : ImGui::GetMouseDragDelta().y;
            float newSplit = m_dragStart + delta;
            newSplit = std::clamp(newSplit, m_minSize, m_maxLimit - m_minSize);
            m_splitWidth = newSplit;

            if (ImGui::IsMouseReleased(ImGuiMouseButton_Left))
                m_dragging = false;
        }

        bool hovered = ImGui::IsMouseHoveringRect(hoverMin, hoverMax);
        if (!m_dragging && hovered && ImGui::IsWindowFocused() && ImGui::IsMouseDown(ImGuiMouseButton_Left))
        {
            m_dragging = true;
            m_dragStart = m_splitWidth;
        }

        ImGui::GetWindowDrawList()->AddRectFilled(min, max,
            ImGui::GetColorU32(hovered || m_dragging ? ImGuiCol_SeparatorHovered : ImGuiCol_Border));

        if (hovered || m_dragging)
            ImGui::SetMouseCursor(m_type == Type::Horizontal ? ImGuiMouseCursor_ResizeEW : ImGuiMouseCursor_ResizeNS);

        m_splitIndex++;
        ImGui::BeginChild(m_ChildId(m_splitIndex), ImVec2(0, 0), false);
    }

    void End()
    {
        ImGui::EndChild();
    }

private:
    Type m_type;
    float m_defaultSplit;
    float m_minSize;
    int m_splitIndex = 0;
    float m_splitWidth = 0.0f;
    float m_dragStart = 0.0f;
    float m_maxLimit = 0.0f;
    bool m_dragging = false;

    ImVec2 GetInitialSize()
    {
        ImVec2 avail = ImGui::GetContentRegionAvail();
        float maxVal = (m_type == Type::Horizontal) ? avail.x : avail.y;
        if (m_splitWidth == 0.0f)
        {
            if (m_defaultSplit >= 1.0f)
                m_splitWidth = m_defaultSplit;
            else
                m_splitWidth = maxVal * m_defaultSplit;
        }
        m_maxLimit = maxVal;

        ImVec2 size(0, 0);
        if (m_type == Type::Horizontal)
            size.x = m_splitWidth;
        else
            size.y = m_splitWidth;
        return size;
    }

    const char* m_ChildId(int index)
    {
        static char buf[64];
        snprintf(buf, sizeof(buf), "##SplitView_%p_%d", this, index);
        return buf;
    }
};

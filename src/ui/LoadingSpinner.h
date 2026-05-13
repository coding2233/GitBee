#pragma once
#include <imgui.h>
#include <math.h>
#ifndef IM_PI
#define IM_PI 3.14159265358979323846f
#endif

inline void LoadingSpinner(float radius = 10.0f, float thickness = 3.0f)
{
    ImVec2 pos = ImGui::GetCursorScreenPos();
    float sz = (radius + thickness) * 2.0f;
    ImGui::Dummy(ImVec2(sz, sz));

    ImDrawList* dl = ImGui::GetWindowDrawList();
    float time = ImGui::GetTime();

    ImVec2 center(pos.x + sz * 0.5f, pos.y + sz * 0.5f);
    int numSegments = 12;

    for (int i = 0; i < numSegments; i++)
    {
        float angle = time * 3.0f + (float)i / numSegments * IM_PI * 2.0f;
        float frac = (float)i / numSegments;
        float alpha = 1.0f - frac;
        ImU32 color = IM_COL32(120, 180, 255, (int)(alpha * 255));
        ImVec2 dotPos(
            center.x + cosf(angle) * radius,
            center.y + sinf(angle) * radius
        );
        dl->AddCircleFilled(dotPos, thickness * (0.3f + 0.7f * (1.0f - frac)), color, 8);
    }
}

inline void LoadingSpinnerWithText(const char* text, float radius = 10.0f)
{
    ImGui::BeginGroup();
    LoadingSpinner(radius);
    ImGui::SameLine();
    ImGui::TextUnformatted(text);
    ImGui::EndGroup();
}

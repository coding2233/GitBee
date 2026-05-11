#include "home_view.h"
#include <imgui.h>

void HomeView::Render()
{
    auto avail = ImGui::GetContentRegionAvail();
    float centerX = avail.x * 0.5f;
    float centerY = avail.y * 0.35f;

    ImGui::SetCursorPos(ImVec2(centerX - 200, centerY));

    ImGui::BeginChild("##home", ImVec2(400, 350), true);

    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.6f, 0.2f, 1.0f));
    ImGui::SetCursorPosX(160);
    ImGui::Text("GitBee");
    ImGui::PopStyleColor();

    ImGui::Spacing();
    ImGui::Spacing();

    ImGui::TextColored(ImVec4(0.7f, 0.7f, 0.7f, 1.0f),
        "A Lightweight Git Interface Management Tool");
    ImGui::Spacing();

    RenderQuickStart();
    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();
    RenderRecentRepos();

    ImGui::EndChild();
}

void HomeView::RenderQuickStart()
{
    ImGui::TextUnformatted("Quick Start:");
    ImGui::Spacing();

    ImGui::Indent(16);
    if (ImGui::Button("Open Repository...", ImVec2(200, 30)))
    {
        if (OnOpenRepository) OnOpenRepository();
    }

    ImGui::Spacing();
    ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f),
        "Or drag & drop a repository folder here.");
    ImGui::Unindent(16);
}

void HomeView::RenderRecentRepos()
{
    ImGui::TextUnformatted("Recent Repositories:");

    if (m_recentRepos.empty())
    {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f),
            "  No recent repositories");
        return;
    }

    ImGui::Indent(16);
    for (auto& repo : m_recentRepos)
    {
        ImGui::PushID(repo.path.c_str());
        if (ImGui::Selectable(repo.name.c_str(), false))
        {
            if (OnOpenRecent) OnOpenRecent(repo.path);
        }
        if (ImGui::IsItemHovered())
            ImGui::SetTooltip("%s", repo.path.c_str());
        ImGui::PopID();
    }
    ImGui::Unindent(16);
}

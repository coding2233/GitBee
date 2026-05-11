#include "status_panel.h"
#include "../gitcore/git_repository.h"
#include <imgui.h>

void StatusPanel::SetRepository(std::shared_ptr<GitRepository> repo)
{
    m_repository = repo;
    Refresh();
}

void StatusPanel::Refresh()
{
    if (m_repository)
        m_status = m_repository->GetStatus();
}

void StatusPanel::RenderFileEntry(const GitFileEntry& entry, const char* icon, ImU32 color)
{
    ImGui::TableNextRow();
    ImGui::TableNextColumn();

    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(
        ((color >> IM_COL32_R_SHIFT) & 0xFF) / 255.0f,
        ((color >> IM_COL32_G_SHIFT) & 0xFF) / 255.0f,
        ((color >> IM_COL32_B_SHIFT) & 0xFF) / 255.0f,
        1.0f));
    ImGui::TextUnformatted(icon);
    ImGui::PopStyleColor();

    ImGui::TableNextColumn();
    const char* statusText = "";
    ImU32 statusColor = color;
    switch (entry.status)
    {
        case GitFileStatus::Modified:
        case GitFileStatus::StagedModified: statusText = "M"; break;
        case GitFileStatus::Added:
        case GitFileStatus::StagedAdded: statusText = "A"; break;
        case GitFileStatus::Deleted:
        case GitFileStatus::StagedDeleted: statusText = "D"; break;
        case GitFileStatus::Renamed: statusText = "R"; break;
        case GitFileStatus::Untracked: statusText = "?"; break;
        case GitFileStatus::Unmerged: statusText = "!"; statusColor = IM_COL32(255, 0, 255, 255); break;
        default: statusText = " "; break;
    }
    ImGui::TextColored(ImVec4(
        ((statusColor >> IM_COL32_R_SHIFT) & 0xFF) / 255.0f,
        ((statusColor >> IM_COL32_G_SHIFT) & 0xFF) / 255.0f,
        ((statusColor >> IM_COL32_B_SHIFT) & 0xFF) / 255.0f,
        1.0f), "%s", statusText);

    ImGui::TableNextColumn();
    ImGui::TextUnformatted(entry.filename.c_str());
    if (!entry.oldFilename.empty())
    {
        ImGui::SameLine();
        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.5f, 0.5f, 0.5f, 1.0f));
        ImGui::TextUnformatted((" <- " + entry.oldFilename).c_str());
        ImGui::PopStyleColor();
    }
}

void StatusPanel::RenderFileList(const std::vector<GitFileEntry>& files, const char* title, const char* icon, ImU32 color)
{
    if (files.empty()) return;

    ImGui::Separator();
    ImGui::Text("%s %s (%zu)", icon, title, files.size());

    if (ImGui::BeginTable("##filelist", 3,
        ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_NoPadInnerX))
    {
        ImGui::TableSetupColumn("##icon", ImGuiTableColumnFlags_WidthFixed, 20.0f);
        ImGui::TableSetupColumn("##status", ImGuiTableColumnFlags_WidthFixed, 20.0f);
        ImGui::TableSetupColumn("##name", ImGuiTableColumnFlags_WidthStretch);

        for (const auto& entry : files)
            RenderFileEntry(entry, icon, color);

        ImGui::EndTable();
    }
}

void StatusPanel::RenderBranchInfo()
{
    std::string label = "Branch: " + m_status.currentBranch;
    ImGui::TextUnformatted(label.c_str());

    if (!m_status.upstreamBranch.empty())
    {
        std::string upstream = "Upstream: " + m_status.upstreamBranch;
        ImGui::TextUnformatted(upstream.c_str());

        if (m_status.aheadCount > 0 || m_status.behindCount > 0)
            RenderAheadBehind();
    }

    if (m_status.hasMergeConflict)
        ImGui::TextColored(ImVec4(1.0f, 0.0f, 1.0f, 1.0f), "! Merge Conflict Detected");
}

void StatusPanel::RenderAheadBehind()
{
    if (m_status.aheadCount > 0)
    {
        ImGui::SameLine();
        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.3f, 0.8f, 0.3f, 1.0f));
        ImGui::Text(" +%d", m_status.aheadCount);
        ImGui::PopStyleColor();
    }
    if (m_status.behindCount > 0)
    {
        ImGui::SameLine();
        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.3f, 0.3f, 1.0f));
        ImGui::Text(" -%d", m_status.behindCount);
        ImGui::PopStyleColor();
    }
}

void StatusPanel::Render()
{
    if (!m_repository) return;

    if (ImGui::BeginChild("##status_content", ImVec2(0, 0), ImGuiChildFlags_None))
    {
        ImGui::TextUnformatted(("Repository: " + m_repository->GetRootPath()).c_str());
        RenderBranchInfo();

        RenderFileList(m_status.stagedFiles, "Staged Changes", "+", IM_COL32(0, 200, 0, 255));
        RenderFileList(m_status.unstagedFiles, "Unstaged Changes", "~", IM_COL32(200, 200, 0, 255));
        RenderFileList(m_status.untrackedFiles, "Untracked Files", "?", IM_COL32(128, 128, 128, 255));

        if (m_status.stagedFiles.empty() && m_status.unstagedFiles.empty() && m_status.untrackedFiles.empty())
        {
            ImGui::Separator();
            ImGui::TextColored(ImVec4(0.5f, 0.9f, 0.5f, 1.0f), "Working tree clean");
        }
    }
    ImGui::EndChild();
}

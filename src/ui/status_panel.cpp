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
    if (m_repository) {
        m_status = m_repository->GetStatus();
    }
}

void StatusPanel::RenderFileEntry(const GitFileEntry& entry, const char* icon, float r, float g, float b)
{
    ImGui::TableNextRow();
    ImGui::TableNextColumn();
    ImGui::TextColored(ImVec4(r, g, b, 1.0f), "%s", icon);
    ImGui::TableNextColumn();
    ImGui::TextColored(ImVec4(r, g, b, 1.0f), "%s", entry.filename.c_str());
    if (!entry.oldFilename.empty()) {
        ImGui::SameLine();
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), " <- %s", entry.oldFilename.c_str());
    }
}

void StatusPanel::RenderFileList(
    const std::vector<GitFileEntry>& files,
    const char* title,
    const char* icon,
    float r, float g, float b)
{
    if (files.empty()) return;

    ImGui::Separator();
    ImGui::Text("%s %s (%zu)", icon, title, files.size());

    if (ImGui::BeginTable("##filelist", 2,
        ImGuiTableFlags_SizingFixedFit | ImGuiTableFlags_NoPadInnerX))
    {
        ImGui::TableSetupColumn("##icon", ImGuiTableColumnFlags_WidthFixed, 20.0f);
        ImGui::TableSetupColumn("##name", ImGuiTableColumnFlags_WidthStretch);

        for (const auto& entry : files) {
            RenderFileEntry(entry, icon, r, g, b);
        }
        ImGui::EndTable();
    }
}

void StatusPanel::Render()
{
    if (!m_repository) return;

    if (ImGui::BeginChild("##status_content", ImVec2(0, 0), ImGuiChildFlags_None))
    {
        std::string pathLabel = "Repository: " + m_repository->GetRootPath();
        ImGui::TextUnformatted(pathLabel.c_str());

        std::string branchLabel = "Branch: " + m_status.currentBranch;
        ImGui::TextUnformatted(branchLabel.c_str());

        if (!m_status.upstreamBranch.empty()) {
            std::string upstream = "Upstream: " + m_status.upstreamBranch;
            if (m_status.aheadCount > 0 || m_status.behindCount > 0) {
                upstream += " (ahead " + std::to_string(m_status.aheadCount)
                    + ", behind " + std::to_string(m_status.behindCount) + ")";
            }
            ImGui::TextUnformatted(upstream.c_str());
        }

        if (m_status.hasMergeConflict) {
            ImGui::TextColored(ImVec4(1.0f, 0.0f, 1.0f, 1.0f), "! Merge Conflict Detected");
        }

        RenderFileList(m_status.stagedFiles, "Staged Changes", "+", 0.0f, 1.0f, 0.0f);
        RenderFileList(m_status.unstagedFiles, "Unstaged Changes", "~", 1.0f, 1.0f, 0.0f);
        RenderFileList(m_status.untrackedFiles, "Untracked Files", "?", 0.5f, 0.5f, 0.5f);

        if (m_status.stagedFiles.empty() && m_status.unstagedFiles.empty()
            && m_status.untrackedFiles.empty()) {
            ImGui::Separator();
            ImGui::TextColored(ImVec4(0.5f, 0.9f, 0.5f, 1.0f), "Working tree clean");
        }
    }
    ImGui::EndChild();
}

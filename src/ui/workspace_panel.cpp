#include "workspace_panel.h"
#include "../gitcore/git_repository.h"
#include <imgui.h>
#include <misc/cpp/imgui_stdlib.h>
#include <algorithm>

void WorkspacePanel::SetRepository(std::shared_ptr<GitRepository> repo)
{
    m_repository = std::move(repo);
    m_selectedStagedPaths.clear();
    m_selectedUnstagedPaths.clear();
    Refresh();
}

void WorkspacePanel::Refresh()
{
    if (m_repository)
    {
        m_updating = true;
        m_status = m_repository->GetStatus();
        m_updating = false;
    }
}

void WorkspacePanel::Render()
{
    if (!m_repository) return;

    ImGui::BeginChild("##workspace_content", ImVec2(0, ImGui::GetContentRegionAvail().y - 90), true);

    if (m_updating)
    {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "Loading...");
        ImGui::EndChild();
        return;
    }

    m_vSplit.Begin();
    RenderStagedArea();
    m_vSplit.Separate();
    RenderUnstagedArea();
    m_vSplit.End();

    ImGui::EndChild();

    RenderCommitArea();
}

void WorkspacePanel::RenderStagedArea()
{
    ImGui::TextColored(ImVec4(0.3f, 0.8f, 0.3f, 1.0f), "Staged Changes");
    ImGui::Separator();

    if (ImGui::Button("Unstage All"))
    {
        m_repository->Unstage();
        Refresh();
    }
    ImGui::SameLine();
    if (!m_selectedStagedPaths.empty())
    {
        if (ImGui::Button("Unstage Selected"))
        {
            std::vector<std::string> files(m_selectedStagedPaths.begin(), m_selectedStagedPaths.end());
            m_repository->Unstage(files);
            m_selectedStagedPaths.clear();
            Refresh();
        }
    }

    ImGui::BeginChild("##staged_list", ImVec2(0, 0), true);
    if (m_status.stagedFiles.empty())
    {
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "No staged changes");
    }
    for (auto& f : m_status.stagedFiles)
    {
        RenderFileRow(f.filename, f.oldFilename, f.status, true);
    }
    ImGui::EndChild();
}

void WorkspacePanel::RenderUnstagedArea()
{
    ImGui::TextColored(ImVec4(1.0f, 1.0f, 0.3f, 1.0f), "Unstaged Changes");
    ImGui::Separator();

    if (ImGui::Button("Stage All"))
    {
        m_repository->Stage();
        Refresh();
    }
    ImGui::SameLine();
    if (!m_selectedUnstagedPaths.empty())
    {
        if (ImGui::Button("Stage Selected"))
        {
            std::vector<std::string> files(m_selectedUnstagedPaths.begin(), m_selectedUnstagedPaths.end());
            m_repository->Stage(files);
            m_selectedUnstagedPaths.clear();
            Refresh();
        }
        ImGui::SameLine();
        if (ImGui::Button("Discard Selected"))
        {
            std::vector<std::string> files(m_selectedUnstagedPaths.begin(), m_selectedUnstagedPaths.end());
            m_repository->Discard(files);
            m_selectedUnstagedPaths.clear();
            Refresh();
        }
    }

    ImGui::BeginChild("##unstaged_list", ImVec2(0, 0), true);

    std::vector<GitFileEntry> allUnstaged = m_status.unstagedFiles;
    allUnstaged.insert(allUnstaged.end(), m_status.untrackedFiles.begin(), m_status.untrackedFiles.end());

    if (allUnstaged.empty())
    {
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "No unstaged changes");
    }
    for (auto& f : allUnstaged)
    {
        RenderFileRow(f.filename, f.oldFilename, f.status, false);
    }
    ImGui::EndChild();
}

void WorkspacePanel::RenderFileRow(const std::string& path, const std::string& oldPath, GitFileStatus status, bool isStaged)
{
    ImGui::PushID(path.c_str());

    auto& selSet = isStaged ? m_selectedStagedPaths : m_selectedUnstagedPaths;
    bool selected = selSet.count(path) > 0;

    // Status indicator
    const char* statusIcon = " ";
    ImU32 statusColor = IM_COL32(128, 128, 128, 255);
    switch (status)
    {
        case GitFileStatus::Modified:
        case GitFileStatus::StagedModified:
            statusIcon = "M";
            statusColor = IM_COL32(200, 200, 0, 255);
            break;
        case GitFileStatus::Added:
        case GitFileStatus::StagedAdded:
        case GitFileStatus::Untracked:
            statusIcon = "A";
            statusColor = IM_COL32(0, 180, 0, 255);
            break;
        case GitFileStatus::Deleted:
        case GitFileStatus::StagedDeleted:
            statusIcon = "D";
            statusColor = IM_COL32(200, 60, 60, 255);
            break;
        case GitFileStatus::Renamed:
            statusIcon = "R";
            statusColor = IM_COL32(100, 200, 255, 255);
            break;
        case GitFileStatus::Unmerged:
            statusIcon = "!";
            statusColor = IM_COL32(255, 0, 255, 255);
            break;
        default: break;
    }

    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(
        ((statusColor >> IM_COL32_R_SHIFT) & 0xFF) / 255.0f,
        ((statusColor >> IM_COL32_G_SHIFT) & 0xFF) / 255.0f,
        ((statusColor >> IM_COL32_B_SHIFT) & 0xFF) / 255.0f,
        1.0f));

    ImGui::Text("[%s]", statusIcon);
    ImGui::SameLine();
    ImGui::PopStyleColor();

    if (ImGui::Selectable(path.c_str(), selected))
    {
        if (ImGui::GetIO().KeyCtrl)
        {
            if (selected)
                selSet.erase(path);
            else
                selSet.insert(path);
        }
        else
        {
            selSet.clear();
            selSet.insert(path);
        }
    }

    if (!oldPath.empty())
    {
        ImGui::SameLine();
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), " <- %s", oldPath.c_str());
    }

    ImGui::PopID();
}

void WorkspacePanel::RenderCommitArea()
{
    ImGui::Separator();
    ImGui::PushItemWidth(ImGui::GetContentRegionAvail().x - 100);

    ImGui::InputTextMultiline("##commit_msg", &m_commitMessage,
        ImVec2(ImGui::GetContentRegionAvail().x - 10, 50));

    ImGui::PopItemWidth();

    if (m_repository)
    {
        auto sig = m_repository->GetSignature();
        ImGui::Text("%s <%s>", sig.name.c_str(), sig.email.c_str());
    }

    ImGui::SameLine();
    ImGui::SetCursorPosX(ImGui::GetWindowWidth() - 100);

    bool canCommit = !m_commitMessage.empty() &&
        (!m_status.stagedFiles.empty() || !m_status.unstagedFiles.empty());

    if (!canCommit) { ImGui::PushItemFlag(ImGuiItemFlags_Disabled, true); ImGui::PushStyleVar(ImGuiStyleVar_Alpha, 0.5f); }
    if (ImGui::Button("Commit", ImVec2(90, 40)))
    {
        // If nothing staged, stage all first
        if (m_status.stagedFiles.empty() && !m_status.unstagedFiles.empty())
            m_repository->Stage();
        m_repository->Commit(m_commitMessage);
        m_commitMessage.clear();
        Refresh();
    }
    if (!canCommit) { ImGui::PopStyleVar(); ImGui::PopItemFlag(); }
}

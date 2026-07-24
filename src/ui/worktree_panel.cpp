#include "worktree_panel.h"
#include "LoadingSpinner.h"
#include "../gitcore/git_repository.h"
#include <imgui.h>
#include <filesystem>
#include <algorithm>

void WorktreePanel::SetRepository(std::shared_ptr<GitRepository> repo)
{
    m_repository = std::move(repo);
    m_loaded = false;
    m_worktrees.clear();
    m_branchNames.clear();
    m_branchesDirty = true;
    Refresh();
}

void WorktreePanel::Refresh()
{
    m_loaded = false;
    m_branchesDirty = true;
}

void WorktreePanel::LoadWorktrees()
{
    if (m_loading || !m_repository) return;
    m_loading = true;

    auto repo = m_repository;
    m_loadThread = std::thread([this, repo]() {
        auto wts = repo->GetWorktrees();
        std::vector<WorktreeEntry> entries;
        for (auto& wt : wts) {
            entries.push_back({std::move(wt), true});
        }
        {
            std::lock_guard<std::mutex> lock(m_mutex);
            m_pendingWorktrees = std::move(entries);
        }
        m_loading = false;
    });
    m_loadThread.detach();
}

void WorktreePanel::StartRefreshBranches()
{
    if (!m_repository) return;
    if (!m_branchesDirty) return;

    m_branchNames.clear();
    auto branches = m_repository->GetBranches();
    for (auto& b : branches) {
        if (!b.isHead)
            m_branchNames.push_back(b.name);
    }
    // Put HEAD (current) branch at index 0
    for (auto& b : branches) {
        if (b.isHead) {
            m_branchNames.insert(m_branchNames.begin(), b.name);
            break;
        }
    }
    m_branchesDirty = false;
}

void WorktreePanel::Render()
{
    if (!m_repository)
    {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "No repository opened");
        return;
    }

    // Process async load results
    if (!m_loading && !m_loaded) {
        if (m_loadThread.joinable())
            m_loadThread.join();
        std::lock_guard<std::mutex> lock(m_mutex);
        if (!m_pendingWorktrees.empty()) {
            m_worktrees = std::move(m_pendingWorktrees);
            m_pendingWorktrees.clear();
            m_loaded = true;
            m_branchesDirty = true;
        } else if (m_loaded) {
            m_pendingWorktrees.clear();
            m_branchesDirty = true;
        } else {
            LoadWorktrees();
        }
    }

    ImGui::BeginChild("##worktree_content", ImVec2(0, 0), true);

    // --- Header ---
    ImGui::TextColored(ImVec4(0.8f, 0.6f, 0.2f, 1.0f), "Git Worktrees");
    ImGui::SameLine(ImGui::GetWindowWidth() - 160);

    // Toolbar buttons
    if (ImGui::SmallButton("+ Add")) {
        m_showAddForm = !m_showAddForm;
        StartRefreshBranches();
        if (m_showAddForm) {
            m_newPathBuf[0] = '\0';
            m_selectedBranch = 0;
            m_detachHead = false;
            m_baseCommitBuf[0] = '\0';
        }
    }
    ImGui::SameLine();
    if (ImGui::SmallButton("Prune")) {
        if (m_repository->PruneWorktrees()) {
            Refresh();
        }
    }
    if (ImGui::IsItemHovered())
        ImGui::SetTooltip("Remove stale worktree entries");

    ImGui::SameLine();
    if (ImGui::SmallButton("Refresh"))
        Refresh();

    ImGui::Separator();

    // --- Add Form ---
    if (m_showAddForm)
        RenderAddForm();

    // --- Loading ---
    if (!m_loaded || m_loading)
    {
        LoadingSpinnerWithText("Loading worktrees...");
        ImGui::EndChild();
        return;
    }

    if (m_worktrees.empty())
    {
        ImGui::SetCursorPosY(ImGui::GetCursorPosY() + 40);
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "  No linked worktrees.");
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f),
            "  Use 'Add' to check out a branch in a separate directory.");
        ImGui::EndChild();
        return;
    }

    // --- Table ---
    if (ImGui::BeginTable("##worktree_table", 5,
        ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders | ImGuiTableFlags_ScrollY,
        ImVec2(0, 0)))
    {
        ImGui::TableSetupColumn("", ImGuiTableColumnFlags_WidthFixed, 22);
        ImGui::TableSetupColumn("Branch", ImGuiTableColumnFlags_WidthFixed, 160);
        ImGui::TableSetupColumn("HEAD", ImGuiTableColumnFlags_WidthFixed, 90);
        ImGui::TableSetupColumn("Path", ImGuiTableColumnFlags_WidthStretch);
        ImGui::TableSetupColumn("", ImGuiTableColumnFlags_WidthFixed, 60);
        ImGui::TableHeadersRow();

        for (int i = 0; i < (int)m_worktrees.size(); i++)
        {
            auto& entry = m_worktrees[i];
            ImGui::TableNextRow();
            ImGui::PushID(i);

            // Col 0: Indicator
            ImGui::TableNextColumn();
            ImGui::PushStyleColor(ImGuiCol_Text,
                entry.info.isMain ? ImVec4(0.3f, 0.8f, 0.3f, 1.0f) : ImVec4(0.5f, 0.5f, 0.5f, 1.0f));
            ImGui::TextUnformatted(entry.info.isMain ? "◆" : "○");
            ImGui::PopStyleColor();
            if (ImGui::IsItemHovered())
                ImGui::SetTooltip(entry.info.isMain ? "Main worktree" : "Linked worktree");

            // Col 1: Branch
            ImGui::TableNextColumn();
            if (entry.info.isDetached) {
                ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.9f, 0.5f, 0.3f, 1.0f));
                ImGui::TextUnformatted("[detached]");
                ImGui::PopStyleColor();
            } else if (entry.info.isBare) {
                ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "(bare)");
            } else {
                ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.4f, 0.7f, 1.0f, 1.0f));
                ImGui::TextUnformatted(entry.info.branch.c_str());
                ImGui::PopStyleColor();
            }

            // Col 2: HEAD hash (short)
            ImGui::TableNextColumn();
            std::string shortSha = entry.info.sha.substr(0, std::min(entry.info.sha.size(), size_t(7)));
            ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "%s", shortSha.c_str());

            // Col 3: Path
            ImGui::TableNextColumn();
            ImGui::TextUnformatted(entry.info.path.c_str());

            // Col 4: Actions
            ImGui::TableNextColumn();
            if (!entry.info.isMain) {
                if (ImGui::SmallButton("Open")) {
                    if (OnOpenWorktree) OnOpenWorktree(entry.info.path);
                }
                ImGui::SameLine();
                ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(1.0f, 0.3f, 0.3f, 1.0f));
                if (ImGui::SmallButton("X")) {
                    // Confirm removal
                    bool forceRemove = entry.info.isLocked;
                    if (m_repository->RemoveWorktree(entry.info.path, forceRemove)) {
                        Refresh();
                    } else {
                        // Try with force
                        if (m_repository->RemoveWorktree(entry.info.path, true)) {
                            Refresh();
                        }
                    }
                }
                ImGui::PopStyleColor();
                if (ImGui::IsItemHovered())
                    ImGui::SetTooltip("Remove this worktree");
            }

            ImGui::PopID();
        }

        ImGui::EndTable();
    }

    ImGui::EndChild();
}

void WorktreePanel::RenderAddForm()
{
    ImGui::PushStyleColor(ImGuiCol_ChildBg, ImVec4(0.15f, 0.20f, 0.15f, 1.0f));
    ImGui::BeginChild("##add_worktree_form", ImVec2(0, ImGui::GetFrameHeight() * 7 + 24), true);

    ImGui::TextColored(ImVec4(0.5f, 0.9f, 0.5f, 1.0f), "New Worktree");

    // Path
    ImGui::TextUnformatted("Directory:");
    ImGui::SameLine(80);
    ImGui::PushItemWidth(ImGui::GetWindowWidth() - 180);
    ImGui::InputTextWithHint("##new_path", "/path/to/new/worktree", m_newPathBuf, sizeof(m_newPathBuf));
    ImGui::PopItemWidth();

    // Branch
    ImGui::TextUnformatted("Branch:");
    ImGui::SameLine(80);
    ImGui::PushItemWidth(220);
    if (ImGui::BeginCombo("##new_branch",
        (m_selectedBranch < (int)m_branchNames.size()) ? m_branchNames[m_selectedBranch].c_str() : ""))
    {
        for (int i = 0; i < (int)m_branchNames.size(); i++) {
            bool sel = (i == m_selectedBranch);
            if (ImGui::Selectable(m_branchNames[i].c_str(), sel))
                m_selectedBranch = i;
            if (sel) ImGui::SetItemDefaultFocus();
        }
        ImGui::EndCombo();
    }
    ImGui::PopItemWidth();

    // Options
    ImGui::Checkbox("Detach HEAD", &m_detachHead);
    if (!m_detachHead) {
        ImGui::SameLine();
        ImGui::TextUnformatted("|  Base commit (optional):");
        ImGui::SameLine();
        ImGui::PushItemWidth(160);
        ImGui::InputTextWithHint("##base_commit", "SHA / tag", m_baseCommitBuf, sizeof(m_baseCommitBuf));
        ImGui::PopItemWidth();
    }

    // Buttons
    ImGui::Spacing();
    bool canCreate = m_newPathBuf[0] != '\0' && m_selectedBranch < (int)m_branchNames.size();
    if (!canCreate) { ImGui::PushItemFlag(ImGuiItemFlags_Disabled, true); ImGui::PushStyleVar(ImGuiStyleVar_Alpha, 0.5f); }

    if (ImGui::Button("Create", ImVec2(80, 0))) {
        std::string branch = m_branchNames[m_selectedBranch];
        std::string baseCommit = m_baseCommitBuf[0] ? std::string(m_baseCommitBuf) : "";
        bool ok = m_repository->AddWorktree(m_newPathBuf, branch, m_detachHead, baseCommit);
        if (ok) {
            m_showAddForm = false;
            Refresh();
        }
        // On failure, keep form open so user can adjust
    }

    if (!canCreate) { ImGui::PopStyleVar(); ImGui::PopItemFlag(); }

    ImGui::SameLine();
    if (ImGui::Button("Cancel", ImVec2(80, 0)))
        m_showAddForm = false;

    ImGui::EndChild();
    ImGui::PopStyleColor();
    ImGui::Separator();
}

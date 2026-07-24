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
    m_nestedRepoCount = 0;
    m_nestedRepoCountLoaded = false;
    Refresh();
}

void WorktreePanel::Refresh()
{
    m_loaded = false;
    m_branchesDirty = true;
    m_nestedRepoCountLoaded = false;
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

void WorktreePanel::StartDetectNestedCount()
{
    if (m_detectingNested || m_nestedRepoCountLoaded || !m_repository) return;
    m_detectingNested = true;

    auto repo = m_repository;
    m_nestedDetectThread = std::thread([this, repo]() {
        auto nested = repo->DetectNestedRepos();
        m_nestedRepoCount = (int)nested.size();
        m_nestedRepoCountLoaded = true;
        m_detectingNested = false;
    });
    m_nestedDetectThread.detach();
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

    // Render post-create progress if active
    if (m_postTask && m_postTask->running) {
        RenderPostCreateProgress();
    }

    ImGui::BeginChild("##worktree_content", ImVec2(0, 0), true);

    // --- Header ---
    ImGui::TextColored(ImVec4(0.8f, 0.6f, 0.2f, 1.0f), "Git Worktrees");
    ImGui::SameLine(ImGui::GetWindowWidth() - 200);

    // Toolbar buttons
    bool creating = m_postTask && m_postTask->running;
    if (creating) { ImGui::PushItemFlag(ImGuiItemFlags_Disabled, true); ImGui::PushStyleVar(ImGuiStyleVar_Alpha, 0.5f); }

    if (ImGui::SmallButton("+ Add")) {
        m_showAddForm = !m_showAddForm;
        StartRefreshBranches();
        StartDetectNestedCount();  // fire & forget for badge display
        if (m_showAddForm) {
            m_newPathBuf[0] = '\0';
            m_selectedBranch = 0;
            m_detachHead = false;
            m_baseCommitBuf[0] = '\0';
            m_initSubmodules = true;
            m_copyNestedRepos = true;
        }
    }
    ImGui::SameLine();
    if (ImGui::SmallButton("Prune")) {
        if (m_repository->PruneWorktrees())
            Refresh();
    }
    if (ImGui::IsItemHovered())
        ImGui::SetTooltip("Remove stale worktree entries");

    ImGui::SameLine();
    if (ImGui::SmallButton("Refresh"))
        Refresh();

    if (creating) { ImGui::PopStyleVar(); ImGui::PopItemFlag(); }

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
        ImGui::TableSetupColumn("", ImGuiTableColumnFlags_WidthFixed, 120);
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
                // Init submodules button for existing worktrees
                if (ImGui::SmallButton("Sub")) {
                    if (m_repository->InitSubmodules(entry.info.path)) {
                        if (OnOperationLog)
                            OnOperationLog("submodule init", true,
                                "Submodules initialized in " + entry.info.path, "");
                    } else {
                        if (OnOperationLog)
                            OnOperationLog("submodule init", false,
                                "Failed: " + m_repository->GetLastGitError(), "");
                    }
                }
                if (ImGui::IsItemHovered())
                    ImGui::SetTooltip("Init submodules in this worktree");
                ImGui::SameLine();
                ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(1.0f, 0.3f, 0.3f, 1.0f));
                if (ImGui::SmallButton("X")) {
                    bool forceRemove = entry.info.isLocked;
                    if (m_repository->RemoveWorktree(entry.info.path, forceRemove)) {
                        Refresh();
                    } else {
                        if (m_repository->RemoveWorktree(entry.info.path, true))
                            Refresh();
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
    // Load nested count in background
    if (!m_nestedRepoCountLoaded && !m_detectingNested)
        StartDetectNestedCount();

    float formH = ImGui::GetFrameHeight() * 10 + 40;
    ImGui::PushStyleColor(ImGuiCol_ChildBg, ImVec4(0.15f, 0.20f, 0.15f, 1.0f));
    ImGui::BeginChild("##add_worktree_form", ImVec2(0, formH), true);

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

    // Detach / Base commit
    ImGui::TextUnformatted("Options:");
    ImGui::SameLine(80);
    ImGui::Checkbox("Detach HEAD", &m_detachHead);
    if (!m_detachHead) {
        ImGui::SameLine();
        ImGui::TextUnformatted("|  Base commit:");
        ImGui::SameLine();
        ImGui::PushItemWidth(160);
        ImGui::InputTextWithHint("##base_commit", "SHA / tag (optional)", m_baseCommitBuf, sizeof(m_baseCommitBuf));
        ImGui::PopItemWidth();
    }

    // --- Post-creation options ---
    ImGui::Spacing();
    ImGui::Separator();
    ImGui::TextColored(ImVec4(0.6f, 0.7f, 0.9f, 1.0f), "After creation:");

    // Submodules checkbox
    ImGui::Checkbox("Initialize submodules", &m_initSubmodules);
    ImGui::SameLine();
    ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f),
        "(git submodule update --init --recursive)");

    // Nested repos checkbox with count badge
    ImGui::Checkbox("Copy nested repositories", &m_copyNestedRepos);
    ImGui::SameLine();
    if (m_detectingNested) {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "(scanning...)");
    } else if (m_nestedRepoCountLoaded) {
        if (m_nestedRepoCount > 0)
            ImGui::TextColored(ImVec4(0.4f, 0.8f, 0.4f, 1.0f), "(%d found)", m_nestedRepoCount);
        else
            ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "(none detected)");
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

            // Check if post-creation tasks are needed
            bool needSubmodules = m_initSubmodules;
            bool needNested = m_copyNestedRepos && m_nestedRepoCount > 0;

            if (needSubmodules || needNested) {
                // Launch async post-create task
                m_postTask = std::make_unique<PostCreateTask>();
                m_postTask->worktreePath = m_newPathBuf;
                m_postTask->initSubmodules = needSubmodules;
                m_postTask->copyNested = needNested;
                m_postTask->totalNested = m_nestedRepoCount;
                m_postTask->running = true;

                auto repo = m_repository;
                std::string wtPath = m_newPathBuf;
                auto* task = m_postTask.get();

                task->worker = std::thread([repo, wtPath, task]() {
                    // Step 1: Init submodules
                    if (task->initSubmodules) {
                        task->status = "Initializing submodules...";
                        if (repo->InitSubmodules(wtPath)) {
                            task->submoduleDone = true;
                        } else {
                            task->submoduleError = repo->GetLastGitError();
                            task->submoduleDone = true;
                        }
                    } else {
                        task->submoduleDone = true;
                    }

                    // Step 2: Copy nested repos
                    if (task->copyNested) {
                        auto nested = repo->DetectNestedRepos();
                        task->totalNested = (int)nested.size();
                        for (int i = 0; i < task->totalNested; i++) {
                            auto& nr = nested[i];
                            task->status = "Cloning nested repo " + std::to_string(i + 1) +
                                           "/" + std::to_string(task->totalNested) +
                                           ": " + nr.path;
                            std::string dstPath = wtPath + "/" + nr.path;
                            // Create parent directories if needed
                            std::filesystem::create_directories(
                                std::filesystem::path(dstPath).parent_path());
                            if (!repo->CloneNestedRepo(nr.gitDir, dstPath)) {
                                task->nestedErrors.push_back(nr.path + ": " + repo->GetLastGitError());
                            }
                            task->nestedDone = i + 1;
                        }
                    }

                    task->status = "Done";
                    task->running = false;
                });
                task->worker.detach();
            }

            Refresh();
        }
    }

    if (!canCreate) { ImGui::PopStyleVar(); ImGui::PopItemFlag(); }

    ImGui::SameLine();
    if (ImGui::Button("Cancel", ImVec2(80, 0)))
        m_showAddForm = false;

    ImGui::EndChild();
    ImGui::PopStyleColor();
    ImGui::Separator();
}

void WorktreePanel::RenderPostCreateProgress()
{
    if (!m_postTask) return;

    auto& t = *m_postTask;

    // Auto-join and cleanup when done
    if (!t.running) {
        if (t.worker.joinable()) t.worker.join();

        // Build summary for operation log
        std::string summary;
        if (t.submoduleDone && t.submoduleError.empty())
            summary += "Submodules: OK\n";
        else if (!t.submoduleError.empty())
            summary += "Submodules: FAILED - " + t.submoduleError + "\n";
        if (t.copyNested) {
            int ok = t.nestedDone - (int)t.nestedErrors.size();
            summary += "Nested repos: " + std::to_string(ok) + "/" +
                       std::to_string(t.totalNested) + " cloned";
            for (auto& e : t.nestedErrors)
                summary += "\n  Error: " + e;
        }

        if (OnOperationLog)
            OnOperationLog("worktree post-init", t.nestedErrors.empty() && t.submoduleError.empty(),
                summary, "Worktree: " + t.worktreePath);

        m_postTask.reset();
        Refresh();
        return;
    }

    // Progress bar rendering
    ImGui::PushStyleColor(ImGuiCol_ChildBg, ImVec4(0.15f, 0.15f, 0.25f, 1.0f));
    ImGui::BeginChild("##post_create_progress", ImVec2(0, ImGui::GetFrameHeight() * 3 + 16), true);

    ImGui::TextColored(ImVec4(0.5f, 0.7f, 1.0f, 1.0f), "%s", t.status.c_str());

    int totalSteps = (t.initSubmodules ? 1 : 0) + (t.copyNested ? t.totalNested : 0);
    int doneSteps = 0;
    if (t.submoduleDone) doneSteps++;
    doneSteps += t.nestedDone;

    if (totalSteps > 0) {
        float frac = (float)doneSteps / (float)totalSteps;
        ImGui::ProgressBar(frac, ImVec2(-1, 0),
            doneSteps > 0 ? (std::to_string(doneSteps) + "/" + std::to_string(totalSteps)).c_str() : "");
    } else {
        LoadingSpinner(8.0f, 2.0f);
        ImGui::SameLine();
        ImGui::TextUnformatted("Working...");
    }

    ImGui::EndChild();
    ImGui::PopStyleColor();
    ImGui::Separator();
}

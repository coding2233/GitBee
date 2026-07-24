#include "worktree_panel.h"
#include "LoadingSpinner.h"
#include "../gitcore/git_repository.h"
#include <imgui.h>
#include <filesystem>
#include <algorithm>
#include <sstream>

// -----------------------------------------------------------------------
// Helpers
// -----------------------------------------------------------------------
bool WorktreePanel::IsAnyOpRunning() const
{
    return m_op && m_op->running;
}

// -----------------------------------------------------------------------
// SetRepository / Refresh
// -----------------------------------------------------------------------
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

// -----------------------------------------------------------------------
// Async worktree list loading
// -----------------------------------------------------------------------
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

// -----------------------------------------------------------------------
// Branch / nested helpers
// -----------------------------------------------------------------------
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

// -----------------------------------------------------------------------
// Async operation launchers
// -----------------------------------------------------------------------
void WorktreePanel::OpStartAddWorktree(const std::string& path, const std::string& branch,
                                       bool detach, const std::string& baseCommit)
{
    auto op = std::make_unique<Operation>();
    op->type = OpType::AddWorktree;
    op->running = true;
    op->result = false;
    op->statusText = "Creating worktree...";
    op->worktreePath = path;
    op->branch = branch;
    op->detach = detach;
    op->baseCommit = baseCommit;

    auto repo = m_repository;
    auto* raw = op.get();

    raw->worker = std::thread([repo, raw]() {
        std::this_thread::sleep_for(std::chrono::milliseconds(100)); // let UI update
        raw->statusText = "Running: git worktree add...";
        bool ok = repo->AddWorktree(raw->worktreePath, raw->branch,
                                    raw->detach, raw->baseCommit);
        if (!ok) raw->error = repo->GetLastGitError();
        raw->result = ok;
        raw->running = false;
    });
    raw->worker.detach();

    m_op = std::move(op);
}

void WorktreePanel::OpStartRemoveWorktree(const std::string& path)
{
    auto op = std::make_unique<Operation>();
    op->type = OpType::RemoveWorktree;
    op->running = true;
    op->statusText = "Removing worktree...";
    op->targetPath = path;

    auto repo = m_repository;
    auto* raw = op.get();

    raw->worker = std::thread([repo, raw]() {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
        raw->statusText = "Running: git worktree remove...";
        bool ok = repo->RemoveWorktree(raw->targetPath, false);
        if (!ok) {
            raw->statusText = "Retrying with --force...";
            ok = repo->RemoveWorktree(raw->targetPath, true);
        }
        if (!ok) raw->error = repo->GetLastGitError();
        raw->result = ok;
        raw->running = false;
    });
    raw->worker.detach();

    m_op = std::move(op);
}

void WorktreePanel::OpStartPrune()
{
    auto op = std::make_unique<Operation>();
    op->type = OpType::Prune;
    op->running = true;
    op->statusText = "Pruning stale worktrees...";

    auto repo = m_repository;
    auto* raw = op.get();

    raw->worker = std::thread([repo, raw]() {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
        raw->statusText = "Running: git worktree prune...";
        bool ok = repo->PruneWorktrees();
        if (!ok) raw->error = repo->GetLastGitError();
        raw->result = ok;
        raw->running = false;
    });
    raw->worker.detach();

    m_op = std::move(op);
}

void WorktreePanel::OpStartInitSubmodules(const std::string& path)
{
    auto op = std::make_unique<Operation>();
    op->type = OpType::InitSubmodules;
    op->running = true;
    op->statusText = "Initializing submodules...";
    op->targetPath = path;

    auto repo = m_repository;
    auto* raw = op.get();

    raw->worker = std::thread([repo, raw]() {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
        raw->statusText = "Running: git submodule update --init --recursive...";
        bool ok = repo->InitSubmodules(raw->targetPath);
        if (!ok) raw->error = repo->GetLastGitError();
        raw->result = ok;
        raw->running = false;
    });
    raw->worker.detach();

    m_op = std::move(op);
}

void WorktreePanel::OpStartPostCreate(const std::string& path, bool doSub, bool doNested, int nestedCount)
{
    auto op = std::make_unique<Operation>();
    op->type = OpType::PostCreate;
    op->running = true;
    op->statusText = "Starting post-creation tasks...";
    op->worktreePath = path;
    op->doInitSubmodules = doSub;
    op->doCopyNested = doNested;
    op->totalNested = nestedCount;
    op->nestedDone = 0;
    op->submoduleDone = false;

    auto repo = m_repository;
    std::string wtPath = path;
    auto* raw = op.get();

    raw->worker = std::thread([repo, wtPath, raw]() {
        // Step 1: Init submodules
        if (raw->doInitSubmodules) {
            raw->statusText = "Initializing submodules...";
            if (repo->InitSubmodules(wtPath)) {
                raw->submoduleDone = true;
            } else {
                raw->submoduleError = repo->GetLastGitError();
                raw->submoduleDone = true;
            }
        } else {
            raw->submoduleDone = true;
        }

        // Step 2: Copy nested repos
        if (raw->doCopyNested) {
            auto nested = repo->DetectNestedRepos();
            raw->totalNested = (int)nested.size();
            for (int i = 0; i < raw->totalNested; i++) {
                auto& nr = nested[i];
                raw->statusText = "Cloning nested repo " + std::to_string(i + 1) +
                                  "/" + std::to_string(raw->totalNested) +
                                  ": " + nr.path;
                std::string dstPath = wtPath + "/" + nr.path;
                std::filesystem::create_directories(
                    std::filesystem::path(dstPath).parent_path());
                if (!repo->CloneNestedRepo(nr.gitDir, dstPath)) {
                    raw->nestedErrors.push_back(nr.path + ": " + repo->GetLastGitError());
                }
                raw->nestedDone = i + 1;
            }
        }

        raw->statusText = "Done";
        raw->running = false;
    });
    raw->worker.detach();

    m_op = std::move(op);
}

// -----------------------------------------------------------------------
// Process completed operation -> log + refresh
// -----------------------------------------------------------------------
void WorktreePanel::OpProcessResult()
{
    if (!m_op || m_op->running) return;

    // Join worker thread
    if (m_op->worker.joinable())
        m_op->worker.join();

    // Build log entry
    switch (m_op->type) {
    case OpType::AddWorktree:
        if (OnOperationLog)
            OnOperationLog("worktree add", m_op->result,
                m_op->result ? ("Created: " + m_op->worktreePath)
                             : ("Failed: " + m_op->error),
                m_op->error);
        if (m_op->result) {
            m_showAddForm = false;

            // Check if post-create tasks are needed
            bool needSub = m_initSubmodules;
            bool needNested = m_copyNestedRepos && m_nestedRepoCount > 0;
            if (needSub || needNested) {
                // Transfer worktreePath to post-create op
                std::string wtPath = m_op->worktreePath;
                m_op.reset();
                OpStartPostCreate(wtPath, needSub, needNested, m_nestedRepoCount);
                return; // don't refresh yet, post-create will handle
            }
        }
        break;
    case OpType::RemoveWorktree:
        if (OnOperationLog)
            OnOperationLog("worktree remove", m_op->result,
                m_op->result ? ("Removed: " + m_op->targetPath)
                             : ("Failed: " + m_op->error),
                m_op->error);
        break;
    case OpType::Prune:
        if (OnOperationLog)
            OnOperationLog("worktree prune", m_op->result,
                m_op->result ? "Pruned stale entries" : "Failed: " + m_op->error,
                m_op->error);
        break;
    case OpType::InitSubmodules:
        if (OnOperationLog)
            OnOperationLog("submodule init", m_op->result,
                m_op->result ? ("Submodules inited: " + m_op->targetPath)
                             : ("Failed: " + m_op->error),
                m_op->error);
        break;
    case OpType::PostCreate: {
        std::string summary;
        if (m_op->doInitSubmodules) {
            if (m_op->submoduleError.empty())
                summary += "Submodules: OK\n";
            else
                summary += "Submodules: FAILED - " + m_op->submoduleError + "\n";
        }
        if (m_op->doCopyNested) {
            int ok = m_op->nestedDone - (int)m_op->nestedErrors.size();
            summary += "Nested repos: " + std::to_string(ok) + "/" +
                       std::to_string(m_op->totalNested) + " cloned";
            for (auto& e : m_op->nestedErrors)
                summary += "\n  Error: " + e;
        }
        bool allOk = m_op->submoduleError.empty() && m_op->nestedErrors.empty();
        if (OnOperationLog)
            OnOperationLog("worktree post-init", allOk, summary,
                           "Worktree: " + m_op->worktreePath);
        break;
    }
    default: break;
    }

    m_op.reset();
    Refresh();
}

// -----------------------------------------------------------------------
// Render progress bar for active operation
// -----------------------------------------------------------------------
void WorktreePanel::OpRenderProgress()
{
    if (!m_op || !m_op->running) return;

    ImGui::PushStyleColor(ImGuiCol_ChildBg, ImVec4(0.15f, 0.15f, 0.25f, 1.0f));
    ImGui::BeginChild("##op_progress", ImVec2(0, ImGui::GetFrameHeight() * 3 + 16), true);

    // Icon + status text
    LoadingSpinner(10.0f, 2.5f);
    ImGui::SameLine();
    ImGui::TextColored(ImVec4(0.5f, 0.7f, 1.0f, 1.0f), "%s", m_op->statusText.c_str());

    // Progress bar for PostCreate (multi-step)
    if (m_op->type == OpType::PostCreate) {
        int totalSteps = (m_op->doInitSubmodules ? 1 : 0) + (m_op->doCopyNested ? m_op->totalNested : 0);
        int doneSteps = 0;
        if (m_op->submoduleDone) doneSteps++;
        doneSteps += m_op->nestedDone;

        if (totalSteps > 0) {
            float frac = std::min(1.0f, (float)doneSteps / (float)totalSteps);
            ImGui::ProgressBar(frac, ImVec2(-1, 0),
                (std::to_string(doneSteps) + "/" + std::to_string(totalSteps)).c_str());
        }
    } else {
        // Indeterminate spinner for simple ops
        float w = ImGui::GetContentRegionAvail().x;
        ImGui::ProgressBar(-1.0f /* indeterminate */, ImVec2(w, 0), "");
    }

    ImGui::EndChild();
    ImGui::PopStyleColor();
    ImGui::Separator();
}

// -----------------------------------------------------------------------
// Main Render
// -----------------------------------------------------------------------
void WorktreePanel::Render()
{
    if (!m_repository)
    {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "No repository opened");
        return;
    }

    // Process async worktree list load
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

    // Process completed operation
    OpProcessResult();

    bool opBusy = IsAnyOpRunning();

    // Progress banner (rendered outside the child window so it's always visible)
    if (opBusy)
        OpRenderProgress();

    ImGui::BeginChild("##worktree_content", ImVec2(0, 0), true);

    // --- Header ---
    ImGui::TextColored(ImVec4(0.8f, 0.6f, 0.2f, 1.0f), "Git Worktrees");
    ImGui::SameLine(ImGui::GetWindowWidth() - 200);

    if (opBusy) { ImGui::PushItemFlag(ImGuiItemFlags_Disabled, true); ImGui::PushStyleVar(ImGuiStyleVar_Alpha, 0.5f); }

    if (ImGui::SmallButton("+ Add")) {
        m_showAddForm = !m_showAddForm;
        StartRefreshBranches();
        StartDetectNestedCount();
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
        OpStartPrune();
    }
    if (ImGui::IsItemHovered())
        ImGui::SetTooltip("Remove stale worktree entries");

    ImGui::SameLine();
    if (ImGui::SmallButton("Refresh"))
        Refresh();

    if (opBusy) { ImGui::PopStyleVar(); ImGui::PopItemFlag(); }

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
                if (opBusy) { ImGui::PushItemFlag(ImGuiItemFlags_Disabled, true); ImGui::PushStyleVar(ImGuiStyleVar_Alpha, 0.5f); }

                if (ImGui::SmallButton("Open")) {
                    if (OnOpenWorktree) OnOpenWorktree(entry.info.path);
                }
                ImGui::SameLine();
                if (ImGui::SmallButton("Sub")) {
                    OpStartInitSubmodules(entry.info.path);
                }
                if (ImGui::IsItemHovered())
                    ImGui::SetTooltip("Init submodules in this worktree");
                ImGui::SameLine();
                ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(1.0f, 0.3f, 0.3f, 1.0f));
                if (ImGui::SmallButton("X")) {
                    OpStartRemoveWorktree(entry.info.path);
                }
                ImGui::PopStyleColor();
                if (ImGui::IsItemHovered())
                    ImGui::SetTooltip("Remove this worktree");

                if (opBusy) { ImGui::PopStyleVar(); ImGui::PopItemFlag(); }
            }

            ImGui::PopID();
        }

        ImGui::EndTable();
    }

    ImGui::EndChild();
}

// -----------------------------------------------------------------------
// RenderAddForm
// -----------------------------------------------------------------------
void WorktreePanel::RenderAddForm()
{
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

    ImGui::Checkbox("Initialize submodules", &m_initSubmodules);
    ImGui::SameLine();
    ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f),
        "(git submodule update --init --recursive)");

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
    if (!canCreate || IsAnyOpRunning()) {
        ImGui::PushItemFlag(ImGuiItemFlags_Disabled, true);
        ImGui::PushStyleVar(ImGuiStyleVar_Alpha, 0.5f);
    }

    if (ImGui::Button("Create", ImVec2(80, 0))) {
        std::string branch = m_branchNames[m_selectedBranch];
        std::string baseCommit = m_baseCommitBuf[0] ? std::string(m_baseCommitBuf) : "";
        OpStartAddWorktree(m_newPathBuf, branch, m_detachHead, baseCommit);
    }

    if (!canCreate || IsAnyOpRunning()) {
        ImGui::PopStyleVar();
        ImGui::PopItemFlag();
    }

    ImGui::SameLine();
    if (ImGui::Button("Cancel", ImVec2(80, 0)))
        m_showAddForm = false;

    ImGui::EndChild();
    ImGui::PopStyleColor();
    ImGui::Separator();
}

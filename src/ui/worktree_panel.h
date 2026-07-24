#pragma once

#include <memory>
#include <functional>
#include <string>
#include <vector>
#include <atomic>
#include <thread>
#include <mutex>
#include "../gitcore/git_types.h"

class GitRepository;

class WorktreePanel
{
public:
    void Render();
    void SetRepository(std::shared_ptr<GitRepository> repo);
    void Refresh();

    // Callback when user wants to open a worktree in a new tab
    std::function<void(const std::string& path)> OnOpenWorktree;

private:
    struct WorktreeEntry {
        GitWorktreeInfo info;
        bool exists = true;
    };

    std::shared_ptr<GitRepository> m_repository;
    std::vector<WorktreeEntry> m_worktrees;
    bool m_loaded = false;

    // Add worktree form state
    bool m_showAddForm = false;
    char m_newPathBuf[1024]{};
    int m_selectedBranch = 0;
    bool m_detachHead = false;
    char m_baseCommitBuf[64]{};

    // Async
    std::atomic<bool> m_loading{false};
    std::thread m_loadThread;
    std::mutex m_mutex;
    std::vector<WorktreeEntry> m_pendingWorktrees;

    void LoadWorktrees();
    void RenderWorktreeRow(const WorktreeEntry& entry);
    void RenderAddForm();

    // Branch options for the add form
    void StartRefreshBranches();
    std::vector<std::string> m_branchNames;
    bool m_branchesDirty = true;
};

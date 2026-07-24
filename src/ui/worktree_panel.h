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
    // Callback for operation logging
    std::function<void(const std::string& operation, bool success,
                       const std::string& summary, const std::string& detail)> OnOperationLog;

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
    bool m_initSubmodules = true;   // default on
    bool m_copyNestedRepos = true;  // default on

    // Nested repo count (detected from current worktree)
    int m_nestedRepoCount = 0;
    bool m_nestedRepoCountLoaded = false;

    // Post-create async tasks
    struct PostCreateTask {
        std::atomic<bool> running{false};
        std::string worktreePath;
        bool initSubmodules = false;
        bool copyNested = false;
        int totalNested = 0;
        int nestedDone = 0;
        std::string status;
        bool submoduleDone = false;
        std::string submoduleError;
        std::vector<std::string> nestedErrors;
        std::thread worker;
    };
    std::unique_ptr<PostCreateTask> m_postTask;

    // Async
    std::atomic<bool> m_loading{false};
    std::thread m_loadThread;
    std::mutex m_mutex;
    std::vector<WorktreeEntry> m_pendingWorktrees;

    void LoadWorktrees();
    void RenderWorktreeRow(const WorktreeEntry& entry);
    void RenderAddForm();
    void RenderPostCreateProgress();

    // Branch options for the add form
    void StartRefreshBranches();
    std::vector<std::string> m_branchNames;
    bool m_branchesDirty = true;

    // Detect nested repos count (async)
    void StartDetectNestedCount();
    std::atomic<bool> m_detectingNested{false};
    std::thread m_nestedDetectThread;
};

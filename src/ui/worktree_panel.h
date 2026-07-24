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
    bool m_initSubmodules = true;
    bool m_copyNestedRepos = true;

    // Nested repo count (detected from current worktree)
    int m_nestedRepoCount = 0;
    bool m_nestedRepoCountLoaded = false;

    // ------------------------------------------------------------------
    // Unified async operation state
    // ------------------------------------------------------------------
    enum class OpType {
        None,
        AddWorktree,
        RemoveWorktree,
        Prune,
        InitSubmodules,
        PostCreate    // submodule init + nested clone after add
    };

    struct Operation {
        OpType type = OpType::None;
        std::atomic<bool> running{false};
        bool result = false;
        std::string error;
        std::string statusText;   // current step description, updated by worker
        std::thread worker;

        // For Add
        std::string worktreePath;
        std::string branch;
        std::string baseCommit;
        bool detach = false;

        // For Remove / InitSubmodules
        std::string targetPath;

        // For PostCreate
        bool doInitSubmodules = false;
        bool doCopyNested = false;
        int totalNested = 0;
        int nestedDone = 0;
        bool submoduleDone = false;
        std::string submoduleError;
        std::vector<std::string> nestedErrors;
    };

    std::unique_ptr<Operation> m_op;

    void OpStartAddWorktree(const std::string& path, const std::string& branch,
                            bool detach, const std::string& baseCommit);
    void OpStartRemoveWorktree(const std::string& path);
    void OpStartPrune();
    void OpStartInitSubmodules(const std::string& path);
    void OpStartPostCreate(const std::string& path, bool doSub, bool doNested, int nestedCount);
    void OpProcessResult();
    void OpRenderProgress();

    // ------------------------------------------------------------------
    // Async data loading
    // ------------------------------------------------------------------
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

    // Detect nested repos count (async)
    void StartDetectNestedCount();
    std::atomic<bool> m_detectingNested{false};
    std::thread m_nestedDetectThread;

    bool IsAnyOpRunning() const;
};

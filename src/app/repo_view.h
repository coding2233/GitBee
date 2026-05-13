#pragma once

#include <memory>
#include <string>
#include <vector>
#include <atomic>
#include <thread>
#include <mutex>
#include <functional>
#include "../ui/SplitView.h"
#include "../gitcore/git_types.h"

class GitRepository;
class WorkspacePanel;
class WorkTreePanel;
class LogPanel;
class ConfigPanel;

class RepoView
{
public:
    RepoView(std::shared_ptr<GitRepository> repo);
    ~RepoView();

    void Render();

    std::string GetName() const;
    const std::string& GetPath() const { return m_repoPath; }

    std::function<void(const std::string&)> OnStatusMessage;

private:
    enum class Section {
        Workspace,
        Files,
        History,
        Config,
    };

    void RenderToolbar();
    void RenderSidebar();
    void RenderContent();

    void DoGitAction(const char* action);
    void RenderSidebarSection(const char* name, Section section);
    void RenderBranchTree();

    void RefreshBranchData();
    void RefreshAll();
    void ProcessAsyncResult();

    // Async checkout
    void StartAsyncCheckout(const std::string& branchName);
    void ProcessCheckoutResult();

    struct AsyncTask {
        std::atomic<bool> running{false};
        bool result = false;
        std::string error;
        std::string name;
        std::thread thread;
    };

    AsyncTask m_asyncTask;
    bool m_processingAsyncResult = false;

    // Cached branch data (defined before use)
    struct BranchItem {
        std::string name;
        bool isHead = false;
        bool isRemote = false;
    };

    // Async branch loading
    std::atomic<bool> m_branchDataLoading{false};
    std::thread m_branchThread;
    std::mutex m_branchMutex;
    struct BranchData {
        std::vector<BranchItem> localBranches;
        std::vector<BranchItem> remoteBranches;
        std::vector<std::string> tagNames;
        int stashCount = 0;
    };
    BranchData m_pendingBranchData;

    std::shared_ptr<GitRepository> m_repository;
    std::string m_repoPath;

    std::unique_ptr<WorkspacePanel> m_workspacePanel;
    std::unique_ptr<WorkTreePanel> m_worktreePanel;
    std::unique_ptr<LogPanel> m_logPanel;
    std::unique_ptr<ConfigPanel> m_configPanel;

    SplitView m_splitView{ SplitView::Type::Horizontal, 220, 80 };
    Section m_activeSection = Section::Workspace;
    std::vector<BranchItem> m_localBranches;
    std::vector<BranchItem> m_remoteBranches;
    std::vector<std::string> m_tagNames;
    int m_stashCount = -1;
    bool m_branchDataDirty = true;

    // Async checkout state
    std::atomic<bool> m_checkoutLoading{false};
    std::string m_checkoutBranchName;
    std::string m_checkoutError;
    std::thread m_checkoutThread;
};

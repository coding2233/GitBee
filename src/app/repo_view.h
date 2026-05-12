#pragma once

#include <memory>
#include <string>
#include <vector>
#include <atomic>
#include <thread>
#include <functional>
#include "../ui/SplitView.h"
#include "../gitcore/git_types.h"

class GitRepository;
class WorkspacePanel;
class WorkTreePanel;
class LogPanel;

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

    struct AsyncTask {
        std::atomic<bool> running{false};
        bool result = false;
        std::string error;
        std::string name;
        std::thread thread;
    };

    AsyncTask m_asyncTask;
    bool m_processingAsyncResult = false;

    std::shared_ptr<GitRepository> m_repository;
    std::string m_repoPath;

    std::unique_ptr<WorkspacePanel> m_workspacePanel;
    std::unique_ptr<WorkTreePanel> m_worktreePanel;
    std::unique_ptr<LogPanel> m_logPanel;

    SplitView m_splitView{ SplitView::Type::Horizontal, 220, 80 };
    Section m_activeSection = Section::Workspace;

    // Cached branch data
    struct BranchItem {
        std::string name;
        bool isHead = false;
        bool isRemote = false;
    };
    std::vector<BranchItem> m_localBranches;
    std::vector<BranchItem> m_remoteBranches;
    std::vector<std::string> m_tagNames;
    int m_stashCount = -1;
    bool m_branchDataDirty = true;
};

#pragma once

#include <memory>
#include <string>
#include <vector>
#include "../ui/SplitView.h"
#include "../gitcore/git_types.h"

class GitRepository;
class WorkspacePanel;
class WorkTreePanel;
class LogPanel;
class DiffPanel;

class RepoView
{
public:
    RepoView(std::shared_ptr<GitRepository> repo);
    ~RepoView();

    void Render();

    std::string GetName() const;
    const std::string& GetPath() const { return m_repoPath; }

private:
    enum class Section {
        Workspace,
        Files,
        Changes,
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

    std::shared_ptr<GitRepository> m_repository;
    std::string m_repoPath;

    std::unique_ptr<WorkspacePanel> m_workspacePanel;
    std::unique_ptr<WorkTreePanel> m_worktreePanel;
    std::unique_ptr<LogPanel> m_logPanel;
    std::unique_ptr<DiffPanel> m_diffPanel;

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

#pragma once

#include <memory>
#include <functional>
#include <string>
#include <vector>
#include <set>
#include "../gitcore/git_types.h"
#include "SplitView.h"

class GitRepository;

class WorkspacePanel {
public:
    void Render();
    void SetRepository(std::shared_ptr<GitRepository> repo);
    void Refresh();

    std::function<void(const std::string& filePath)> OnOpenFile;

private:
    std::shared_ptr<GitRepository> m_repository;
    GitStatus m_status;
    std::string m_commitMessage;
    SplitView m_hSplit{ SplitView::Type::Horizontal, 0.45f, 100 };
    SplitView m_vSplit{ SplitView::Type::Vertical, 0.5f, 80 };
    bool m_updating = false;

    std::set<std::string> m_selectedStagedPaths;
    std::set<std::string> m_selectedUnstagedPaths;

    void RenderStagedArea();
    void RenderUnstagedArea();
    void RenderCommitArea();
    void RenderFileRow(const std::string& path, const std::string& oldPath, GitFileStatus status, bool isStaged);
};

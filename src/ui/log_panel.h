#pragma once

#include <string>
#include <vector>
#include <functional>
#include <memory>
#include "../gitcore/git_types.h"
#include "../gitcore/git_repository.h"

class LogPanel {
public:
    void Render();
    void SetRepository(std::shared_ptr<GitRepository> repo);
    void Refresh();

    std::function<void(const GitCommit&)> OnCommitSelected;

private:
    std::shared_ptr<GitRepository> m_repository;
    std::vector<GitCommit> m_commits;
    GitLogOptions m_logOptions;
    int m_selectedIndex = -1;
    bool m_loading = false;
    bool m_hasMore = true;
    std::string m_filterText;
    std::vector<std::string> m_branches;
    int m_selectedBranch = 0;

    static constexpr int LOAD_BATCH_SIZE = 100;

    void LoadMoreIfNeeded();
    std::string FormatRelativeTime(const std::string& isoDate);
    void FetchBranches();
    void RenderCommitRow(int index, const GitCommit& commit);
    void RenderGraphIndicator(const GitCommit& commit);
    void RenderFilterBar();
    void RenderCommitList();
};
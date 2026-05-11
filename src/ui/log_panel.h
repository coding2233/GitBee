#pragma once

#include <string>
#include <vector>
#include <functional>
#include <memory>
#include "../gitcore/git_types.h"
#include "../gitcore/git_repository.h"
#include "SplitView.h"

struct CommitGraphLine
{
    std::string parentSha;
    ImVec2 childPoint;
    int laneId = 0;
};

class LogPanel
{
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

    SplitView m_contentSplit{ SplitView::Type::Vertical, 0.6f, 80.0f };
    bool m_showDetailPanel = false;

    static constexpr int LOAD_BATCH_SIZE = 100;

    struct CommitTableRow
    {
        std::string sha;
        std::string shortSha;
        std::string message;
        std::string author;
        std::string date;
        std::string relativeTime;
        std::vector<std::string> parentShas;
    };

    std::vector<CommitTableRow> m_tableRows;

    void LoadMoreIfNeeded();
    std::string FormatRelativeTime(const std::string& isoDate);
    void FetchBranches();
    void RenderFilterBar();
    void RenderCommitTable();
    void RenderCommitDetail();
    void DrawGraph(int laneId, const ImVec2& center, bool isMerge, bool isHead);
    void UpdateGraphLanes(const GitCommit& commit, std::vector<int>& lanes, std::vector<CommitGraphLine>& lines, int& maxLane);
};

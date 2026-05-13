#pragma once

#include <string>
#include <vector>
#include <functional>
#include <memory>
#include <atomic>
#include <thread>
#include <mutex>
#include "../gitcore/git_types.h"
#include "../gitcore/git_repository.h"
#include "../gitcore/git_process.h"
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
    struct FileDiffEntry
    {
        std::string filePath;
        std::string diffContent;
        bool expanded = false;
        int addedLines = 0;
        int removedLines = 0;
    };

    struct DiffLineInfo
    {
        enum Type { Normal, Added, Removed, Header, Hunk };
        Type type = Normal;
        int oldLineNo = -1;
        int newLineNo = -1;
        std::string content;
    };

    struct AsyncCommitLoader {
        std::atomic<bool> running{false};
        std::vector<GitCommit> results;
        GitLogOptions options;
        int skipCount = 0;
        std::thread worker;
    };

    struct AsyncDetailLoader {
        std::atomic<bool> running{false};
        std::string hash;
        GitCommitDetail detail;
        std::vector<std::string> modifiedFiles;
        std::thread worker;
    };

    struct AsyncDiffLoader {
        std::atomic<bool> running{false};
        std::string diffContent;
        std::thread worker;
    };

    std::shared_ptr<GitRepository> m_repository;
    std::vector<GitCommit> m_commits;
    GitLogOptions m_logOptions;
    int m_selectedIndex = -1;
    bool m_loading = false;
    bool m_hasMore = true;
    std::string m_filterText;
    std::vector<std::string> m_branches;
    int m_selectedBranch = 0;
    bool m_branchesLoading = false;

    SplitView m_contentSplit{ SplitView::Type::Vertical, 0.45f, 80.0f };
    bool m_showDetailPanel = false;
    bool m_loadedExtraOnInit = false;

    // Commit detail + diff state
    GitCommit m_selectedCommit;
    GitCommitDetail m_currentCommitDetail;
    std::vector<FileDiffEntry> m_fileDiffs;
    int m_selectedFileIndex = -1;

    SplitView m_detailSplit{ SplitView::Type::Horizontal, 0.35f, 100.0f };
    SplitView m_infoFileSplit{ SplitView::Type::Vertical, 0.25f, 60.0f };

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

    // Async state
    AsyncCommitLoader m_commitLoader;
    AsyncDetailLoader m_detailLoader;
    AsyncDiffLoader m_diffLoader;
    std::mutex m_pendingCommitsMutex;
    std::vector<GitCommit> m_pendingCommits;
    bool m_pendingHasMore = false;

    void LoadMoreIfNeeded();
    void ProcessAsyncResults();
    void StartAsyncCommitLoad();
    void StartAsyncDetailLoad();
    void StartAsyncDiffLoad(const std::string& filePath);

    std::string FormatRelativeTime(const std::string& isoDate);
    void FetchBranches();
    void RenderFilterBar();
    void RenderCommitTable();
    void RenderCommitDetail();
    void RenderFileList();
    void RenderDiffContent(const std::string& diff);
    void RenderCommitHeader();
    void FetchDiff(FileDiffEntry& entry);
    void BuildSelectCommitPatch();
    std::vector<DiffLineInfo> ParseDiffLines(const std::string& diff);
    void DrawGraph(int laneId, const ImVec2& center, bool isMerge, bool isHead);
    void UpdateGraphLanes(const GitCommit& commit, std::vector<int>& lanes, std::vector<CommitGraphLine>& lines, int& maxLane);
};

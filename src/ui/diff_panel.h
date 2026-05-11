#pragma once

#include <string>
#include <vector>
#include <memory>
#include "../gitcore/git_types.h"
#include "../gitcore/git_repository.h"
#include "SplitView.h"

class DiffPanel
{
public:
    DiffPanel();

    void Render();
    void ShowCommitDetail(const GitCommit& commit);
    void Clear();
    void SetRepository(std::shared_ptr<GitRepository> repo);

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

    std::shared_ptr<GitRepository> m_repository;
    GitCommitDetail m_currentCommit;
    std::vector<FileDiffEntry> m_fileDiffs;
    bool m_hasCommitDetail = false;
    SplitView m_splitView{ SplitView::Type::Vertical, 0.3f, 60.0f };

    void FetchDiff(FileDiffEntry& entry);
    void RenderCommitHeader();
    void RenderFileList();
    void RenderDiffContent(const std::string& diff);
    std::vector<DiffLineInfo> ParseDiffLines(const std::string& diff);
};

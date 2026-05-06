#pragma once

#include <string>
#include <vector>
#include <memory>
#include "../gitcore/git_types.h"
#include "../gitcore/git_repository.h"

class DiffPanel {
public:
    DiffPanel();

    void Render();
    void ShowCommitDetail(const GitCommit& commit);
    void Clear();
    void SetRepository(std::shared_ptr<GitRepository> repo);

private:
    struct FileDiffEntry {
        std::string filePath;
        std::string diffContent;
        bool expanded = false;
    };

    std::shared_ptr<GitRepository> m_repository;
    GitCommitDetail m_currentCommit;
    std::vector<FileDiffEntry> m_fileDiffs;
    bool m_hasCommitDetail = false;

    void FetchDiff(FileDiffEntry& entry);
    void RenderCommitHeader();
    void RenderFileList();
    void RenderDiffContent(const std::string& diff);
};
#pragma once

#include <memory>
#include <functional>
#include <string>
#include <vector>
#include <set>
#include <atomic>
#include <thread>
#include <mutex>
#include "../gitcore/git_types.h"
#include "SplitView.h"

class GitRepository;

class WorkspacePanel {
public:
    WorkspacePanel();
    ~WorkspacePanel();
    void Render();
    void SetRepository(std::shared_ptr<GitRepository> repo);
    void Refresh();

    std::function<void(const std::string& filePath)> OnOpenFile;

private:
    struct FileTreeNode {
        std::string name;
        std::string fullPath;
        const GitFileEntry* file = nullptr;
        std::vector<FileTreeNode> children;
        bool expanded = true;
    };

    struct DiffLine {
        enum Type { Normal, Added, Removed, Hunk, Header };
        Type type = Normal;
        int oldLineNo = -1;
        int newLineNo = -1;
        std::string content;
    };

    std::shared_ptr<GitRepository> m_repository;
    GitStatus m_status;
    char m_commitBuf[4096] = {};
    SplitView m_vSplit{ SplitView::Type::Vertical, 0.5f, 80 };
    SplitView m_hSplit{ SplitView::Type::Horizontal, 0.4f, 250 };
    bool m_updating = false;

    bool m_treeView = false;
    bool m_diffOpen = false;
    std::string m_diffFilePath;
    bool m_diffIsStaged = false;
    std::vector<DiffLine> m_diffLines;

    std::set<std::string> m_selectedStagedPaths;
    std::set<std::string> m_selectedUnstagedPaths;

    std::atomic<bool> m_statusLoading{false};
    std::thread m_statusThread;
    std::mutex m_statusMutex;
    GitStatus m_pendingStatus;

    void StartAsyncRefresh();
    void ProcessAsyncResult();
    void BuildTree(const std::vector<GitFileEntry>& files, FileTreeNode& root);

    void RenderStagedArea();
    void RenderUnstagedArea();
    void RenderCommitArea();
    void RenderFileRow(const std::string& path, const std::string& oldPath,
                       GitFileStatus status, bool isStaged);
    void RenderTreeNode(const FileTreeNode& node, bool isStaged);
    void RenderDiff(const std::string& path, bool isStaged);
    void LoadDiff(const std::string& path, bool isStaged);
    std::vector<DiffLine> ParseDiff(const std::string& content);

    const char* StatusIcon(GitFileStatus s) const;
    ImU32 StatusColor(GitFileStatus s) const;
};

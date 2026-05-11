#pragma once

#include <memory>
#include <functional>
#include <string>
#include <vector>
#include <filesystem>
#include "../gitcore/git_types.h"

class GitRepository;

struct FileTreeNode
{
    std::string name;
    std::string fullPath;
    bool isDirectory = false;
    std::vector<FileTreeNode> children;
};

class WorkTreePanel
{
public:
    void Render();
    void SetRepository(std::shared_ptr<GitRepository> repo);
    void Refresh();

    std::function<void(const std::string& filePath)> OnOpenFile;

private:
    std::shared_ptr<GitRepository> m_repository;
    std::vector<FileTreeNode> m_rootNodes;
    bool m_loaded = false;

    void LoadFileTree();
    void LoadDirectory(const std::string& path, std::vector<FileTreeNode>& out);
    void RenderTreeNode(const FileTreeNode& node);
};

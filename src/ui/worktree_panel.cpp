#include "worktree_panel.h"
#include "../gitcore/git_repository.h"
#include <imgui.h>
#include <algorithm>

void WorkTreePanel::SetRepository(std::shared_ptr<GitRepository> repo)
{
    m_repository = std::move(repo);
    m_loaded = false;
    m_rootNodes.clear();
    Refresh();
}

void WorkTreePanel::Refresh()
{
    m_loaded = false;
}

void WorkTreePanel::LoadFileTree()
{
    if (!m_repository) return;
    m_rootNodes.clear();

    std::string rootPath = m_repository->GetRootPath();
    if (rootPath.empty()) return;

    LoadDirectory(rootPath, m_rootNodes);
    m_loaded = true;
}

void WorkTreePanel::LoadDirectory(const std::string& path, std::vector<FileTreeNode>& out)
{
    try
    {
        for (auto& entry : std::filesystem::directory_iterator(path))
        {
            auto filename = entry.path().filename().string();

            // Skip .git directory
            if (filename == ".git" || filename == ".vs" || filename == ".vscode")
                continue;

            FileTreeNode node;
            node.name = filename;
            node.fullPath = entry.path().string();
            node.isDirectory = entry.is_directory();

            if (node.isDirectory)
            {
                LoadDirectory(entry.path().string(), node.children);
            }

            out.push_back(std::move(node));
        }
    }
    catch (...) {}
}

void WorkTreePanel::Render()
{
    if (!m_repository)
    {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "No repository opened");
        return;
    }

    if (!m_loaded)
    {
        ImGui::BeginChild("##wktree_loading", ImVec2(0, 0), true);
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "Loading file tree...");
        ImGui::EndChild();
        LoadFileTree();
        return;
    }

    ImGui::BeginChild("##worktree_content", ImVec2(0, 0), true);

    for (auto& node : m_rootNodes)
    {
        RenderTreeNode(node);
    }

    ImGui::EndChild();
}

void WorkTreePanel::RenderTreeNode(const FileTreeNode& node)
{
    ImGui::PushID(node.fullPath.c_str());

    ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags_OpenOnArrow |
        ImGuiTreeNodeFlags_OpenOnDoubleClick |
        ImGuiTreeNodeFlags_SpanAvailWidth;

    if (!node.isDirectory)
        flags |= ImGuiTreeNodeFlags_Leaf;

    bool open = ImGui::TreeNodeEx(node.name.c_str(), flags);

    if (ImGui::BeginPopupContextItem())
    {
        if (ImGui::MenuItem("Copy Path"))
            ImGui::SetClipboardText(node.fullPath.c_str());
        if (ImGui::MenuItem("Copy Relative Path"))
        {
            std::string rootPath = m_repository->GetRootPath();
            std::string relPath = node.fullPath;
            if (relPath.find(rootPath) == 0)
                relPath = relPath.substr(rootPath.size() + 1);
            ImGui::SetClipboardText(relPath.c_str());
        }
        if (node.isDirectory)
        {
            if (ImGui::MenuItem("Open in File Manager"))
            {
#ifdef _WIN32
                std::string cmd = "explorer \"" + node.fullPath + "\"";
#elif defined(__APPLE__)
                std::string cmd = "open \"" + node.fullPath + "\"";
#else
                std::string cmd = "xdg-open \"" + node.fullPath + "\"";
#endif
                system(cmd.c_str());
            }
        }
        else
        {
            if (ImGui::MenuItem("Open File"))
            {
                if (OnOpenFile) OnOpenFile(node.fullPath);
            }
        }
        ImGui::EndPopup();
    }

    if (open)
    {
        if (node.isDirectory && !node.children.empty())
        {
            for (auto& child : node.children)
            {
                RenderTreeNode(child);
            }
        }
        ImGui::TreePop();
    }

    ImGui::PopID();
}

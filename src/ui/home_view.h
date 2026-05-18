#pragma once

#include <string>
#include <vector>
#include <functional>
#include "SplitView.h"
#include "imgui_markdown.h"

class HomeView
{
public:
    void Render();

    std::function<void()> OnOpenRepository;
    std::function<void(const std::string& path)> OnOpenRecent;
    std::function<void()> OnScanFolder;

    struct RecentRepo
    {
        std::string path;
        std::string name;
    };

    void AddRecent(const std::string& path);
    void SaveRecents(const std::string& filePath);
    void LoadRecents(const std::string& filePath);

private:
    std::vector<RecentRepo> m_recentRepos;
    int m_selectedRepoIndex = -1;
    SplitView m_splitView{ SplitView::Type::Horizontal, 220, 80 };
    std::string m_readmeContent;
    char m_searchFilter[256] = {};

    void RenderLeftPanel();
    void RenderRightPanel();
    void LoadReadme(const std::string& repoPath);
    static void LinkCallback(ImGui::MarkdownLinkCallbackData data);
};

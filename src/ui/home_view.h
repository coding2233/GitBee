#pragma once

#include <string>
#include <vector>
#include <functional>

class HomeView
{
public:
    void Render();

    std::function<void()> OnOpenRepository;
    std::function<void(const std::string& path)> OnOpenRecent;

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
};

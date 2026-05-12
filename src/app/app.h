#pragma once

#include <memory>
#include <string>
#include <vector>
#include <volt-ui/VoltApp.h>
#include "../ui/FileDialog.h"

class RepoView;
class HomeView;

class GitBeeApp : public volt::App
{
public:
    GitBeeApp(const volt::AppConfig& config);
    ~GitBeeApp() override;

    void OpenRepository(const std::string& path);

protected:
    void OnCreate() override;
    void OnRender() override;
    void OnEvent(const SDL_Event& event) override;
    void OnDestroy() override;

private:
    void RenderMenuBar();
    void RenderTabBar();
    void RenderStatusBar();

    // Tabs
    struct RepoTab
    {
        std::shared_ptr<RepoView> view;
        std::string name;
    };
    std::vector<RepoTab> m_repoTabs;
    int m_activeTabIndex = 0;  // 0 = Home, 1+ = repo tabs

    std::unique_ptr<HomeView> m_homeView;
    FileDialog m_fileDialog;

    std::string m_statusMessage = "Ready";
    bool m_showDemoWindow = false;
    std::string m_recentFilePath;
};

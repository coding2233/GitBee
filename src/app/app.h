#pragma once

#include <memory>
#include <string>
#include <vector>
#include <thread>
#include <mutex>
#include <atomic>
#include <volt-ui/VoltApp.h>
#include "../ui/FileDialog.h"

class RepoView;
class HomeView;
class GitRepository;

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
    enum class DialogMode { None, OpenRepo, ScanFolder };

    void RenderMenuBar();
    void RenderTabBar();
    void RenderStatusBar();
    void ScanForRepositories(const std::string& rootPath);

    // Tabs
    struct RepoTab
    {
        std::shared_ptr<RepoView> view;
        std::string name;
    };
    std::vector<RepoTab> m_repoTabs;
    int m_activeTabIndex = 0;  // 0 = Home, 1+ = repo tabs
    bool m_homeTabOpen = true;

    std::unique_ptr<HomeView> m_homeView;
    FileDialog m_fileDialog;
    DialogMode m_dialogMode = DialogMode::None;

    std::string m_statusMessage = "Ready";
    bool m_showDemoWindow = false;
    bool m_globalConfigTabOpen = false;
    std::string m_recentFilePath;

    // Global config state
    struct GlobalConfigEntry {
        std::string key;
        std::string value;
        bool editing = false;
        char editBuf[4096]{};
    };
    std::vector<GlobalConfigEntry> m_globalConfig;
    std::atomic<bool> m_globalConfigLoading{false};
    std::thread m_globalConfigThread;
    bool m_showAddForm = false;
    char m_newConfigKey[256]{};
    char m_newConfigValue[4096]{};

    void LoadGlobalConfig();
    void RenderGlobalConfigTab();

    // Async scan state
    std::atomic<bool> m_scanning{ false };
    std::thread m_scanThread;
    std::mutex m_scanMutex;
    std::vector<std::string> m_scanResults;

    void ProcessScanResults();

    // Async repo opening
    struct PendingRepo;
    std::vector<std::unique_ptr<PendingRepo>> m_pendingRepos;

    void ProcessPendingRepos();
    void StartOpenRepository(const std::string& path);
};

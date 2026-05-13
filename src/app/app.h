#pragma once

#include <memory>
#include <string>
#include <vector>
#include <thread>
#include <mutex>
#include <atomic>
#include <map>
#include <volt-ui/VoltApp.h>
#include "../ui/FileDialog.h"

class RepoView;
class HomeView;
class GitRepository;

struct OperationLogEntry
{
    int64_t id;
    std::string time;
    std::string repoName;
    std::string operation;
    bool success = true;
    std::string summary;
    std::string detail;
    bool expanded = false;
};

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
    std::vector<GlobalConfigEntry> m_systemConfig;
    std::atomic<bool> m_globalConfigLoading{false};
    bool m_globalConfigLoaded = false;
    bool m_globalConfigLoadStarted = false;
    std::thread m_globalConfigThread;
    std::mutex m_globalConfigMutex;
    std::vector<GlobalConfigEntry> m_pendingGlobalConfig;
    std::vector<GlobalConfigEntry> m_pendingSystemConfig;
    bool m_showAddForm = false;
    char m_newConfigKey[256]{};
    char m_newConfigValue[4096]{};
    std::map<std::string, bool> m_globalSectionOpen;

    void LoadGlobalConfig();
    void ProcessGlobalConfigResult();
    void RenderGlobalConfigTab();
    void RenderSectionTableGlobal(const std::string& section, const std::vector<int>& indices);

    std::string GetConfigSection(const std::string& key) const;

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

    // Operation output log
    std::vector<OperationLogEntry> m_operationLog;
    static constexpr int MAX_OP_LOG = 100;
    int64_t m_nextOpId = 1;
    bool m_showOutputWindow = false;
    bool m_autoShowOutput = true;

    // Interactive operation (merge conflict, etc.)
    struct PendingInteraction {
        std::string repoName;
        std::string type;       // "merge", "rebase", "stash", etc.
        std::string title;
        std::string message;
        std::string detail;
        bool hasConflicts = false;
        int conflictCount = 0;
    };
    std::unique_ptr<PendingInteraction> m_pendingInteraction;

    int m_detailPopupIndex = -1;

    void AddOperationLog(const std::string& repoName, const std::string& operation,
                         bool success, const std::string& summary, const std::string& detail);
    void ShowOutputWindow();
    void RenderOutputWindow();
    void RenderDetailPopup();
    void ClearOperationLog();
};

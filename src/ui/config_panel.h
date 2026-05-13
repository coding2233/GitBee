#pragma once

#include <memory>
#include <string>
#include <vector>
#include <atomic>
#include <thread>
#include <mutex>
#include "../gitcore/git_types.h"

class GitRepository;

struct GitConfigEntry
{
    std::string key;
    std::string value;
};

class ConfigPanel
{
public:
    ConfigPanel();
    ~ConfigPanel();
    void Render();
    void SetRepository(std::shared_ptr<GitRepository> repo);
    void Refresh();

private:
    std::shared_ptr<GitRepository> m_repository;
    std::vector<GitConfigEntry> m_localConfig;
    std::vector<GitConfigEntry> m_globalConfig;
    bool m_loaded = false;

    std::atomic<bool> m_configLoading{false};
    std::thread m_configThread;
    std::mutex m_configMutex;
    std::vector<GitConfigEntry> m_pendingLocal;
    std::vector<GitConfigEntry> m_pendingGlobal;

    void StartAsyncLoad();
    void ProcessAsyncResult();
    void RenderTable(const char* title, const std::vector<GitConfigEntry>& entries);
};

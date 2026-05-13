#pragma once

#include <memory>
#include <string>
#include <vector>
#include <atomic>
#include <thread>
#include <mutex>
#include <map>
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
    struct EditableEntry {
        std::string key;
        std::string value;
        bool editing = false;
        char editBuf[4096]{};
    };

    struct SectionData {
        std::string name;
        std::vector<int> entryIndices;
    };

    std::shared_ptr<GitRepository> m_repository;
    std::vector<EditableEntry> m_localConfig;
    std::vector<GitConfigEntry> m_globalConfig;
    bool m_loaded = false;
    bool m_loadStarted = false;

    std::atomic<bool> m_configLoading{false};
    std::thread m_configThread;
    std::mutex m_configMutex;
    std::vector<GitConfigEntry> m_pendingLocal;
    std::vector<GitConfigEntry> m_pendingGlobal;

    char m_newKey[256]{};
    char m_newValue[4096]{};
    bool m_showAddForm = false;

    std::map<std::string, bool> m_sectionOpen;

    void StartAsyncLoad();
    void ProcessAsyncResult();

    std::vector<SectionData> BuildSections();
    std::string GetSectionName(const std::string& key);

    void RenderSummaryBar();
    void RenderSectionTable(const std::string& sectionName, const std::vector<int>& indices);
    void RenderLocalSection();
    void RenderGlobalSection();
    void RenderRemotesSection();

    void CommitEdit(int index, const std::string& newValue);
    void DeleteEntry(int index);
    void AddNewEntry(const std::string& key, const std::string& value);

    std::string FindGlobalValue(const std::string& key) const;
};

#pragma once

#include <memory>
#include <string>
#include <vector>
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
    void Render();
    void SetRepository(std::shared_ptr<GitRepository> repo);
    void Refresh();

private:
    std::shared_ptr<GitRepository> m_repository;
    std::vector<GitConfigEntry> m_localConfig;
    std::vector<GitConfigEntry> m_globalConfig;
    bool m_loaded = false;

    void LoadConfig();
    void RenderTable(const char* title, const std::vector<GitConfigEntry>& entries);
};

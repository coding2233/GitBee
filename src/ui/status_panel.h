#pragma once

#include <memory>
#include "../gitcore/git_types.h"

class GitRepository;

class StatusPanel {
public:
    void Render();
    void SetRepository(std::shared_ptr<GitRepository> repo);
    void Refresh();

private:
    void RenderFileEntry(const GitFileEntry& entry, const char* icon, float r, float g, float b);
    void RenderFileList(const std::vector<GitFileEntry>& files, const char* title, const char* icon, float r, float g, float b);

    std::shared_ptr<GitRepository> m_repository;
    GitStatus m_status;
};

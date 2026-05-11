#pragma once

#include <memory>
#include <imgui.h>
#include "../gitcore/git_types.h"

class GitRepository;

class StatusPanel
{
public:
    void Render();
    void SetRepository(std::shared_ptr<GitRepository> repo);
    void Refresh();

private:
    void RenderFileEntry(const GitFileEntry& entry, const char* icon, ImU32 color);
    void RenderFileList(const std::vector<GitFileEntry>& files, const char* title, const char* icon, ImU32 color);
    void RenderBranchInfo();
    void RenderAheadBehind();

    std::shared_ptr<GitRepository> m_repository;
    GitStatus m_status;
};

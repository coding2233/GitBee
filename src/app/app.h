#pragma once

#include <memory>
#include <string>
#include <volt-ui/VoltApp.h>
#include "../ui/FileDialog.h"

class GitRepository;
class StatusPanel;
class LogPanel;
class DiffPanel;
class LayoutManager;

class GitBeeApp : public volt::App
{
public:
    GitBeeApp(const volt::AppConfig& config);
    ~GitBeeApp() override;

    void OpenRepository(const std::string& path);
    void SetRepository(std::shared_ptr<GitRepository> repo);

protected:
    void OnCreate() override;
    void OnRender() override;
    void OnEvent(const SDL_Event& event) override;
    void OnDestroy() override;

private:
    void RenderMenuBar();
    void RenderStatusBar();

    std::shared_ptr<GitRepository> m_repository;
    std::unique_ptr<StatusPanel> m_statusPanel;
    std::unique_ptr<LogPanel> m_logPanel;
    std::unique_ptr<DiffPanel> m_diffPanel;
    std::unique_ptr<LayoutManager> m_layoutMgr;

    std::string m_statusMessage = "Ready";
    bool m_showDemoWindow = false;

    FileDialog m_fileDialog;
    bool m_showFileDialog = false;
};

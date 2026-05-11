#pragma once

#include <memory>
#include <string>
#include <volt-ui/VoltApp.h>

class GitRepository;
class StatusPanel;
class LogPanel;
class DiffPanel;

class GitBeeApp : public volt::App {
public:
    GitBeeApp(const volt::AppConfig& config);
    ~GitBeeApp() override;

    void OpenRepository(const std::string& path);
    void SetRepository(std::shared_ptr<GitRepository> repo);

protected:
    void OnCreate() override;
    void OnRender() override;
    void OnEvent(const SDL_Event& event) override;

private:
    void RenderMenuBar();
    void RenderStatusBar();
    void RenderLeftPanel();
    void RenderCenterPanel();
    void RenderRightPanel();

    std::shared_ptr<GitRepository> m_repository;
    std::unique_ptr<StatusPanel> m_statusPanel;
    std::unique_ptr<LogPanel> m_logPanel;
    std::unique_ptr<DiffPanel> m_diffPanel;

    float m_leftPanelWidth = 280.0f;
    float m_rightPanelWidth = 380.0f;

    std::string m_statusMessage = "Ready";
    bool m_showDemoWindow = false;
};

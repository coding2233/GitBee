#include "app.h"
#include "../ui/status_panel.h"
#include "../ui/log_panel.h"
#include "../ui/diff_panel.h"
#include "../gitcore/git_repository.h"
#include <imgui.h>
#include <algorithm>

GitBeeApp::GitBeeApp(const volt::AppConfig& config) : volt::App(config) {}
GitBeeApp::~GitBeeApp() = default;

void GitBeeApp::OpenRepository(const std::string& path)
{
    auto repo = GitRepository::Open(path);
    SetRepository(repo);
}

void GitBeeApp::SetRepository(std::shared_ptr<GitRepository> repo)
{
    m_repository = repo;
    if (m_statusPanel) m_statusPanel->SetRepository(repo);
    if (m_logPanel) m_logPanel->SetRepository(repo);
    if (m_diffPanel) m_diffPanel->SetRepository(repo);
    if (repo) {
        m_statusMessage = "Opened: " + repo->GetRootPath();
    } else {
        m_statusMessage = "Failed to open repository";
    }
}

void GitBeeApp::OnCreate()
{
    SetClearColor({0.12f, 0.12f, 0.15f, 1.0f});
    SDL_SetWindowMinimumSize(GetWindow(), 800, 600);

    m_statusPanel = std::make_unique<StatusPanel>();
    m_logPanel = std::make_unique<LogPanel>();
    m_diffPanel = std::make_unique<DiffPanel>();

    m_logPanel->OnCommitSelected = [this](const GitCommit& commit) {
        m_diffPanel->ShowCommitDetail(commit);
        m_statusMessage = "Selected: " + commit.shortHash + " - " + commit.message;
    };
}

void GitBeeApp::RenderMenuBar()
{
    if (ImGui::BeginMainMenuBar()) {
        if (ImGui::BeginMenu("File")) {
            if (ImGui::MenuItem("Open Repository...", "Ctrl+O")) {
            }
            ImGui::Separator();
            if (ImGui::MenuItem("Refresh", "F5")) {
                if (m_statusPanel) m_statusPanel->Refresh();
                if (m_logPanel) m_logPanel->Refresh();
                m_statusMessage = "Refreshed";
            }
            ImGui::Separator();
            if (ImGui::MenuItem("Exit", "Alt+F4")) {
                Quit();
            }
            ImGui::EndMenu();
        }

        if (ImGui::BeginMenu("View")) {
            ImGui::MenuItem("ImGui Demo", nullptr, &m_showDemoWindow);
            ImGui::EndMenu();
        }

        if (ImGui::BeginMenu("Help")) {
            if (ImGui::MenuItem("About GitBee")) {
                m_statusMessage = "GitBee - Git Repository Browser";
            }
            ImGui::EndMenu();
        }

        ImGui::EndMainMenuBar();
    }
}

void GitBeeApp::RenderStatusBar()
{
    ImGuiViewport* viewport = ImGui::GetMainViewport();
    float statusBarHeight = ImGui::GetFrameHeight();

    ImGui::SetNextWindowPos(ImVec2(viewport->Pos.x, viewport->Pos.y + viewport->Size.y - statusBarHeight));
    ImGui::SetNextWindowSize(ImVec2(viewport->Size.x, statusBarHeight));

    ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 0.0f);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0.0f);

    ImGui::Begin("##StatusBar", nullptr,
        ImGuiWindowFlags_NoDecoration |
        ImGuiWindowFlags_NoScrollbar |
        ImGuiWindowFlags_NoSavedSettings);

    ImGui::TextUnformatted(m_statusMessage.c_str());

    if (m_repository) {
        std::string branchInfo = "Branch: " + m_repository->GetCurrentBranch();
        float textWidth = ImGui::CalcTextSize(branchInfo.c_str()).x;
        ImGui::SameLine(ImGui::GetWindowWidth() - textWidth - 10.0f);
        ImGui::TextUnformatted(branchInfo.c_str());
    }

    ImGui::End();
    ImGui::PopStyleVar(2);
}

void GitBeeApp::RenderLeftPanel()
{
    ImGui::Begin("Repository Status", nullptr,
        ImGuiWindowFlags_NoCollapse | ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoResize);

    if (m_repository) {
        if (ImGui::Button("Refresh")) {
            if (m_statusPanel) m_statusPanel->Refresh();
            if (m_logPanel) m_logPanel->Refresh();
            m_statusMessage = "Refreshed";
        }
        ImGui::SameLine();
        if (ImGui::Button("Open...")) {
        }
        if (m_statusPanel) {
            m_statusPanel->Render();
        }
    } else {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "No repository opened");
        ImGui::TextUnformatted("Open a repository via");
        ImGui::TextUnformatted("File > Open Repository...");
        ImGui::TextUnformatted("or pass path as CLI arg.");
    }
    ImGui::End();
}

void GitBeeApp::RenderCenterPanel()
{
    if (m_logPanel) {
        m_logPanel->Render();
    }
}

void GitBeeApp::RenderRightPanel()
{
    if (m_diffPanel) {
        m_diffPanel->Render();
    }
}

void GitBeeApp::OnRender()
{
    if (m_showDemoWindow) {
        ImGui::ShowDemoWindow(&m_showDemoWindow);
    }

    if (GetConfig().use_topbar) {
        ImGui::SetNextWindowPos(ImVec2(0, GetTopbarHeight()), ImGuiCond_Once);
        ImGui::SetNextWindowSize(
            ImVec2(GetConfig().width, GetConfig().height - GetTopbarHeight()), ImGuiCond_Once);
    }

    RenderMenuBar();

    ImGuiViewport* viewport = ImGui::GetMainViewport();
    float menuBarHeight = ImGui::GetFrameHeight();
    float statusBarHeight = ImGui::GetFrameHeight();
    float availableHeight = viewport->Size.y - menuBarHeight - statusBarHeight;
    float availableWidth = viewport->Size.x;

    float leftW = m_leftPanelWidth;
    float rightW = m_rightPanelWidth;
    float centerW = availableWidth - leftW - rightW;

    float yStart = menuBarHeight;

    // --- Left Panel ---
    ImGui::SetNextWindowPos(ImVec2(0, yStart));
    ImGui::SetNextWindowSize(ImVec2(leftW, availableHeight));
    RenderLeftPanel();

    // --- Splitter 1 (left/center) ---
    float splitterX1 = leftW;
    ImGui::SetCursorScreenPos(ImVec2(splitterX1 - 3.0f, yStart));
    ImGui::InvisibleButton("##Splitter1Btn", ImVec2(6.0f, availableHeight));
    if (ImGui::IsItemActive()) {
        m_leftPanelWidth += ImGui::GetIO().MouseDelta.x;
    }
    m_leftPanelWidth = std::clamp(m_leftPanelWidth, 100.0f, availableWidth - 300.0f);
    if (ImGui::IsItemHovered()) ImGui::SetMouseCursor(ImGuiMouseCursor_ResizeEW);

    // --- Center Panel ---
    float centerX = splitterX1;
    ImGui::SetNextWindowPos(ImVec2(centerX, yStart));
    ImGui::SetNextWindowSize(ImVec2(centerW, availableHeight));
    RenderCenterPanel();

    // --- Splitter 2 (center/right) ---
    float splitterX2 = centerX + centerW;
    ImGui::SetCursorScreenPos(ImVec2(splitterX2 - 3.0f, yStart));
    ImGui::InvisibleButton("##Splitter2Btn", ImVec2(6.0f, availableHeight));
    if (ImGui::IsItemActive()) {
        m_rightPanelWidth -= ImGui::GetIO().MouseDelta.x;
    }
    m_rightPanelWidth = std::clamp(m_rightPanelWidth, 200.0f, availableWidth - 300.0f);
    if (ImGui::IsItemHovered()) ImGui::SetMouseCursor(ImGuiMouseCursor_ResizeEW);

    // --- Right Panel ---
    float rightX = splitterX2;
    ImGui::SetNextWindowPos(ImVec2(rightX, yStart));
    ImGui::SetNextWindowSize(ImVec2(rightW, availableHeight));
    RenderRightPanel();

    // --- Status Bar ---
    RenderStatusBar();
}

void GitBeeApp::OnEvent(const SDL_Event& event)
{
    if (event.type == SDL_EVENT_KEY_DOWN) {
        if (event.key.key == SDLK_ESCAPE) {
            Quit();
        }
        if (event.key.key == SDLK_F5) {
            if (m_statusPanel) m_statusPanel->Refresh();
            if (m_logPanel) m_logPanel->Refresh();
            m_statusMessage = "Refreshed";
        }
    }
}

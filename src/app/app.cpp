#include "app.h"
#include "../ui/status_panel.h"
#include "../ui/log_panel.h"
#include "../ui/diff_panel.h"
#include "../ui/LayoutManager.h"
#include "../ui/Theme.h"
#include "../gitcore/git_repository.h"
#include <imgui.h>

GitBeeApp::GitBeeApp(const volt::AppConfig& config) : volt::App(config)
{
    m_statusPanel = std::make_unique<StatusPanel>();
    m_logPanel = std::make_unique<LogPanel>();
    m_diffPanel = std::make_unique<DiffPanel>();
    m_layoutMgr = std::make_unique<LayoutManager>();

    m_logPanel->OnCommitSelected = [this](const GitCommit& commit) {
        m_diffPanel->ShowCommitDetail(commit);
        m_statusMessage = "Selected: " + commit.shortHash + " - " + commit.message;
    };
}
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

    ImGui::GetIO().ConfigFlags |= ImGuiConfigFlags_DockingEnable;

    Theme::ApplyDark();
    Theme::LoadFonts();

    m_layoutMgr->Init();
}

void GitBeeApp::OnDestroy()
{
    m_layoutMgr->Shutdown();
}

void GitBeeApp::RenderMenuBar()
{
    auto* viewport = ImGui::GetMainViewport();
    float topbar_h = GetTopbarHeight();

    ImGui::SetNextWindowPos(ImVec2(viewport->Pos.x, viewport->Pos.y + topbar_h));
    ImGui::SetNextWindowSize(ImVec2(viewport->Size.x, ImGui::GetFrameHeight()));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 0);

    ImGui::Begin("##MenuBar", nullptr,
        ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize |
        ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoScrollbar |
        ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_MenuBar);

    if (ImGui::BeginMenuBar()) {
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
            if (ImGui::MenuItem("Reset Layout")) {
                m_layoutMgr->ResetLayout();
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

        ImGui::EndMenuBar();
    }

    m_layoutMgr->SetMenuBarHeight(ImGui::GetWindowHeight());
    ImGui::End();
    ImGui::PopStyleVar(2);
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

void GitBeeApp::OnRender()
{
    if (m_showDemoWindow) {
        ImGui::ShowDemoWindow(&m_showDemoWindow);
    }

    RenderMenuBar();

    m_layoutMgr->BeginFrame();

    // Left panel: repo status
    if (m_repository) {
        ImGui::Begin("Repository Status");
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
        ImGui::End();
    } else {
        ImGui::Begin("Repository Status");
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "No repository opened");
        ImGui::TextUnformatted("Open a repository via");
        ImGui::TextUnformatted("File > Open Repository...");
        ImGui::TextUnformatted("or pass path as CLI arg.");
        ImGui::End();
    }

    // Center panel: commit log
    if (m_logPanel) {
        m_logPanel->Render();
    }

    // Right panel: diff view
    if (m_diffPanel) {
        m_diffPanel->Render();
    }

    m_layoutMgr->EndFrame();

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

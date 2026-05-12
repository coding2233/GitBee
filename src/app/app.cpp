#include "app.h"
#include "repo_view.h"
#include "../ui/home_view.h"
#include "../ui/Theme.h"
#include "../gitcore/git_repository.h"
#include <imgui.h>
#include <cstdlib>
#include <filesystem>

GitBeeApp::GitBeeApp(const volt::AppConfig& config) : volt::App(config)
{
    const char* appData = std::getenv("APPDATA");
    std::string dataDir = appData ? (std::string(appData) + "\\GitBee") : (std::filesystem::current_path().string() + "\\GitBee");
    std::filesystem::create_directories(dataDir);
    m_recentFilePath = dataDir + "\\recent.json";

    m_homeView = std::make_unique<HomeView>();
    m_homeView->LoadRecents(m_recentFilePath);
    m_homeView->OnOpenRepository = [this]() {
        m_fileDialog.OpenDialog(FileDialog::Type::SelectFolder);
    };
    m_homeView->OnOpenRecent = [this](const std::string& path) {
        OpenRepository(path);
    };
}

GitBeeApp::~GitBeeApp() = default;

void GitBeeApp::OpenRepository(const std::string& path)
{
    for (auto& tab : m_repoTabs)
    {
        if (tab.view && tab.view->GetPath() == path)
        {
            m_activeTabIndex = (int)(&tab - &m_repoTabs[0]) + 1;
            m_statusMessage = "Already opened: " + path;
            return;
        }
    }

    auto repo = GitRepository::Open(path);
    if (!repo) { m_statusMessage = "Failed to open: " + path; return; }

    auto view = std::make_shared<RepoView>(repo);
    m_repoTabs.push_back({view, view->GetName()});
    m_activeTabIndex = (int)m_repoTabs.size();
    m_statusMessage = "Opened: " + repo->GetRootPath();

    if (m_homeView)
    {
        m_homeView->AddRecent(repo->GetRootPath());
        m_homeView->SaveRecents(m_recentFilePath);
    }
}

void GitBeeApp::OnCreate()
{
    SetClearColor({0.12f, 0.12f, 0.15f, 1.0f});
    SDL_SetWindowMinimumSize(GetWindow(), 800, 600);
    Theme::ApplyDark();
    Theme::LoadFonts();
}

void GitBeeApp::OnDestroy()
{
    if (m_homeView)
        m_homeView->SaveRecents(m_recentFilePath);
}

void GitBeeApp::RenderMenuBar()
{
    auto* vp = ImGui::GetMainViewport();
    float topbar_h = GetTopbarHeight();

    ImGui::SetNextWindowPos(ImVec2(vp->Pos.x, vp->Pos.y + topbar_h));
    ImGui::SetNextWindowSize(ImVec2(vp->Size.x, ImGui::GetFrameHeight()));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 0);

    ImGui::Begin("##MenuBar", nullptr,
        ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize |
        ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoScrollbar |
        ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_MenuBar);

    if (ImGui::BeginMenuBar())
    {
        if (ImGui::BeginMenu("File"))
        {
            if (ImGui::MenuItem("Open Repository...", "Ctrl+O"))
                m_fileDialog.OpenDialog(FileDialog::Type::SelectFolder);
            ImGui::Separator();
            if (ImGui::MenuItem("Exit", "Alt+F4"))
                Quit();
            ImGui::EndMenu();
        }
        if (ImGui::BeginMenu("View"))
        {
            ImGui::MenuItem("ImGui Demo", nullptr, &m_showDemoWindow);
            ImGui::EndMenu();
        }
        if (ImGui::BeginMenu("Help"))
        {
            if (ImGui::MenuItem("About GitBee"))
                m_statusMessage = "GitBee - Git Repository Browser";
            ImGui::EndMenu();
        }
        ImGui::EndMenuBar();
    }

    ImGui::End();
    ImGui::PopStyleVar(2);
}

void GitBeeApp::RenderStatusBar()
{
    ImGuiViewport* vp = ImGui::GetMainViewport();
    float h = ImGui::GetFrameHeight();

    ImGui::SetNextWindowPos(ImVec2(vp->Pos.x, vp->Pos.y + vp->Size.y - h));
    ImGui::SetNextWindowSize(ImVec2(vp->Size.x, h));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 0.0f);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0.0f);

    ImGui::Begin("##StatusBar", nullptr,
        ImGuiWindowFlags_NoDecoration | ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoSavedSettings);
    ImGui::TextUnformatted(m_statusMessage.c_str());
    ImGui::End();
    ImGui::PopStyleVar(2);
}

void GitBeeApp::OnRender()
{
    if (m_showDemoWindow)
        ImGui::ShowDemoWindow(&m_showDemoWindow);

    RenderMenuBar();

    auto* vp = ImGui::GetMainViewport();
    float topbarH = GetTopbarHeight();
    float menuH = ImGui::GetFrameHeight();
    float statusH = ImGui::GetFrameHeight();
    float y = topbarH + menuH;

    ImGui::SetNextWindowPos(ImVec2(vp->Pos.x, y));
    ImGui::SetNextWindowSize(ImVec2(vp->Size.x, vp->Size.y - y - statusH));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 0);

    // Main window with embedded tab bar + content
    ImGui::Begin("##MainContent", nullptr,
        ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize |
        ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoScrollbar |
        ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoBringToFrontOnFocus);

    ImGuiTabBarFlags tabFlags = ImGuiTabBarFlags_FittingPolicyScroll |
        ImGuiTabBarFlags_AutoSelectNewTabs | ImGuiTabBarFlags_Reorderable;

    if (ImGui::BeginTabBar("##MainTabs", tabFlags))
    {
        int realActive = 0;

        // Home tab (always present, non-closable)
        {
            bool homeOpen = true;
            if (ImGui::BeginTabItem("Home", &homeOpen))
            {
                realActive = 0;
                m_homeView->Render();
                ImGui::EndTabItem();
            }
        }

        // Repo tabs
        int closeIdx = -1;
        for (int i = 0; i < (int)m_repoTabs.size(); i++)
        {
            auto& tab = m_repoTabs[i];
            bool open = true;

            if (ImGui::BeginTabItem(tab.name.c_str(), &open))
            {
                realActive = i + 1;
                if (tab.view) tab.view->Render();
                ImGui::EndTabItem();
            }

            if (!open) closeIdx = i;
        }

        // Handle tab close after iteration
        if (closeIdx >= 0)
            m_repoTabs.erase(m_repoTabs.begin() + closeIdx);

        // Remember which tab was active
        m_activeTabIndex = realActive;

        ImGui::EndTabBar();
    }

    ImGui::End();
    ImGui::PopStyleVar(2);

    RenderStatusBar();

    // File dialog overlay
    if (m_fileDialog.open)
    {
        if (m_fileDialog.Render())
        {
            std::string result = m_fileDialog.resultBuffer;
            if (!result.empty())
            {
                OpenRepository(result);
            }
        }
    }
}

void GitBeeApp::OnEvent(const SDL_Event& event)
{
    if (event.type == SDL_EVENT_KEY_DOWN)
    {
        if (event.key.key == SDLK_ESCAPE)
            Quit();
    }
}

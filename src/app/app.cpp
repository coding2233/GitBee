#include "app.h"
#include "repo_view.h"
#include "../ui/home_view.h"
#include "../ui/Theme.h"
#include "../gitcore/git_repository.h"
#include <imgui.h>

GitBeeApp::GitBeeApp(const volt::AppConfig& config) : volt::App(config)
{
    m_homeView = std::make_unique<HomeView>();
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
    auto repo = GitRepository::Open(path);
    if (!repo) { m_statusMessage = "Failed to open: " + path; return; }

    auto view = std::make_shared<RepoView>(repo);
    m_repoTabs.push_back({view, view->GetName()});

    // Switch to the new tab
    m_activeTabIndex = (int)m_repoTabs.size();
    m_statusMessage = "Opened: " + repo->GetRootPath();
}

void GitBeeApp::OnCreate()
{
    SetClearColor({0.12f, 0.12f, 0.15f, 1.0f});
    SDL_SetWindowMinimumSize(GetWindow(), 800, 600);
    Theme::ApplyDark();
    Theme::LoadFonts();
}

void GitBeeApp::OnDestroy() {}

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
            if (ImGui::MenuItem("Home"))
                m_activeTabIndex = 0;
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

void GitBeeApp::RenderTabBar()
{
    if (m_repoTabs.empty()) return;

    float menuH = ImGui::GetFrameHeight();
    float topbarH = GetTopbarHeight();
    float y = topbarH + menuH;

    auto* vp = ImGui::GetMainViewport();
    ImGui::SetNextWindowPos(ImVec2(vp->Pos.x, y));
    ImGui::SetNextWindowSize(ImVec2(vp->Size.x, ImGui::GetFrameHeight()));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 0);

    ImGui::Begin("##TabBar", nullptr,
        ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize |
        ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoScrollbar |
        ImGuiWindowFlags_NoSavedSettings);

    if (ImGui::BeginTabBar("##MainTabs", ImGuiTabBarFlags_FittingPolicyScroll |
        ImGuiTabBarFlags_AutoSelectNewTabs | ImGuiTabBarFlags_Reorderable))
    {
        // Home tab
        ImGuiTabItemFlags homeFlags = (m_activeTabIndex == 0)
            ? ImGuiTabItemFlags_SetSelected : 0;
        bool homeOpen = true;
        if (ImGui::BeginTabItem("Home", &homeOpen, homeFlags))
        {
            m_activeTabIndex = 0;
            ImGui::EndTabItem();
        }

        // Repo tabs
        for (int i = 0; i < (int)m_repoTabs.size(); i++)
        {
            auto& tab = m_repoTabs[i];
            bool open = true;
            ImGuiTabItemFlags flags = (m_activeTabIndex == i + 1)
                ? ImGuiTabItemFlags_SetSelected : 0;

            if (ImGui::BeginTabItem(tab.name.c_str(), &open, flags))
            {
                m_activeTabIndex = i + 1;
                ImGui::EndTabItem();
            }

            if (!open)
            {
                m_repoTabs.erase(m_repoTabs.begin() + i);
                if (m_activeTabIndex >= (int)m_repoTabs.size() + 1)
                    m_activeTabIndex = 0;
                i--;
            }
        }
        ImGui::EndTabBar();
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
    RenderTabBar();

    auto* vp = ImGui::GetMainViewport();
    float topbarH = GetTopbarHeight();
    float menuH = ImGui::GetFrameHeight();
    float tabH = m_repoTabs.empty() ? 0.0f : ImGui::GetFrameHeight();
    float statusH = ImGui::GetFrameHeight();
    float y = topbarH + menuH + tabH;

    ImGui::SetNextWindowPos(ImVec2(vp->Pos.x, y));
    ImGui::SetNextWindowSize(ImVec2(vp->Size.x, vp->Size.y - y - statusH));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0);
    ImGui::PushStyleVar(ImGuiStyleVar_WindowRounding, 0);

    ImGui::Begin("##MainContent", nullptr,
        ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize |
        ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoScrollbar |
        ImGuiWindowFlags_NoSavedSettings);

    if (m_activeTabIndex == 0)
    {
        m_homeView->Render();
    }
    else
    {
        int idx = m_activeTabIndex - 1;
        if (idx >= 0 && idx < (int)m_repoTabs.size() && m_repoTabs[idx].view)
        {
            m_repoTabs[idx].view->Render();
        }
        else
        {
            m_activeTabIndex = 0;
        }
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

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
        m_dialogMode = DialogMode::OpenRepo;
        m_fileDialog.OpenDialog(FileDialog::Type::SelectFolder);
    };
    m_homeView->OnOpenRecent = [this](const std::string& path) {
        OpenRepository(path);
    };
    m_homeView->OnScanFolder = [this]() {
        m_dialogMode = DialogMode::ScanFolder;
        m_fileDialog.OpenDialog(FileDialog::Type::SelectFolder);
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
    if (m_scanning)
    {
        m_scanning = false;
        if (m_scanThread.joinable())
            m_scanThread.join();
    }

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
            {
                m_dialogMode = DialogMode::OpenRepo;
                m_fileDialog.OpenDialog(FileDialog::Type::SelectFolder);
            }
            if (ImGui::MenuItem("Scan for Repositories..."))
            {
                m_dialogMode = DialogMode::ScanFolder;
                m_fileDialog.OpenDialog(FileDialog::Type::SelectFolder);
            }
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

    ProcessScanResults();
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
                if (m_dialogMode == DialogMode::ScanFolder)
                    ScanForRepositories(result);
                else
                    OpenRepository(result);
            }
            m_dialogMode = DialogMode::None;
        }
    }


}

void GitBeeApp::ScanForRepositories(const std::string& rootPath)
{
    if (m_scanning) return;

    m_scanning = true;
    m_statusMessage = "Scanning for repositories...";

    m_scanThread = std::thread([this, rootPath]()
    {
        std::vector<std::string> found;
        constexpr int MAX_DEPTH = 10;

        try
        {
            std::error_code ec;
            auto it = std::filesystem::recursive_directory_iterator(
                rootPath,
                std::filesystem::directory_options::skip_permission_denied,
                ec);
            auto end = std::filesystem::recursive_directory_iterator{};
            for (; it != end; it.increment(ec))
            {
                if (m_scanning.load() == false)
                    return;

                if (ec)
                {
                    ec.clear();
                    continue;
                }

                if (it.depth() >= MAX_DEPTH)
                {
                    it.disable_recursion_pending();
                    continue;
                }

                auto filename = it->path().filename().string();
                if (filename == ".git")
                {
                    found.push_back(it->path().parent_path().string());
                    it.disable_recursion_pending();
                    continue;
                }

                // Skip common system dirs to speed up scan
                if (it.depth() == 0 && (filename == "Windows" || filename == "$Recycle.Bin"
                    || filename == "System Volume Information" || filename == "Program Files"
                    || filename == "Program Files (x86)" || filename == "WinSxS"))
                {
                    it.disable_recursion_pending();
                    continue;
                }
            }
        }
        catch (...) {}

        {
            std::lock_guard<std::mutex> lock(m_scanMutex);
            m_scanResults = std::move(found);
        }
        m_scanning = false;
    });
    m_scanThread.detach();
}

void GitBeeApp::ProcessScanResults()
{
    if (m_scanning) return;

    std::vector<std::string> results;
    {
        std::lock_guard<std::mutex> lock(m_scanMutex);
        results.swap(m_scanResults);
    }

    if (results.empty())
    {
        if (m_statusMessage == "Scanning for repositories...")
            m_statusMessage = "No repositories found";
        return;
    }

    for (auto& path : results)
    {
        if (m_homeView)
            m_homeView->AddRecent(path);
    }

    if (m_homeView)
        m_homeView->SaveRecents(m_recentFilePath);

    m_statusMessage = "Scan complete: found " + std::to_string(results.size()) + " repositories";
}

void GitBeeApp::OnEvent(const SDL_Event& event)
{
    if (event.type == SDL_EVENT_KEY_DOWN)
    {
        if (event.key.key == SDLK_ESCAPE)
            Quit();
    }
}

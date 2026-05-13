#include "app.h"
#include "repo_view.h"
#include "../ui/home_view.h"
#include "../ui/Theme.h"
#include "../ui/LoadingSpinner.h"
#include "../gitcore/git_repository.h"
#include "../gitcore/git_process.h"
#include <imgui.h>
#include <cstdlib>
#include <filesystem>
#include <sstream>

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
        StartOpenRepository(path);
    };
    m_homeView->OnScanFolder = [this]() {
        m_dialogMode = DialogMode::ScanFolder;
        m_fileDialog.OpenDialog(FileDialog::Type::SelectFolder);
    };
}

GitBeeApp::~GitBeeApp()
{
    if (m_globalConfigThread.joinable())
        m_globalConfigThread.join();
}

void GitBeeApp::OpenRepository(const std::string& path)
{
    StartOpenRepository(path);
}

struct GitBeeApp::PendingRepo {
    std::string path;
    std::string displayName;
    std::atomic<bool> loading{true};
    std::shared_ptr<GitRepository> result;
    std::thread worker;
};

void GitBeeApp::StartOpenRepository(const std::string& path)
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

    auto pending = std::make_unique<PendingRepo>();
    pending->path = path;
    std::string resolvedPath = path;
    while (!resolvedPath.empty() && (resolvedPath.back() == '/' || resolvedPath.back() == '\\'))
        resolvedPath.pop_back();
    auto slash = resolvedPath.find_last_of("\\/");
    pending->displayName = (slash != std::string::npos) ? resolvedPath.substr(slash + 1) : resolvedPath;

    m_pendingRepos.push_back(std::move(pending));
    auto* pRepo = m_pendingRepos.back().get();

    m_statusMessage = "Opening repository: " + path + "...";

    pRepo->worker = std::thread([this, pRepo, path]() {
        std::string resolved = path;
        while (!resolved.empty() && (resolved.back() == '/' || resolved.back() == '\\'))
            resolved.pop_back();
        if (resolved.size() >= 5) {
            std::string suffix = resolved.substr(resolved.size() - 4);
            if (suffix == ".git" || suffix == ".GIT") {
                char sep = resolved[resolved.size() - 5];
                if (sep == '/' || sep == '\\') resolved.resize(resolved.size() - 5);
            }
        }

        auto tmp = std::make_shared<GitRepository>(resolved);
        if (tmp->IsValid())
        {
            std::string root = tmp->GetRootPath();
            if (!root.empty()) resolved = root;
            auto repo = std::make_shared<GitRepository>(resolved);
            if (repo->IsValid())
            {
                pRepo->result = repo;
            }
        }
        pRepo->loading = false;
    });
    pRepo->worker.detach();
}

void GitBeeApp::ProcessPendingRepos()
{
    if (m_pendingRepos.empty()) return;

    for (auto it = m_pendingRepos.begin(); it != m_pendingRepos.end(); )
    {
        auto& pending = *it;

        if (pending->loading)
        {
            ++it;
            continue;
        }

        if (pending->worker.joinable())
            pending->worker.join();

        if (pending->result)
        {
            auto repo = pending->result;
            auto view = std::make_shared<RepoView>(repo);
            view->OnStatusMessage = [this](const std::string& msg) {
                m_statusMessage = msg;
            };
            m_repoTabs.push_back({view, pending->displayName});
            m_activeTabIndex = (int)m_repoTabs.size();
            m_statusMessage = "Opened: " + repo->GetRootPath();

            if (m_homeView)
            {
                m_homeView->AddRecent(repo->GetRootPath());
                m_homeView->SaveRecents(m_recentFilePath);
            }
        }
        else
        {
            m_statusMessage = "Failed to open: " + pending->path;
        }

        it = m_pendingRepos.erase(it);
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

    for (auto& p : m_pendingRepos)
    {
        if (p->worker.joinable())
            p->worker.join();
    }

    if (m_globalConfigThread.joinable())
        m_globalConfigThread.join();

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
            if (ImGui::MenuItem("Home", "Ctrl+H", nullptr, !m_homeTabOpen))
            {
                m_homeTabOpen = true;
                m_activeTabIndex = 0;
            }
            ImGui::Separator();
            if (ImGui::MenuItem("Global Config"))
            {
                m_globalConfigTabOpen = true;
                if (m_globalConfig.empty())
                    LoadGlobalConfig();
            }
            ImGui::Separator();
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

    bool anyLoading = false;
    for (auto& p : m_pendingRepos) { if (p->loading) anyLoading = true; }
    if (m_scanning || anyLoading || m_globalConfigLoading)
    {
        LoadingSpinner(6.0f, 2.0f);
        ImGui::SameLine();
    }

    ImGui::TextUnformatted(m_statusMessage.c_str());
    ImGui::End();
    ImGui::PopStyleVar(2);
}

void GitBeeApp::LoadGlobalConfig()
{
    if (m_globalConfigLoading) return;
    m_globalConfigLoading = true;
    m_statusMessage = "Loading global config...";

    m_globalConfigThread = std::thread([this]() {
        std::vector<GlobalConfigEntry> entries;
        auto r = GitProcess::Execute("", {"config", "--global", "--list"});
        if (r.ok && !r.out.empty())
        {
            std::istringstream stream(r.out);
            std::string line;
            while (std::getline(stream, line))
            {
                auto eq = line.find('=');
                if (eq != std::string::npos)
                {
                    GlobalConfigEntry entry;
                    entry.key = line.substr(0, eq);
                    entry.value = line.substr(eq + 1);
                    entry.editing = false;
                    entry.editBuf[0] = '\0';
                    entries.push_back(std::move(entry));
                }
            }
        }

        {
            std::lock_guard<std::mutex> lock(m_scanMutex);
            m_globalConfig = std::move(entries);
        }
        m_globalConfigLoading = false;
        m_statusMessage = "Global config loaded";
    });
    m_globalConfigThread.detach();
}

void GitBeeApp::RenderGlobalConfigTab()
{
    if (!m_globalConfigLoading && m_globalConfigThread.joinable())
        m_globalConfigThread.join();

    if (m_globalConfigLoading)
    {
        LoadingSpinnerWithText("Loading global config...");
        return;
    }

    // Toolbar
    ImGui::BeginChild("##config_toolbar", ImVec2(0, ImGui::GetFrameHeight() + 8), false);
    if (ImGui::Button("+ Add"))
        m_showAddForm = !m_showAddForm;
    ImGui::SameLine();
    if (ImGui::Button("Refresh"))
    {
        m_globalConfig.clear();
        LoadGlobalConfig();
    }
    ImGui::SameLine();
    ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "%d entries", (int)m_globalConfig.size());

    if (m_showAddForm)
    {
        ImGui::Separator();
        ImGui::TextUnformatted("New Config:");
        ImGui::SameLine();
        ImGui::PushItemWidth(200);
        ImGui::InputText("##newkey", m_newConfigKey, sizeof(m_newConfigKey));
        ImGui::PopItemWidth();
        ImGui::SameLine();
        ImGui::TextUnformatted("=");
        ImGui::SameLine();
        ImGui::PushItemWidth(300);
        ImGui::InputText("##newval", m_newConfigValue, sizeof(m_newConfigValue));
        ImGui::PopItemWidth();
        ImGui::SameLine();
        if (ImGui::Button("Save") && m_newConfigKey[0] != '\0')
        {
            std::string key(m_newConfigKey);
            std::string val(m_newConfigValue);
            std::thread([key, val]() {
                GitProcess::Execute("", {"config", "--global", key, val});
            }).detach();
            GlobalConfigEntry entry;
            entry.key = key;
            entry.value = val;
            entry.editing = false;
            entry.editBuf[0] = '\0';
            m_globalConfig.push_back(std::move(entry));
            m_newConfigKey[0] = '\0';
            m_newConfigValue[0] = '\0';
            m_showAddForm = false;
            m_statusMessage = "Added config: " + key;
        }
        ImGui::SameLine();
        if (ImGui::Button("Cancel"))
        {
            m_newConfigKey[0] = '\0';
            m_newConfigValue[0] = '\0';
            m_showAddForm = false;
        }
    }
    ImGui::EndChild();

    ImGui::Separator();

    // Config table
    ImGui::BeginChild("##config_table");
    if (ImGui::BeginTable("##global_config", 3,
        ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders |
        ImGuiTableFlags_Resizable | ImGuiTableFlags_ScrollY,
        ImVec2(0, 0)))
    {
        ImGui::TableSetupColumn("Key", ImGuiTableColumnFlags_WidthStretch);
        ImGui::TableSetupColumn("Value", ImGuiTableColumnFlags_WidthStretch);
        ImGui::TableSetupColumn("", ImGuiTableColumnFlags_WidthFixed, 50);
        ImGui::TableHeadersRow();

        for (int i = 0; i < (int)m_globalConfig.size(); i++)
        {
            auto& entry = m_globalConfig[i];
            ImGui::TableNextRow();
            ImGui::PushID(i);

            ImGui::TableNextColumn();
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.6f, 0.2f, 1.0f));
            ImGui::TextUnformatted(entry.key.c_str());
            ImGui::PopStyleColor();

            ImGui::TableNextColumn();
            if (entry.editing)
            {
                ImGui::PushItemWidth(-50);
                bool committed = ImGui::InputText("##edit", entry.editBuf, sizeof(entry.editBuf),
                    ImGuiInputTextFlags_EnterReturnsTrue);
                ImGui::PopItemWidth();
                ImGui::SameLine();
                if (committed || ImGui::SmallButton("OK"))
                {
                    std::string newVal(entry.editBuf);
                    std::string key = entry.key;
                    std::thread([key, newVal]() {
                        GitProcess::Execute("", {"config", "--global", key, newVal});
                    }).detach();
                    entry.value = newVal;
                    entry.editing = false;
                    m_statusMessage = "Updated: " + key;
                }
                ImGui::SameLine();
                if (ImGui::SmallButton("X"))
                {
                    entry.editing = false;
                }
            }
            else
            {
                ImGui::TextUnformatted(entry.value.c_str());
                if (ImGui::IsItemHovered() && ImGui::IsMouseDoubleClicked(0))
                {
                    entry.editing = true;
                    strncpy_s(entry.editBuf, sizeof(entry.editBuf), entry.value.c_str(), _TRUNCATE);
                }
            }

            ImGui::TableNextColumn();
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(1.0f, 0.3f, 0.3f, 1.0f));
            if (ImGui::SmallButton("-"))
            {
                std::string key = entry.key;
                std::thread([key]() {
                    GitProcess::Execute("", {"config", "--global", "--unset", key});
                }).detach();
                m_globalConfig.erase(m_globalConfig.begin() + i);
                m_statusMessage = "Removed: " + key;
                ImGui::PopStyleColor();
                ImGui::PopID();
                break;
            }
            ImGui::PopStyleColor();

            ImGui::PopID();
        }

        ImGui::EndTable();
    }
    ImGui::EndChild();
}

void GitBeeApp::OnRender()
{
    if (m_showDemoWindow)
        ImGui::ShowDemoWindow(&m_showDemoWindow);

    ProcessScanResults();
    ProcessPendingRepos();
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

    ImGui::Begin("##MainContent", nullptr,
        ImGuiWindowFlags_NoTitleBar | ImGuiWindowFlags_NoResize |
        ImGuiWindowFlags_NoMove | ImGuiWindowFlags_NoScrollbar |
        ImGuiWindowFlags_NoSavedSettings | ImGuiWindowFlags_NoBringToFrontOnFocus);

    ImGuiTabBarFlags tabFlags = ImGuiTabBarFlags_FittingPolicyScroll |
        ImGuiTabBarFlags_AutoSelectNewTabs | ImGuiTabBarFlags_Reorderable;

    if (ImGui::BeginTabBar("##MainTabs", tabFlags))
    {
        int realActive = 0;

        // Home tab (closable)
        if (m_homeTabOpen)
        {
            bool homeOpen = true;
            if (ImGui::BeginTabItem("Home", &homeOpen))
            {
                realActive = 0;
                m_homeView->Render();
                ImGui::EndTabItem();
            }
            if (!homeOpen) m_homeTabOpen = false;
        }

        // Pending (loading) repo tabs
        for (int pi = 0; pi < (int)m_pendingRepos.size(); pi++)
        {
            auto& pending = m_pendingRepos[pi];
            if (ImGui::BeginTabItem(pending->displayName.c_str(), (bool*)0))
            {
                ImGui::BeginChild("##pending_content", ImVec2(0, 0), true);
                ImGui::SetCursorPosX((ImGui::GetWindowWidth() - 100) * 0.5f);
                ImGui::SetCursorPosY(ImGui::GetWindowHeight() * 0.4f);
                LoadingSpinnerWithText("Opening repository...", 12.0f);
                ImGui::EndChild();
                ImGui::EndTabItem();
            }
        }

        // Global Config tab
        if (m_globalConfigTabOpen)
        {
            bool configOpen = true;
            if (ImGui::BeginTabItem("Global Config", &configOpen))
            {
                RenderGlobalConfigTab();
                ImGui::EndTabItem();
            }
            if (!configOpen) m_globalConfigTabOpen = false;
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

        if (closeIdx >= 0)
            m_repoTabs.erase(m_repoTabs.begin() + closeIdx);

        m_activeTabIndex = realActive;

        ImGui::EndTabBar();
    }

    ImGui::End();
    ImGui::PopStyleVar(2);

    RenderStatusBar();

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
                    StartOpenRepository(result);
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

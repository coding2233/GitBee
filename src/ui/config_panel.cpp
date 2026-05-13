#include "config_panel.h"
#include "LoadingSpinner.h"
#include "../gitcore/git_repository.h"
#include "../gitcore/git_process.h"
#include <imgui.h>
#include <sstream>

ConfigPanel::ConfigPanel() {}

ConfigPanel::~ConfigPanel()
{
    if (m_configThread.joinable())
        m_configThread.join();
}

void ConfigPanel::SetRepository(std::shared_ptr<GitRepository> repo)
{
    m_repository = std::move(repo);
    m_loaded = false;
}

void ConfigPanel::Refresh()
{
    m_loaded = false;
}

void ConfigPanel::StartAsyncLoad()
{
    if (m_configLoading || !m_repository) return;
    m_configLoading = true;

    std::string repoPath = m_repository->GetPath();

    m_configThread = std::thread([this, repoPath]() {
        std::vector<GitConfigEntry> localCfg, globalCfg;

        auto r = GitProcess::Execute(repoPath, {"config", "--local", "--list"});
        if (r.ok && !r.out.empty())
        {
            std::istringstream stream(r.out);
            std::string line;
            while (std::getline(stream, line))
            {
                auto eq = line.find('=');
                if (eq != std::string::npos)
                    localCfg.push_back({line.substr(0, eq), line.substr(eq + 1)});
            }
        }

        auto rg = GitProcess::Execute(repoPath, {"config", "--global", "--list"});
        if (rg.ok && !rg.out.empty())
        {
            std::istringstream stream(rg.out);
            std::string line;
            while (std::getline(stream, line))
            {
                auto eq = line.find('=');
                if (eq != std::string::npos)
                    globalCfg.push_back({line.substr(0, eq), line.substr(eq + 1)});
            }
        }

        {
            std::lock_guard<std::mutex> lock(m_configMutex);
            m_pendingLocal = std::move(localCfg);
            m_pendingGlobal = std::move(globalCfg);
        }
        m_configLoading = false;
    });
    m_configThread.detach();
}

void ConfigPanel::ProcessAsyncResult()
{
    if (m_configLoading || m_loaded) return;
    if (m_configThread.joinable())
        m_configThread.join();

    std::lock_guard<std::mutex> lock(m_configMutex);
    if (!m_pendingLocal.empty() || !m_pendingGlobal.empty())
    {
        m_localConfig = std::move(m_pendingLocal);
        m_globalConfig = std::move(m_pendingGlobal);
    }
    m_loaded = true;
}

void ConfigPanel::Render()
{
    if (!m_repository)
    {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "No repository opened");
        return;
    }

    if (!m_loaded && !m_configLoading)
    {
        StartAsyncLoad();
    }

    if (m_configLoading)
    {
        LoadingSpinnerWithText("Loading config...");
        return;
    }

    ProcessAsyncResult();

    ImGui::BeginChild("##config_content", ImVec2(0, 0), true);

    ImGui::TextUnformatted("Local Config (project)");
    ImGui::Separator();
    if (m_localConfig.empty())
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "No local config");
    else
        RenderTable("##local", m_localConfig);

    ImGui::Spacing();
    ImGui::TextUnformatted("Global Config (user)");
    ImGui::Separator();
    if (m_globalConfig.empty())
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "No global config");
    else
        RenderTable("##global", m_globalConfig);

    ImGui::EndChild();
}

void ConfigPanel::RenderTable(const char* title, const std::vector<GitConfigEntry>& entries)
{
    if (ImGui::BeginTable(title, 2,
        ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders,
        ImVec2(0, 0)))
    {
        ImGui::TableSetupColumn("Key", ImGuiTableColumnFlags_WidthStretch);
        ImGui::TableSetupColumn("Value", ImGuiTableColumnFlags_WidthStretch);
        ImGui::TableHeadersRow();

        for (auto& entry : entries)
        {
            ImGui::TableNextRow();
            ImGui::TableNextColumn();
            ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.6f, 0.2f, 1.0f));
            ImGui::TextUnformatted(entry.key.c_str());
            ImGui::PopStyleColor();
            ImGui::TableNextColumn();
            ImGui::TextUnformatted(entry.value.c_str());
        }

        ImGui::EndTable();
    }
}

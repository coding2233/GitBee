#include "config_panel.h"
#include "LoadingSpinner.h"
#include "../gitcore/git_repository.h"
#include "../gitcore/git_process.h"
#include <imgui.h>
#include <sstream>
#include <algorithm>
#include <utility>

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
    m_loadStarted = false;
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
    if (m_configLoading) return;
    if (m_loaded) return;
    if (!m_loadStarted) return;
    if (m_configThread.joinable())
        m_configThread.join();

    std::lock_guard<std::mutex> lock(m_configMutex);
    if (!m_pendingLocal.empty() || !m_pendingGlobal.empty())
    {
        m_localConfig.clear();
        for (auto& e : m_pendingLocal)
            m_localConfig.push_back({e.key, e.value, false, {}});
        m_globalConfig = std::move(m_pendingGlobal);
    }
    m_loaded = true;
}

std::string ConfigPanel::GetSectionName(const std::string& key)
{
    auto dot = key.find('.');
    if (dot == std::string::npos) return "core";
    return key.substr(0, dot);
}

std::vector<ConfigPanel::SectionData> ConfigPanel::BuildSections()
{
    std::vector<std::string> order = {"core", "user", "remote", "branch", "credential", "http", "diff", "merge", "alias", "filter", "other"};
    std::map<std::string, std::vector<int>> sectionMap;

    for (int i = 0; i < (int)m_localConfig.size(); i++)
    {
        auto& e = m_localConfig[i];
        if (e.key.find("remote.") == 0 && e.key.rfind('.') != 2)
        {
            int nameEnd = (int)e.key.find('.', 7);
            std::string remoteName = (nameEnd != (int)std::string::npos) ? e.key.substr(7, nameEnd - 7) : e.key.substr(7);
            (void)remoteName;
        }
        std::string section = GetSectionName(e.key);
        sectionMap[section].push_back(i);
    }

    bool hasOther = false;
    std::vector<SectionData> result;
    for (auto& sec : order)
    {
        if (sec == "other") { hasOther = true; continue; }
        if (sectionMap.find(sec) != sectionMap.end())
        {
            result.push_back({sec, sectionMap[sec]});
            sectionMap.erase(sec);
        }
    }

    if (hasOther)
    {
        for (auto& kv : sectionMap)
            result.push_back({kv.first, kv.second});
    }

    return result;
}

std::string ConfigPanel::FindGlobalValue(const std::string& key) const
{
    for (auto& e : m_globalConfig)
    {
        if (e.key == key) return e.value;
    }
    return {};
}

void ConfigPanel::CommitEdit(int index, const std::string& newValue)
{
    if (!m_repository || index < 0 || index >= (int)m_localConfig.size()) return;
    auto& entry = m_localConfig[index];
    std::string repoPath = m_repository->GetPath();
    std::string key = entry.key;
    std::string val = newValue;

    std::thread([repoPath, key, val]() {
        GitProcess::Execute(repoPath, {"config", "--local", key, val});
    }).detach();

    entry.value = newValue;
    entry.editing = false;
}

void ConfigPanel::DeleteEntry(int index)
{
    if (!m_repository || index < 0 || index >= (int)m_localConfig.size()) return;
    auto& entry = m_localConfig[index];
    std::string repoPath = m_repository->GetPath();
    std::string key = entry.key;

    std::thread([repoPath, key]() {
        GitProcess::Execute(repoPath, {"config", "--local", "--unset", key});
    }).detach();

    m_localConfig.erase(m_localConfig.begin() + index);
}

void ConfigPanel::AddNewEntry(const std::string& key, const std::string& value)
{
    if (!m_repository || key.empty()) return;
    std::string repoPath = m_repository->GetPath();

    std::thread([repoPath, key, value]() {
        GitProcess::Execute(repoPath, {"config", "--local", key, value});
    }).detach();

    m_localConfig.push_back({key, value, false, {}});
}

void ConfigPanel::Render()
{
    if (!m_repository)
    {
        ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "No repository opened");
        return;
    }

    ProcessAsyncResult();

    if (!m_loaded && !m_loadStarted)
    {
        m_loadStarted = true;
        StartAsyncLoad();
    }

    if (m_configLoading)
    {
        LoadingSpinnerWithText("Loading config...");
        return;
    }

    ImGui::BeginChild("##config_content", ImVec2(0, 0), true);

    RenderSummaryBar();

    ImGui::Separator();
    ImGui::Spacing();

    RenderLocalSection();

    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();

    RenderRemotesSection();

    ImGui::Spacing();
    ImGui::Separator();
    ImGui::Spacing();

    RenderGlobalSection();

    ImGui::EndChild();
}

void ConfigPanel::RenderSummaryBar()
{
    ImGui::BeginChild("##config_summary", ImVec2(0, ImGui::GetFrameHeight() * 3 + 16), true);

    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.8f, 0.8f, 1.0f));
    ImGui::TextUnformatted("Project Configuration");
    ImGui::PopStyleColor();

    // Find key project settings
    auto findVal = [&](const std::string& key, const std::string& fallback = {}) -> std::string {
        for (auto& e : m_localConfig)
        {
            if (e.key == key) { auto v = e.value; return v.empty() ? fallback : v; }
        }
        std::string gv = FindGlobalValue(key);
        return gv.empty() ? fallback : gv;
    };

    std::string userName = findVal("user.name");
    std::string userEmail = findVal("user.email");
    std::string coreEditor = findVal("core.editor", "default");
    std::string defaultBranch = findVal("init.defaultbranch", "master");

    ImGui::Columns(4, "##summary_cols", false);
    ImGui::SetColumnWidth(0, 140);
    ImGui::SetColumnWidth(1, 200);
    ImGui::SetColumnWidth(2, 140);
    ImGui::SetColumnWidth(3, 200);

    ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "User:");
    ImGui::NextColumn();
    ImGui::TextUnformatted(userName.c_str());
    if (!userEmail.empty()) { ImGui::SameLine(); ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "<%s>", userEmail.c_str()); }
    ImGui::NextColumn();
    ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "Editor:");
    ImGui::NextColumn();
    ImGui::TextUnformatted(coreEditor.c_str());

    ImGui::NextColumn();
    ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "Default Branch:");
    ImGui::NextColumn();
    ImGui::TextUnformatted(defaultBranch.c_str());
    ImGui::NextColumn();
    ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "Config Entries:");
    ImGui::NextColumn();
    ImGui::Text("%d local / %d global", (int)m_localConfig.size(), (int)m_globalConfig.size());

    ImGui::Columns(1);
    ImGui::EndChild();
}

void ConfigPanel::RenderLocalSection()
{
    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.3f, 0.8f, 0.3f, 1.0f));
    ImGui::TextUnformatted("Local Config");
    ImGui::PopStyleColor();

    // Toolbar
    ImGui::BeginChild("##local_toolbar", ImVec2(0, ImGui::GetFrameHeight() + 4), false);
    if (ImGui::SmallButton("+ Add"))
        m_showAddForm = !m_showAddForm;
    ImGui::SameLine();
    if (ImGui::SmallButton("Refresh"))
    {
        m_loaded = false;
        m_localConfig.clear();
    }

    if (m_showAddForm)
    {
        ImGui::Separator();
        ImGui::TextUnformatted("New:");
        ImGui::SameLine();
        ImGui::PushItemWidth(180);
        ImGui::InputText("##newkey", m_newKey, sizeof(m_newKey));
        ImGui::PopItemWidth();
        ImGui::SameLine();
        ImGui::TextUnformatted("=");
        ImGui::SameLine();
        ImGui::PushItemWidth(280);
        ImGui::InputText("##newval", m_newValue, sizeof(m_newValue));
        ImGui::PopItemWidth();
        ImGui::SameLine();
        if (ImGui::SmallButton("Save") && m_newKey[0] != '\0')
        {
            AddNewEntry(m_newKey, m_newValue);
            m_newKey[0] = '\0';
            m_newValue[0] = '\0';
            m_showAddForm = false;
        }
        ImGui::SameLine();
        if (ImGui::SmallButton("Cancel"))
        {
            m_newKey[0] = '\0';
            m_newValue[0] = '\0';
            m_showAddForm = false;
        }
    }
    ImGui::EndChild();

    if (m_localConfig.empty() && !m_showAddForm)
    {
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "No local config. Add one with + Add.");
        return;
    }

    // Sections
    auto sections = BuildSections();
    for (auto& sec : sections)
    {
        auto it = m_sectionOpen.find(sec.name);
        if (it == m_sectionOpen.end())
            m_sectionOpen[sec.name] = true;

        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags_DefaultOpen;
        if (m_sectionOpen[sec.name])
            flags |= ImGuiTreeNodeFlags_DefaultOpen;
        else
            flags &= ~ImGuiTreeNodeFlags_DefaultOpen;

        bool open = ImGui::TreeNodeEx(sec.name.c_str(), flags, "%s  (%d)",
            sec.name.c_str(), (int)sec.entryIndices.size());
        m_sectionOpen[sec.name] = open;

        if (open)
        {
            RenderSectionTable(sec.name, sec.entryIndices);
            ImGui::TreePop();
        }
    }
}

void ConfigPanel::RenderSectionTable(const std::string& sectionName, const std::vector<int>& indices)
{
    ImGui::Unindent(8);
    if (ImGui::BeginTable(("##tbl_" + sectionName).c_str(), 3,
        ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders,
        ImVec2(-1, 0)))
    {
        ImGui::TableSetupColumn("Key", ImGuiTableColumnFlags_WidthFixed, 250);
        ImGui::TableSetupColumn("Value", ImGuiTableColumnFlags_WidthStretch);
        ImGui::TableSetupColumn("", ImGuiTableColumnFlags_WidthFixed, 44);
        ImGui::TableHeadersRow();

        for (int idx : indices)
        {
            auto& entry = m_localConfig[idx];
            ImGui::TableNextRow();
            ImGui::PushID(idx);

            ImGui::TableNextColumn();
            std::string shortKey = entry.key.substr(sectionName.size() + 1);
            std::string fullKey = entry.key;

            // Show global override indicator
            std::string globalVal = FindGlobalValue(fullKey);
            if (!globalVal.empty() && globalVal != entry.value)
            {
                ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.6f, 0.8f, 0.6f, 1.0f));
                ImGui::TextUnformatted(shortKey.c_str());
                ImGui::PopStyleColor();
                if (ImGui::IsItemHovered())
                    ImGui::SetTooltip("Overrides global: %s", globalVal.c_str());
            }
            else
            {
                ImGui::TextUnformatted(shortKey.c_str());
            }

            ImGui::TableNextColumn();
            if (entry.editing)
            {
                ImGui::PushItemWidth(-60);
                bool committed = ImGui::InputText("##edit", entry.editBuf, sizeof(entry.editBuf),
                    ImGuiInputTextFlags_EnterReturnsTrue);
                ImGui::PopItemWidth();
                ImGui::SameLine();
                if (committed || ImGui::SmallButton("OK"))
                    CommitEdit(idx, entry.editBuf);
                ImGui::SameLine();
                if (ImGui::SmallButton("X"))
                    entry.editing = false;
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
                DeleteEntry(idx);
                ImGui::PopStyleColor();
                ImGui::PopID();
                break;
            }
            ImGui::PopStyleColor();

            ImGui::PopID();
        }

        ImGui::EndTable();
    }
    ImGui::Indent(8);
}

void ConfigPanel::RenderRemotesSection()
{
    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.3f, 0.6f, 1.0f, 1.0f));
    ImGui::TextUnformatted("Remotes");
    ImGui::PopStyleColor();

    // Collect remotes organized by name
    std::map<std::string, std::map<std::string, std::string>> remotes;
    for (auto& e : m_localConfig)
    {
        if (e.key.find("remote.") == 0)
        {
            auto dot2 = e.key.find('.', 7);
            if (dot2 != std::string::npos)
            {
                std::string name = e.key.substr(7, dot2 - 7);
                std::string prop = e.key.substr(dot2 + 1);
                remotes[name][prop] = e.value;
            }
        }
    }

    if (remotes.empty())
    {
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "No remotes configured");
        return;
    }

    for (auto& [name, props] : remotes)
    {
        std::string label = name + "##remote_" + name;
        bool open = ImGui::TreeNodeEx(label.c_str(), ImGuiTreeNodeFlags_DefaultOpen,
            "%s", name.c_str());

        if (open)
        {
            ImGui::Unindent(8);
            if (ImGui::BeginTable(("##r_" + name).c_str(), 2,
                ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders,
                ImVec2(-1, 0)))
            {
                ImGui::TableSetupColumn("Property", ImGuiTableColumnFlags_WidthFixed, 80);
                ImGui::TableSetupColumn("Value", ImGuiTableColumnFlags_WidthStretch);
                ImGui::TableHeadersRow();

                for (auto& [prop, val] : props)
                {
                    ImGui::TableNextRow();
                    ImGui::TableNextColumn();
                    ImGui::TextUnformatted(prop.c_str());
                    ImGui::TableNextColumn();
                    ImGui::TextUnformatted(val.c_str());
                }

                ImGui::EndTable();
            }
            ImGui::Indent(8);
            ImGui::TreePop();
        }
    }
}

void ConfigPanel::RenderGlobalSection()
{
    ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.8f, 0.8f, 0.3f, 1.0f));
    ImGui::TextUnformatted("Global Config (read-only reference)");
    ImGui::PopStyleColor();

    ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f),
        "Edit via View > Global Config");

    if (m_globalConfig.empty())
    {
        ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f), "No global config");
        return;
    }

    // Group global by section
    std::map<std::string, std::vector<std::pair<std::string, std::string>>> groups;
    for (auto& e : m_globalConfig)
    {
        std::string sec = GetSectionName(e.key);
        groups[sec].push_back({e.key, e.value});
    }

    for (auto& [sec, entries] : groups)
    {
        bool open = ImGui::TreeNodeEx(sec.c_str(), ImGuiTreeNodeFlags_DefaultOpen,
            "%s  (%d)", sec.c_str(), (int)entries.size());

        if (open)
        {
            ImGui::Unindent(8);
            if (ImGui::BeginTable(("##g_" + sec).c_str(), 2,
                ImGuiTableFlags_RowBg | ImGuiTableFlags_Borders,
                ImVec2(-1, 0)))
            {
                ImGui::TableSetupColumn("Key", ImGuiTableColumnFlags_WidthFixed, 250);
                ImGui::TableSetupColumn("Value", ImGuiTableColumnFlags_WidthStretch);
                ImGui::TableHeadersRow();

                for (auto& [key, val] : entries)
                {
                    // Check if local overrides this
                    bool isOverridden = false;
                    for (auto& le : m_localConfig)
                    {
                        if (le.key == key) { isOverridden = true; break; }
                    }

                    ImGui::TableNextRow();
                    ImGui::TableNextColumn();
                    std::string shortKey = key.substr(sec.size() + 1);

                    if (isOverridden)
                    {
                        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.4f, 0.4f, 0.4f, 1.0f));
                        ImGui::TextUnformatted(shortKey.c_str());
                        ImGui::PopStyleColor();
                        if (ImGui::IsItemHovered())
                            ImGui::SetTooltip("Overridden by local config");
                    }
                    else
                    {
                        ImGui::TextUnformatted(shortKey.c_str());
                    }

                    ImGui::TableNextColumn();
                    if (isOverridden)
                    {
                        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.4f, 0.4f, 0.4f, 1.0f));
                        ImGui::TextUnformatted(val.c_str());
                        ImGui::PopStyleColor();
                    }
                    else
                    {
                        ImGui::TextUnformatted(val.c_str());
                    }
                }

                ImGui::EndTable();
            }
            ImGui::Indent(8);
            ImGui::TreePop();
        }
    }
}

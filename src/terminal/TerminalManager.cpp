#include "TerminalManager.h"
#include "TerminalTab.h"
#include "PtyAdapter.h"
#include "ConnectionStore.h"
#include "KeyMapping.h"
#include "../dbg_log.h"
#include "imgui.h"
#include <algorithm>

// Buffer sizes for the connection dialog form fields
static constexpr int FORM_BUF_SIZE = 256;

TerminalManager::TerminalManager() {
    m_store = std::make_unique<ConnectionStore>();
    m_store->Load();
    // Start with one local terminal
    OpenLocalTerminal();
}

TerminalManager::~TerminalManager() {
    m_tabs.clear();
}

// ---------------------------------------------------------------------------
// Open / Close
// ---------------------------------------------------------------------------

TerminalTab* TerminalManager::OpenLocalTerminal(const std::string& workingDir) {
    auto tab = std::make_unique<TerminalTab>(TerminalTab::Type::LocalShell, NextTabTitle());
    if (!workingDir.empty())
        tab->SetWorkingDirectory(workingDir);

    PtyConfig config;
    config.cols = 80;
    config.rows = 24;

    if (!tab->Start(config)) {
        LOG_ERROR("Failed to open local terminal");
        return nullptr;
    }

    m_tabs.push_back(std::move(tab));
    m_activeTabIndex = (int)m_tabs.size() - 1;
    LOG_INFO("Opened local terminal tab %d", m_activeTabIndex);
    return m_tabs.back().get();
}

TerminalTab* TerminalManager::OpenSshTerminal(const SshConnection& conn) {
    // Check if already connected to this host
    int existing = FindTabByConnection(conn.id);
    if (existing >= 0) {
        m_activeTabIndex = existing;
        return m_tabs[existing].get();
    }

    auto tab = std::make_unique<TerminalTab>(TerminalTab::Type::RemoteSsh, conn.name);
    tab->SetTitle(conn.name);

    PtyConfig config;
    config.cols = 80;
    config.rows = 24;
    config.host = conn.host;
    config.port = conn.port;
    config.username = conn.username;
    config.privateKeyPath = conn.privateKeyPath;

    if (!tab->Start(config)) {
        LOG_ERROR("Failed to open SSH terminal to %s@%s", conn.username.c_str(), conn.host.c_str());
        return nullptr;
    }

    m_tabs.push_back(std::move(tab));
    m_activeTabIndex = (int)m_tabs.size() - 1;
    LOG_INFO("Opened SSH terminal tab %d: %s@%s", m_activeTabIndex,
             conn.username.c_str(), conn.host.c_str());
    return m_tabs.back().get();
}

void TerminalManager::CloseTerminal(int index) {
    if (index < 0 || index >= (int)m_tabs.size()) return;
    m_tabs[index]->Close();
    m_tabs.erase(m_tabs.begin() + index);
    if (m_tabs.empty()) {
        m_activeTabIndex = -1;
    } else if (m_activeTabIndex >= (int)m_tabs.size()) {
        m_activeTabIndex = (int)m_tabs.size() - 1;
    }
}

int TerminalManager::FindTabByConnection(const std::string& connId) const {
    // Simple heuristic: match by host+user for SSH tabs
    for (int i = 0; i < (int)m_tabs.size(); i++) {
        if (m_tabs[i]->GetType() == TerminalTab::Type::RemoteSsh) {
            // We don't have direct access to the connection config,
            // so we'll just return the first SSH tab to the same host
            if (m_tabs[i]->GetTitle().find(connId) != std::string::npos)
                return i;
        }
    }
    return -1;
}

// ---------------------------------------------------------------------------
// Rendering
// ---------------------------------------------------------------------------

void TerminalManager::Render() {
    float sidebarWidth = m_showSidebar ? 200.0f : 0.0f;

    if (m_showSidebar) {
        ImGui::BeginChild("##terminal_sidebar", ImVec2(sidebarWidth, 0), true);
        RenderSidebar();
        ImGui::EndChild();
        ImGui::SameLine();
    }

    ImGui::BeginChild("##terminal_content", ImVec2(0, 0), false);

    if (m_tabs.empty()) {
        ImVec2 avail = ImGui::GetContentRegionAvail();
        ImVec2 pos = ImGui::GetCursorScreenPos();
        auto* dl = ImGui::GetWindowDrawList();
        dl->AddRectFilled(pos, ImVec2(pos.x + avail.x, pos.y + avail.y),
                          IM_COL32(24, 24, 30, 255));

        const char* msg = "No terminals open.\n\n"
                          "  [Local Terminal]  Ctrl+Shift+N\n"
                          "  [SSH Connection]  Click a profile in sidebar";
        ImVec2 textSize = ImGui::CalcTextSize(msg, nullptr, false, avail.x * 0.8f);
        ImVec2 textPos(pos.x + (avail.x - textSize.x) * 0.5f,
                       pos.y + (avail.y - textSize.y) * 0.5f);
        dl->AddText(ImGui::GetFont(), ImGui::GetFontSize(), textPos,
                    IM_COL32(100, 100, 120, 255), msg, nullptr, avail.x * 0.8f);
    } else {
        RenderTabBar();
        ImGui::Separator();
        RenderActiveTab();
    }

    ImGui::EndChild();

    // Connection dialogs (rendered as popups)
    if (m_showNewConnectionDialog)
        RenderConnectionDialog(true);
    if (m_showEditConnectionDialog)
        RenderConnectionDialog(false);
}

void TerminalManager::RenderSidebar() {
    ImGui::TextUnformatted("Terminals");
    ImGui::Separator();

    ImGui::PushItemWidth(-1);
    ImGui::InputTextWithHint("##search", "Search...", m_searchFilter, sizeof(m_searchFilter));
    ImGui::PopItemWidth();
    ImGui::Dummy(ImVec2(0, 4));

    RenderLocalSection();
    ImGui::Dummy(ImVec2(0, 4));
    RenderSshSection();
    ImGui::Dummy(ImVec2(0, 4));

    // [+ New Local] button
    if (ImGui::Button("[+] New Terminal", ImVec2(-1, 0))) {
        OpenLocalTerminal();
    }
    if (ImGui::IsItemHovered())
        ImGui::SetTooltip("Open a local shell");

    // [+ New SSH] button
    if (ImGui::Button("[+] New SSH Connection", ImVec2(-1, 0))) {
        m_editingConnId.clear();
        m_showNewConnectionDialog = true;
    }
    if (ImGui::IsItemHovered())
        ImGui::SetTooltip("Add an SSH connection profile");
}

void TerminalManager::RenderLocalSection() {
    if (ImGui::TreeNodeEx("Local", ImGuiTreeNodeFlags_DefaultOpen)) {
        for (int i = 0; i < (int)m_tabs.size(); i++) {
            if (m_tabs[i]->GetType() != TerminalTab::Type::LocalShell)
                continue;

            if (m_searchFilter[0] != '\0') {
                std::string title = m_tabs[i]->GetTitle();
                std::string filter = m_searchFilter;
                std::transform(title.begin(), title.end(), title.begin(), ::tolower);
                std::transform(filter.begin(), filter.end(), filter.begin(), ::tolower);
                if (title.find(filter) == std::string::npos)
                    continue;
            }

            bool selected = (i == m_activeTabIndex);
            bool closed = !m_tabs[i]->IsOpen();

            ImGui::PushID(i);
            if (closed) {
                ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(0.4f, 0.4f, 0.4f, 1.0f));
            }
            if (ImGui::Selectable(m_tabs[i]->GetTitle().c_str(), selected)) {
                m_activeTabIndex = i;
            }
            if (closed) {
                ImGui::PopStyleColor();
            }
            if (ImGui::BeginPopupContextItem()) {
                if (ImGui::MenuItem("Close")) { CloseTerminal(i); ImGui::CloseCurrentPopup(); }
                if (ImGui::MenuItem("Close Others")) {
                    for (int j = (int)m_tabs.size() - 1; j >= 0; j--)
                        if (j != i) CloseTerminal(j);
                    ImGui::CloseCurrentPopup();
                }
                ImGui::EndPopup();
            }
            ImGui::PopID();
        }
        ImGui::TreePop();
    }
}

void TerminalManager::RenderSshSection() {
    if (ImGui::TreeNodeEx("SSH Connections", ImGuiTreeNodeFlags_DefaultOpen)) {
        // Get all connections from store
        auto allConns = m_store->GetAll();

        // Apply search filter
        std::vector<const SshConnection*> filtered;
        for (auto& c : allConns) {
            if (m_searchFilter[0] != '\0') {
                std::string name = c.name;
                std::string host = c.host;
                std::string filter = m_searchFilter;
                std::transform(name.begin(), name.end(), name.begin(), ::tolower);
                std::transform(host.begin(), host.end(), host.begin(), ::tolower);
                std::transform(filter.begin(), filter.end(), filter.begin(), ::tolower);
                if (name.find(filter) == std::string::npos &&
                    host.find(filter) == std::string::npos)
                    continue;
            }
            filtered.push_back(&c);
        }

        if (filtered.empty()) {
            ImGui::TextColored(ImVec4(0.4f, 0.4f, 0.4f, 1.0f),
                              m_store->GetAll().empty()
                              ? "No connections. Click [+] to add."
                              : "No matches.");
        } else {
            // Render by group
            auto groups = m_store->GetGroups();
            bool hasUngrouped = !m_store->GetUngrouped().empty();

            for (const auto& group : groups) {
                // Count visible connections in this group
                int visibleCount = 0;
                for (auto* c : filtered)
                    if (c->group == group) visibleCount++;
                if (visibleCount == 0) continue;

                if (ImGui::TreeNodeEx(group.c_str(), ImGuiTreeNodeFlags_DefaultOpen)) {
                    for (auto* conn : filtered) {
                        if (conn->group != group) continue;
                        RenderSshConnectionItem(*conn);
                    }
                    ImGui::TreePop();
                }
            }

            if (hasUngrouped) {
                // Render ungrouped items at top level
                for (auto* conn : filtered) {
                    if (!conn->group.empty()) continue;
                    RenderSshConnectionItem(*conn);
                }
            }
        }

        ImGui::TreePop();
    }
}

void TerminalManager::RenderSshConnectionItem(const SshConnection& conn) {
    ImGui::PushID(conn.id.c_str());

    std::string label = conn.name.empty() ? conn.host : conn.name;
    if (!conn.username.empty())
        label += "  (" + conn.username + "@" + conn.host + ")";
    else
        label += "  (" + conn.host + ")";

    bool doubleClicked = false;
    if (ImGui::Selectable(label.c_str(), false, ImGuiSelectableFlags_AllowDoubleClick)) {
        if (ImGui::IsMouseDoubleClicked(0)) {
            doubleClicked = true;
        }
    }

    // Tooltip
    if (ImGui::IsItemHovered()) {
        std::string tip = conn.host;
        if (conn.port != 22) tip += ":" + std::to_string(conn.port);
        if (!conn.username.empty()) tip += "  user: " + conn.username;
        if (!conn.privateKeyPath.empty()) tip += "\nkey: " + conn.privateKeyPath;
        if (!conn.notes.empty()) tip += "\n" + conn.notes;
        ImGui::SetTooltip("%s", tip.c_str());
    }

    // Double-click to connect
    if (doubleClicked) {
        OpenSshTerminal(conn);
    }

    // Context menu
    if (ImGui::BeginPopupContextItem()) {
        if (ImGui::MenuItem("Connect")) { OpenSshTerminal(conn); ImGui::CloseCurrentPopup(); }
        ImGui::Separator();
        if (ImGui::MenuItem("Edit")) {
            m_editingConnId = conn.id;
            m_showEditConnectionDialog = true;
            ImGui::CloseCurrentPopup();
        }
        if (ImGui::MenuItem("Delete")) {
            m_store->RemoveConnection(conn.id);
            ImGui::CloseCurrentPopup();
        }
        ImGui::EndPopup();
    }

    ImGui::PopID();
}

// ---------------------------------------------------------------------------
// Connection Dialog
// ---------------------------------------------------------------------------

void TerminalManager::RenderConnectionDialog(bool isNew) {
    const char* title = isNew ? "New SSH Connection" : "Edit SSH Connection";
    ImGui::SetNextWindowSize(ImVec2(520, 420), ImGuiCond_Appearing);

    bool open = true;
    if (!ImGui::Begin(title, &open, ImGuiWindowFlags_NoDocking | ImGuiWindowFlags_NoCollapse)) {
        if (!open) { m_showNewConnectionDialog = false; m_showEditConnectionDialog = false; }
        ImGui::End();
        return;
    }

    // Form state (track which dialog instance we're in for reset detection)
    static char formName[FORM_BUF_SIZE] = {};
    static char formHost[FORM_BUF_SIZE] = {};
    static int  formPort = 22;
    static char formUser[FORM_BUF_SIZE] = {};
    static int  formAuthMethod = 3;  // Default
    static char formKeyPath[FORM_BUF_SIZE] = {};
    static char formGroup[FORM_BUF_SIZE] = {};
    static char formNotes[4096] = {};
    static bool formEverOpened = false;

    // Reset form fields when dialog first opens or switches mode
    if (!formEverOpened || (isNew && ImGui::IsWindowAppearing())) {
        memset(formName, 0, sizeof(formName));
        memset(formHost, 0, sizeof(formHost));
        formPort = 22;
        memset(formUser, 0, sizeof(formUser));
        formAuthMethod = 3;
        memset(formKeyPath, 0, sizeof(formKeyPath));
        memset(formGroup, 0, sizeof(formGroup));
        memset(formNotes, 0, sizeof(formNotes));
        formEverOpened = true;
    }

    // Load existing connection data when editing
    if (!isNew) {
        static std::string loadedId;
        if (ImGui::IsWindowAppearing() || loadedId != m_editingConnId) {
            loadedId = m_editingConnId;
            auto* conn = m_store->FindById(m_editingConnId);
            if (conn) {
                strncpy(formName, conn->name.c_str(), FORM_BUF_SIZE - 1);
                strncpy(formHost, conn->host.c_str(), FORM_BUF_SIZE - 1);
                formPort = conn->port;
                strncpy(formUser, conn->username.c_str(), FORM_BUF_SIZE - 1);
                formAuthMethod = (int)conn->authMethod;
                strncpy(formKeyPath, conn->privateKeyPath.c_str(), FORM_BUF_SIZE - 1);
                strncpy(formGroup, conn->group.c_str(), FORM_BUF_SIZE - 1);
                strncpy(formNotes, conn->notes.c_str(), sizeof(formNotes) - 1);
            }
        }
    }

    // --- Form ---
    ImGui::PushItemWidth(160);

    // Name
    ImGui::LabelText("##name_label", "Connection Name");
    ImGui::SameLine();
    ImGui::InputText("##name", formName, FORM_BUF_SIZE);
    ImGui::Spacing();

    // Host
    ImGui::LabelText("##host_label", "Host");
    ImGui::SameLine();
    ImGui::InputText("##host", formHost, FORM_BUF_SIZE);
    ImGui::SameLine();
    ImGui::LabelText("##port_label", "Port");
    ImGui::SameLine();
    ImGui::PushItemWidth(60);
    ImGui::InputInt("##port", &formPort, 1, 100);
    if (formPort < 1) formPort = 1;
    if (formPort > 65535) formPort = 65535;
    ImGui::PopItemWidth();
    ImGui::Spacing();

    // Username
    ImGui::LabelText("##user_label", "Username");
    ImGui::SameLine();
    ImGui::InputText("##user", formUser, FORM_BUF_SIZE);
    ImGui::Spacing();

    // Auth method
    ImGui::LabelText("##auth_label", "Auth Method");
    ImGui::SameLine();
    ImGui::PushItemWidth(160);
    const char* authItems[] = { "SSH Agent", "Public Key", "Password", "~/.ssh/config Default" };
    ImGui::Combo("##auth", &formAuthMethod, authItems, IM_ARRAYSIZE(authItems));
    ImGui::PopItemWidth();
    ImGui::Spacing();

    // Key path (only for PublicKey)
    if (formAuthMethod == 1) {
        ImGui::LabelText("##key_label", "Private Key");
        ImGui::SameLine();
        ImGui::PushItemWidth(260);
        ImGui::InputText("##key", formKeyPath, FORM_BUF_SIZE);
        ImGui::PopItemWidth();
        ImGui::SameLine();
        if (ImGui::SmallButton("Browse...")) {
            // TODO: open file dialog for key selection
        }
        ImGui::Spacing();
    }

    // Group
    ImGui::LabelText("##group_label", "Group");
    ImGui::SameLine();
    ImGui::InputText("##group", formGroup, FORM_BUF_SIZE);
    ImGui::Spacing();

    // Notes
    ImGui::LabelText("##notes_label", "Notes");
    ImGui::InputTextMultiline("##notes", formNotes, sizeof(formNotes),
                              ImVec2(-1, 80));

    ImGui::PopItemWidth();

    ImGui::Separator();
    ImGui::Dummy(ImVec2(0, 4));

    // Buttons
    float buttonWidth = 100;
    float windowWidth = ImGui::GetWindowWidth();
    ImGui::SetCursorPosX((windowWidth - buttonWidth * 2 - 8) * 0.5f);

    if (ImGui::Button("Save", ImVec2(buttonWidth, 0))) {
        if (strlen(formHost) > 0) {
            SshConnection conn;
            if (!isNew) conn.id = m_editingConnId;
            conn.name = formName;
            conn.host = formHost;
            conn.port = formPort;
            conn.username = formUser;
            conn.authMethod = (SshConnection::AuthMethod)formAuthMethod;
            conn.privateKeyPath = formKeyPath;
            conn.group = formGroup;
            conn.notes = formNotes;

            m_store->SaveConnection(conn);
            LOG_INFO("Saved SSH connection: %s@%s", conn.username.c_str(), conn.host.c_str());

            m_showNewConnectionDialog = false;
            m_showEditConnectionDialog = false;
        }
    }

    ImGui::SameLine();

    // Allow closing with X button or Cancel
    if (ImGui::Button("Cancel", ImVec2(buttonWidth, 0))) {
        m_showNewConnectionDialog = false;
        m_showEditConnectionDialog = false;
    }

    ImGui::End();

    if (!open) {
        m_showNewConnectionDialog = false;
        m_showEditConnectionDialog = false;
    }
}

// ---------------------------------------------------------------------------
// Tab Bar & Active Tab
// ---------------------------------------------------------------------------

void TerminalManager::RenderTabBar() {
    ImGuiTabBarFlags tabFlags = ImGuiTabBarFlags_FittingPolicyScroll |
                                ImGuiTabBarFlags_AutoSelectNewTabs |
                                ImGuiTabBarFlags_Reorderable;

    if (ImGui::BeginTabBar("##terminal_tabs", tabFlags)) {
        for (int i = 0; i < (int)m_tabs.size(); i++) {
            bool open = true;
            std::string label = m_tabs[i]->GetTitle();

            // Add indicator for closed tabs
            if (!m_tabs[i]->IsOpen())
                label += " (disconnected)";

            label += "##t" + std::to_string(i);

            ImGuiTabItemFlags itemFlags = ImGuiTabItemFlags_None;
            if (i == m_activeTabIndex)
                itemFlags |= ImGuiTabItemFlags_SetSelected;

            if (ImGui::BeginTabItem(label.c_str(), &open, itemFlags)) {
                m_activeTabIndex = i;
                ImGui::EndTabItem();
            }

            if (!open) {
                CloseTerminal(i);
                i--;
            }
        }

        RenderNewTabButton();
        ImGui::EndTabBar();
    }
}

void TerminalManager::RenderNewTabButton() {
    ImGui::SameLine();
    ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(4, 2));
    if (ImGui::SmallButton("+")) {
        OpenLocalTerminal();
    }
    if (ImGui::IsItemHovered())
        ImGui::SetTooltip("New Local Terminal (Ctrl+Shift+N)");
    ImGui::PopStyleVar();
}

void TerminalManager::RenderActiveTab() {
    if (m_activeTabIndex >= 0 && m_activeTabIndex < (int)m_tabs.size()) {
        m_tabs[m_activeTabIndex]->Render();
    }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

std::string TerminalManager::NextTabTitle() {
    return "Terminal " + std::to_string(m_nextTabId++);
}

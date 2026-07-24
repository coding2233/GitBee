#pragma once

#include <memory>
#include <vector>
#include <string>

class TerminalTab;
class ConnectionStore;
struct SshConnection;

/// Manages all terminal tabs and the connection sidebar.
class TerminalManager {
public:
    TerminalManager();
    ~TerminalManager();

    /// Render the entire terminal view (sidebar + active tab).
    void Render();

    /// Open a new local terminal.
    TerminalTab* OpenLocalTerminal(const std::string& workingDir = "");

    /// Open an SSH terminal from a connection profile.
    TerminalTab* OpenSshTerminal(const SshConnection& conn);

    /// Close a specific terminal tab.
    void CloseTerminal(int index);

    /// Total number of terminal tabs.
    int Count() const { return (int)m_tabs.size(); }

    /// Get the active tab index.
    int GetActiveIndex() const { return m_activeTabIndex; }

private:
    std::vector<std::unique_ptr<TerminalTab>> m_tabs;
    int m_activeTabIndex = -1;
    int m_nextTabId = 1;

    // Connection store
    std::unique_ptr<ConnectionStore> m_store;

    // UI state
    bool m_showSidebar = true;
    char m_searchFilter[256] = {};

    // New/edit connection dialog state
    bool m_showNewConnectionDialog = false;
    bool m_showEditConnectionDialog = false;
    std::string m_editingConnId;  // which connection is being edited

    // Rendering methods
    void RenderSidebar();
    void RenderTabBar();
    void RenderActiveTab();
    void RenderNewTabButton();

    // Sidebar sub-sections
    void RenderLocalSection();
    void RenderSshSection();
    void RenderSshConnectionItem(const SshConnection& conn);
    void RenderConnectionDialog(bool isNew);
    void RenderConfirmDelete(const std::string& id, const std::string& name);

    // Get a unique tab title
    std::string NextTabTitle();

    // Find a tab that is already connected to a given SSH connection
    int FindTabByConnection(const std::string& connId) const;
};

#pragma once

#include <memory>
#include <string>
#include <functional>
#include <imgui.h>

class PtyAdapter;
class TerminalEmulator;

struct PtyConfig;

/// Represents a single terminal session tab (local or remote SSH).
class TerminalTab {
public:
    enum class Type { LocalShell, RemoteSsh };

    TerminalTab(Type type, const std::string& title = "Terminal");
    ~TerminalTab();

    /// Start the terminal session (local shell or SSH connection).
    bool Start(const PtyConfig& config);

    /// Close the terminal session.
    void Close();

    /// Attempt to reconnect a disconnected terminal.
    void Reconnect();

    /// Render the content of this tab (fills the available area).
    void Render();

    /// Check if the tab is still usable.
    bool IsOpen() const { return m_open; }

    /// Get the display title.
    const std::string& GetTitle() const { return m_title; }
    void SetTitle(const std::string& t) { m_title = t; }

    /// Get the terminal type.
    Type GetType() const { return m_type; }

    /// Set working directory (for local terminals, effective before Start).
    void SetWorkingDirectory(const std::string& dir) { m_workingDir = dir; }

    /// Copy selected text to clipboard.
    void CopySelection();

    /// Paste text from clipboard to PTY.
    void PasteClipboard();

    /// Callbacks
    std::function<void(const std::string& title)> OnTitleChange;

    /// Check if reconnect is possible.
    bool CanReconnect() const { return m_reconnectPending; }

private:
    Type m_type;
    std::string m_title;
    std::string m_workingDir;
    bool m_open = false;
    bool m_reconnectPending = false;

    std::unique_ptr<PtyAdapter> m_pty;
    std::unique_ptr<TerminalEmulator> m_terminal;

    // Rendering state
    float m_scrollY = 0.0f;
    bool m_focused = false;

    // Selection state (mouse drag)
    bool m_mouseDragging = false;
    bool m_mouseSelecting = false;

    // Search overlay state
    bool m_searchActive = false;
    char m_searchText[256] = {};
    int m_searchCurrentMatch = 0;
    int m_searchTotalMatches = 0;
    bool m_searchCaseSensitive = false;

    void RenderTerminal();
    void ProcessPtyOutput();
    void ProcessKeyboardInput();
    void ProcessMouseInput();
    void RenderStatusBar();
    void RenderSearchOverlay();

    /// Map screen pixel position to terminal grid cell (row, col).
    /// Returns false if position is outside the terminal area.
    bool ScreenPosToGrid(ImVec2 mousePos, int& row, int& col) const;

    /// Layout info for coordinate mapping.
    struct TermLayout {
        ImVec2 pos;
        ImVec2 size;
        float cellWidth;
        float cellHeight;
        int visibleCols;
        int visibleRows;
        int scrollRow;  // first visible row in scrollback-adjusted coords
    };
    TermLayout ComputeLayout(ImVec2 availSize) const;
};

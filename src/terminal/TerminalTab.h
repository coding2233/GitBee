#pragma once

#include <memory>
#include <string>
#include <functional>

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

    /// Callbacks
    std::function<void(const std::string& title)> OnTitleChange;

private:
    Type m_type;
    std::string m_title;
    std::string m_workingDir;
    bool m_open = false;

    std::unique_ptr<PtyAdapter> m_pty;
    std::unique_ptr<TerminalEmulator> m_terminal;

    // Rendering state
    float m_scrollY = 0.0f;
    bool m_focused = false;

    void RenderTerminal();
    void ProcessPtyOutput();
    void ProcessKeyboardInput();
    void RenderStatusBar();
};

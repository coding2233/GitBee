#include "TerminalTab.h"
#include "PtyAdapter.h"
#include "LocalPty.h"
#include "SshPty.h"
#include "TerminalEmulator.h"
#include "KeyMapping.h"
#include "../dbg_log.h"
#include <imgui.h>
#include <cstring>
#include <algorithm>
#include <SDL3/SDL.h>

TerminalTab::TerminalTab(Type type, const std::string& title)
    : m_type(type), m_title(title) {
}

TerminalTab::~TerminalTab() {
    Close();
}

bool TerminalTab::Start(const PtyConfig& config) {
    // Create the PTY
    switch (m_type) {
        case Type::LocalShell:
            m_pty = std::make_unique<LocalPty>();
            break;
        case Type::RemoteSsh:
            m_pty = std::make_unique<SshPty>();
            break;
    }

    // Apply working directory for local PTY
    PtyConfig cfg = config;
    if (!m_workingDir.empty() && cfg.workingDir.empty())
        cfg.workingDir = m_workingDir;

    // Start the PTY
    if (!m_pty->Start(cfg)) {
        LOG_ERROR("Failed to start PTY");
        m_pty.reset();
        return false;
    }

    // Create terminal emulator
    m_terminal = std::make_unique<TerminalEmulator>(cfg.cols, cfg.rows);

    // Wire up title change
    m_terminal->OnTitleChange = [this](const std::string& title) {
        if (!title.empty()) {
            m_title = title;
            if (OnTitleChange) OnTitleChange(title);
        }
    };

    // PTY closed notification
    m_pty->OnClosed = [this]() {
        m_open = false;
        LOG_INFO("Terminal PTY closed");
    };

    m_open = true;
    LOG_INFO("TerminalTab started: %s", m_title.c_str());
    return true;
}

void TerminalTab::Close() {
    if (m_pty) {
        m_pty->Close();
        m_pty.reset();
    }
    m_terminal.reset();
    m_open = false;
}

void TerminalTab::Render() {
    if (!m_open || !m_terminal) {
        // Display a "disconnected" placeholder
        ImVec2 avail = ImGui::GetContentRegionAvail();
        auto* dl = ImGui::GetWindowDrawList();
        ImVec2 pos = ImGui::GetCursorScreenPos();
        dl->AddRectFilled(pos, ImVec2(pos.x + avail.x, pos.y + avail.y),
                          IM_COL32(32, 32, 40, 255));

        const char* msg = m_type == Type::LocalShell
            ? "Terminal not running. Click [+] to open a new terminal."
            : "SSH connection closed. Click to reconnect.";

        ImVec2 textSize = ImGui::CalcTextSize(msg);
        ImVec2 textPos(pos.x + (avail.x - textSize.x) * 0.5f,
                       pos.y + (avail.y - textSize.y) * 0.5f);
        dl->AddText(textPos, IM_COL32(128, 128, 128, 255), msg);
        return;
    }

    // Process PTY output (non-blocking)
    ProcessPtyOutput();

    // Render terminal content
    RenderTerminal();

    // Process keyboard events
    if (m_focused)
        ProcessKeyboardInput();

    // Process mouse events for selection
    ProcessMouseInput();

    // Status bar at bottom
    RenderStatusBar();
}

void TerminalTab::ProcessPtyOutput() {
    if (!m_pty || !m_terminal) return;

    char buf[4096];
    int n;
    while ((n = m_pty->Read(buf, sizeof(buf))) > 0) {
        m_terminal->ProcessInput(buf, (size_t)n);
    }

    if (n < 0) {
        // PTY closed or error
        LOG_DEBUG("Terminal PTY read returned %d, closing", n);
        m_open = false;
    }
}

// ---------------------------------------------------------------------------
// Layout computation
// ---------------------------------------------------------------------------

TerminalTab::TermLayout TerminalTab::ComputeLayout(ImVec2 availSize) const {
    TermLayout layout = {};
    layout.pos = ImGui::GetCursorScreenPos();

    // Reserve space for status bar
    float statusH = ImGui::GetFrameHeight() + 4;
    layout.size = ImVec2(availSize.x, availSize.y - statusH);

    if (m_terminal) {
        auto* font = ImGui::GetFont();
        layout.cellWidth = font->CalcTextSizeA(ImGui::GetFontSize(), FLT_MAX, -1.0f, "M", nullptr, nullptr).x;
        layout.cellHeight = ImGui::GetTextLineHeight();
        layout.visibleCols = std::max(16, (int)(layout.size.x / layout.cellWidth));
        layout.visibleRows = std::max(4, (int)(layout.size.y / layout.cellHeight));

        // Compute scroll position
        int sbSize = m_terminal->GetScrollbackRows();
        int totalRows = sbSize + m_terminal->GetRows();
        int maxScroll = std::max(0, totalRows - layout.visibleRows);
        layout.scrollRow = (int)(m_scrollY * maxScroll + 0.5f);
        layout.scrollRow = std::max(0, std::min(layout.scrollRow, maxScroll));
    } else {
        layout.cellWidth = 8.0f;
        layout.cellHeight = 16.0f;
        layout.visibleCols = 80;
        layout.visibleRows = 24;
        layout.scrollRow = 0;
    }

    return layout;
}

bool TerminalTab::ScreenPosToGrid(ImVec2 mousePos, int& row, int& col) const {
    auto& io = ImGui::GetIO();

    // Get the terminal child window bounds
    ImVec2 winPos = ImGui::GetWindowPos();
    ImVec2 winSize = ImGui::GetWindowSize();

    // We need layout info. Recompute quickly.
    TermLayout layout = ComputeLayout(ImGui::GetContentRegionAvail());
    if (layout.cellWidth <= 0 || layout.cellHeight <= 0) return false;

    float dx = mousePos.x - layout.pos.x;
    float dy = mousePos.y - layout.pos.y;

    if (dx < 0 || dy < 0 || dx >= layout.size.x || dy >= layout.size.y)
        return false;

    col = (int)(dx / layout.cellWidth);
    row = (int)(dy / layout.cellHeight);

    // Clamp
    col = std::max(0, std::min(col, layout.visibleCols - 1));
    row = std::max(0, std::min(row, layout.visibleRows - 1));

    // Convert to buffer coordinates
    row += layout.scrollRow;

    return true;
}

// ---------------------------------------------------------------------------
// Mouse Input
// ---------------------------------------------------------------------------

void TerminalTab::ProcessMouseInput() {
    if (!m_terminal || !m_open) return;

    auto& io = ImGui::GetIO();

    // Only process mouse events when the terminal view is hovered
    if (!ImGui::IsWindowHovered()) return;

    // Left mouse button: start/drag selection
    if (ImGui::IsMouseClicked(ImGuiMouseButton_Left)) {
        int row, col;
        if (ScreenPosToGrid(ImGui::GetMousePos(), row, col)) {
            m_mouseSelecting = true;
            m_mouseDragging = false;
            m_terminal->BeginSelection(row, col);
        }
    }

    if (ImGui::IsMouseDown(ImGuiMouseButton_Left) && m_mouseSelecting) {
        if (!m_mouseDragging) {
            // Check for drag threshold
            if (ImGui::IsMouseDragging(ImGuiMouseButton_Left, 4.0f)) {
                m_mouseDragging = true;
            }
        }
        if (m_mouseDragging) {
            int row, col;
            if (ScreenPosToGrid(ImGui::GetMousePos(), row, col)) {
                m_terminal->UpdateSelection(row, col);
            }
        }
    }

    if (ImGui::IsMouseReleased(ImGuiMouseButton_Left)) {
        if (m_mouseSelecting) {
            m_terminal->EndSelection();
            m_mouseSelecting = false;
            m_mouseDragging = false;
        }
    }

    // Right click: cancel selection
    if (ImGui::IsMouseClicked(ImGuiMouseButton_Right)) {
        if (m_terminal->HasSelection()) {
            m_terminal->CancelSelection();
            m_mouseSelecting = false;
            m_mouseDragging = false;
        }
    }
}

// ---------------------------------------------------------------------------
// Copy / Paste
// ---------------------------------------------------------------------------

void TerminalTab::CopySelection() {
    if (!m_terminal || !m_terminal->HasSelection()) return;

    std::string text = m_terminal->GetSelectedText();
    if (text.empty()) return;

    // Set clipboard via SDL
    if (SDL_SetClipboardText(text.c_str()) == 0) {
        LOG_DEBUG("Copied %zu bytes from terminal selection", text.size());
    } else {
        LOG_ERROR("Failed to copy to clipboard: %s", SDL_GetError());
    }

    // Clear selection after copy
    m_terminal->CancelSelection();
}

void TerminalTab::PasteClipboard() {
    if (!m_pty || !m_open) return;

    // Get clipboard text via SDL
    if (!SDL_HasClipboardText()) return;

    char* text = SDL_GetClipboardText();
    if (!text) return;

    size_t len = strlen(text);
    if (len > 0) {
        m_pty->Write(text, len);
        LOG_DEBUG("Pasted %zu bytes from clipboard to PTY", len);
    }
    SDL_free(text);
}

// ---------------------------------------------------------------------------
// Terminal Rendering
// ---------------------------------------------------------------------------

void TerminalTab::RenderTerminal() {
    ImVec2 avail = ImGui::GetContentRegionAvail();
    TermLayout layout = ComputeLayout(avail);
    if (layout.size.x <= 0 || layout.size.y <= 0) return;

    // Begin a child window to track focus
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(0, 0));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0);
    ImGui::BeginChild("##terminal_view", layout.size, false,
        ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse);

    m_focused = ImGui::IsWindowFocused(ImGuiFocusedFlags_RootAndChildWindows);

    // Resize terminal emulator if window size changed
    if (m_terminal) {
        m_terminal->Resize(layout.visibleCols, layout.visibleRows);

        // Render
        ImVec2 renderPos = ImGui::GetCursorScreenPos();
        m_terminal->Render(renderPos, layout.size, m_scrollY);
    }

    // Capture scroll wheel for scrolling through buffer
    if (ImGui::IsWindowHovered()) {
        float wheel = ImGui::GetIO().MouseWheel;
        if (wheel != 0.0f) {
            m_scrollY = std::max(0.0f, m_scrollY - wheel * 0.1f);
        }
    }

    ImGui::EndChild();
    ImGui::PopStyleVar(2);
}

// ---------------------------------------------------------------------------
// Keyboard Input
// ---------------------------------------------------------------------------

void TerminalTab::ProcessKeyboardInput() {
    if (!m_pty || !m_open) return;

    auto& io = ImGui::GetIO();
    ImGuiKeyChord mods = io.KeyMods;

    // Check for Ctrl+Shift actions first (these are NOT sent to PTY)
    for (int key = ImGuiKey_A; key <= ImGuiKey_Z; key++) {
        auto imguiKey = (ImGuiKey)key;
        if (!ImGui::IsKeyPressed(imguiKey, false)) continue;

        auto action = KeyMapping::GetAction(imguiKey, mods);
        switch (action) {
            case KeyMapping::Action::Copy:
                CopySelection();
                return;
            case KeyMapping::Action::Paste:
                PasteClipboard();
                return;
            case KeyMapping::Action::NewTab:
                // Handled by TerminalManager
                return;
            case KeyMapping::Action::Clear: {
                // Send clear screen sequence (Ctrl+L equivalent)
                const char* clearSeq = "\x0c";
                m_pty->Write(clearSeq, 1);
                return;
            }
            default:
                break;
        }
    }

    // Send regular character input (skip if Ctrl/Alt/Super held)
    bool ctrlHeld = (mods & ImGuiMod_Ctrl) != 0;
    bool altHeld = (mods & ImGuiMod_Alt) != 0;
    bool superHeld = (mods & ImGuiMod_Super) != 0;

    if (!ctrlHeld && !altHeld && !superHeld) {
        for (int i = 0; i < io.InputQueueCharacters.Size; i++) {
            unsigned int c = io.InputQueueCharacters[i];
            char utf8[8] = {};
            int len = 0;
            if (c < 0x80) {
                utf8[len++] = (char)c;
            } else if (c < 0x800) {
                utf8[len++] = 0xC0 | (c >> 6);
                utf8[len++] = 0x80 | (c & 0x3F);
            } else if (c < 0x10000) {
                utf8[len++] = 0xE0 | (c >> 12);
                utf8[len++] = 0x80 | ((c >> 6) & 0x3F);
                utf8[len++] = 0x80 | (c & 0x3F);
            } else {
                utf8[len++] = 0xF0 | (c >> 18);
                utf8[len++] = 0x80 | ((c >> 12) & 0x3F);
                utf8[len++] = 0x80 | ((c >> 6) & 0x3F);
                utf8[len++] = 0x80 | (c & 0x3F);
            }
            if (len > 0)
                m_pty->Write(utf8, (size_t)len);
        }
    }

    // Send special key sequences (only check relevant keys for performance)
    static const ImGuiKey specialKeys[] = {
        ImGuiKey_Enter, ImGuiKey_Backspace, ImGuiKey_Tab, ImGuiKey_Escape,
        ImGuiKey_UpArrow, ImGuiKey_DownArrow, ImGuiKey_LeftArrow, ImGuiKey_RightArrow,
        ImGuiKey_Home, ImGuiKey_End, ImGuiKey_PageUp, ImGuiKey_PageDown,
        ImGuiKey_Insert, ImGuiKey_Delete,
        ImGuiKey_F1, ImGuiKey_F2, ImGuiKey_F3, ImGuiKey_F4, ImGuiKey_F5, ImGuiKey_F6,
        ImGuiKey_F7, ImGuiKey_F8, ImGuiKey_F9, ImGuiKey_F10, ImGuiKey_F11, ImGuiKey_F12,
        ImGuiKey_LeftBracket, ImGuiKey_RightBracket, ImGuiKey_Backslash, ImGuiKey_Slash
    };

    // Also check A-Z for Ctrl+letter combos (but skip Ctrl+Shift which was handled above)
    for (int key = ImGuiKey_A; key <= ImGuiKey_Z; key++) {
        auto imguiKey = (ImGuiKey)key;
        if (!ImGui::IsKeyPressed(imguiKey, false)) continue;

        // Skip if it's a Ctrl+Shift action (already handled above)
        if (KeyMapping::GetAction(imguiKey, mods) != KeyMapping::Action::None)
            continue;

        // Send VT sequence to PTY
        const char* seq = KeyMapping::ToVtSequence(imguiKey, mods);
        if (seq) {
            m_pty->Write(seq, strlen(seq));
        }
    }

    for (auto imguiKey : specialKeys) {
        if (ImGui::IsKeyPressed(imguiKey, false)) {
            const char* seq = KeyMapping::ToVtSequence(imguiKey, mods);
            if (seq) {
                m_pty->Write(seq, strlen(seq));
            }
        }
    }
}

// ---------------------------------------------------------------------------
// Status Bar
// ---------------------------------------------------------------------------

void TerminalTab::RenderStatusBar() {
    ImGui::Separator();

    std::string status;
    if (m_open) {
        status = m_type == Type::LocalShell ? "Local Terminal" : "SSH Connected";
        if (m_terminal) {
            int sbSize = m_terminal->GetScrollbackRows();
            if (sbSize > 0) {
                status += " | " + std::to_string(m_terminal->GetCols()) + "x" +
                          std::to_string(m_terminal->GetRows()) +
                          " | buffer: " + std::to_string(sbSize) + " lines";
            } else {
                status += " | " + std::to_string(m_terminal->GetCols()) + "x" +
                          std::to_string(m_terminal->GetRows());
            }
        }
        if (m_terminal && m_terminal->HasSelection()) {
            status += " | [selected]";
        }
    } else {
        status = "Disconnected";
    }

    ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "%s", status.c_str());

    // Show copy/paste shortcuts hint on hover
    if (ImGui::IsItemHovered()) {
        ImGui::SetTooltip("Ctrl+Shift+C  Copy selection\n"
                          "Ctrl+Shift+V  Paste\n"
                          "Ctrl+Shift+L  Clear screen\n"
                          "Mouse drag    Select text");
    }
}

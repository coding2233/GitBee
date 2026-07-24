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
        m_reconnectPending = true;  // Enable session recovery
        LOG_INFO("Terminal PTY closed, reconnect pending");
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
    m_reconnectPending = false;
}

void TerminalTab::Reconnect() {
    if (!m_reconnectPending) return;
    if (m_pty) {
        m_pty->Close();
        m_pty.reset();
    }

    // Recreate PTY based on type
    if (m_type == Type::LocalShell) {
        m_pty = std::make_unique<LocalPty>();
    } else {
        m_pty = std::make_unique<SshPty>();
    }

    // Try to restart with same config
    PtyConfig cfg;
    cfg.cols = 80;
    cfg.rows = 24;
    if (!m_workingDir.empty())
        cfg.workingDir = m_workingDir;

    if (m_pty->Start(cfg)) {
        m_open = true;
        m_reconnectPending = false;
        LOG_INFO("TerminalTab reconnected: %s", m_title.c_str());

        // Recreate terminal emulator if needed
        if (!m_terminal) {
            m_terminal = std::make_unique<TerminalEmulator>(80, 24);
            m_terminal->OnTitleChange = [this](const std::string& title) {
                if (!title.empty()) {
                    m_title = title;
                    if (OnTitleChange) OnTitleChange(title);
                }
            };
        }

        m_pty->OnClosed = [this]() {
            m_open = false;
            m_reconnectPending = true;
            LOG_INFO("Terminal PTY closed after reconnect");
        };
    } else {
        LOG_ERROR("Failed to reconnect terminal");
        m_pty.reset();
    }
}

void TerminalTab::Render() {
    if ((!m_open || !m_terminal) && !m_reconnectPending) {
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

        // Show reconnect button
        if (m_reconnectPending) {
            float btnY = textPos.y + textSize.y + 8;
            ImVec2 btnPos(pos.x + (avail.x - 120) * 0.5f, btnY);
            ImGui::SetCursorScreenPos(btnPos);
            if (ImGui::Button("Reconnect", ImVec2(120, 0))) {
                Reconnect();
            }
        }
        return;
    }

    // Auto-reconnect if pending
    if (m_reconnectPending) {
        Reconnect();
        if (!m_open) return;  // Reconnect failed
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

    // Render search overlay if active
    if (m_searchActive) {
        RenderSearchOverlay();
    }
}

void TerminalTab::ProcessPtyOutput() {
    if (!m_pty || !m_terminal) return;

    char buf[4096];
    int n;
    while ((n = m_pty->Read(buf, sizeof(buf))) > 0) {
        m_terminal->ProcessInput(buf, (size_t)n);
    }

    if (n < 0) {
        LOG_DEBUG("Terminal PTY read returned %d, closing", n);
        m_open = false;
        m_reconnectPending = true;
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
    // Reserve space for search bar if active
    float searchH = m_searchActive ? (ImGui::GetFrameHeight() + 8) : 0;
    layout.size = ImVec2(availSize.x, availSize.y - statusH - searchH);

    if (m_terminal) {
        auto* font = ImGui::GetFont();
        layout.cellWidth = font->CalcTextSizeA(ImGui::GetFontSize(), FLT_MAX, -1.0f, "M", nullptr, nullptr).x;
        layout.cellHeight = ImGui::GetTextLineHeight();
        layout.visibleCols = std::max(16, (int)(layout.size.x / layout.cellWidth));
        layout.visibleRows = std::max(4, (int)(layout.size.y / layout.cellHeight));

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
    TermLayout layout = ComputeLayout(ImGui::GetContentRegionAvail());
    if (layout.cellWidth <= 0 || layout.cellHeight <= 0) return false;

    float dx = mousePos.x - layout.pos.x;
    float dy = mousePos.y - layout.pos.y;

    if (dx < 0 || dy < 0 || dx >= layout.size.x || dy >= layout.size.y)
        return false;

    col = (int)(dx / layout.cellWidth);
    row = (int)(dy / layout.cellHeight);

    col = std::max(0, std::min(col, layout.visibleCols - 1));
    row = std::max(0, std::min(row, layout.visibleRows - 1));

    // Convert to buffer coordinates
    row += layout.scrollRow;

    return true;
}

// ---------------------------------------------------------------------------
// Mouse Input (Selection + Right-click menu)
// ---------------------------------------------------------------------------

void TerminalTab::ProcessMouseInput() {
    if (!m_terminal || !m_open) return;

    auto& io = ImGui::GetIO();

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

    // Right click: context menu OR cancel selection
    if (ImGui::IsMouseClicked(ImGuiMouseButton_Right)) {
        if (m_terminal->HasSelection()) {
            m_terminal->CancelSelection();
            m_mouseSelecting = false;
            m_mouseDragging = false;
        }
        // Open context menu on right click
        if (ImGui::IsWindowHovered()) {
            ImGui::OpenPopup("##terminal_context");
        }
    }

    // Render right-click context menu
    if (ImGui::BeginPopup("##terminal_context")) {
        if (ImGui::MenuItem("Copy", "Ctrl+Shift+C", false, m_terminal->HasSelection())) {
            CopySelection();
            ImGui::CloseCurrentPopup();
        }
        if (ImGui::MenuItem("Paste", "Ctrl+Shift+V")) {
            PasteClipboard();
            ImGui::CloseCurrentPopup();
        }
        ImGui::Separator();
        if (ImGui::MenuItem("Clear Screen", "Ctrl+Shift+L")) {
            if (m_pty) {
                const char* clearSeq = "\x0c";
                m_pty->Write(clearSeq, 1);
            }
            ImGui::CloseCurrentPopup();
        }
        if (ImGui::MenuItem("Find...", "Ctrl+F")) {
            m_searchActive = true;
            m_searchText[0] = '\0';
            ImGui::CloseCurrentPopup();
        }
        ImGui::Separator();
        if (ImGui::MenuItem("Select All")) {
            if (m_terminal) {
                int sbSize = m_terminal->GetScrollbackRows();
                int totalRows = sbSize + m_terminal->GetRows();
                m_terminal->BeginSelection(0, 0);
                m_terminal->UpdateSelection(totalRows - 1, m_terminal->GetCols() - 1);
                m_terminal->EndSelection();
            }
            ImGui::CloseCurrentPopup();
        }
        ImGui::Separator();
        if (ImGui::MenuItem("Reconnect", nullptr, false, m_reconnectPending || !m_open)) {
            Reconnect();
            ImGui::CloseCurrentPopup();
        }
        if (ImGui::MenuItem("Close Tab", "Ctrl+Shift+W")) {
            Close();
            ImGui::CloseCurrentPopup();
        }
        ImGui::EndPopup();
    }
}

// ---------------------------------------------------------------------------
// Search Overlay
// ---------------------------------------------------------------------------

void TerminalTab::RenderSearchOverlay() {
    ImVec2 avail = ImGui::GetContentRegionAvail();
    float searchH = ImGui::GetFrameHeight() + 8;
    ImVec2 searchPos = ImGui::GetCursorScreenPos();
    searchPos.y += avail.y - searchH;

    ImGui::SetCursorScreenPos(searchPos);

    // Search bar background
    auto* dl = ImGui::GetWindowDrawList();
    dl->AddRectFilled(searchPos, ImVec2(searchPos.x + avail.x, searchPos.y + searchH),
                      IM_COL32(30, 30, 40, 230));

    ImGui::PushStyleVar(ImGuiStyleVar_FramePadding, ImVec2(4, 2));
    ImGui::PushStyleVar(ImGuiStyleVar_ItemSpacing, ImVec2(4, 0));

    // Search input
    ImGui::SetCursorScreenPos(ImVec2(searchPos.x + 4, searchPos.y + 4));
    ImGui::PushItemWidth(220);
    ImGui::InputTextWithHint("##terminal_search", "Find...",
                             m_searchText, sizeof(m_searchText));
    ImGui::PopItemWidth();

    ImGui::SameLine();

    // Match count
    ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "%d/%d",
                       m_searchCurrentMatch, m_searchTotalMatches);

    // Navigation buttons
    ImGui::SameLine();
    if (ImGui::SmallButton("<")) {
        m_searchCurrentMatch = std::max(1, m_searchCurrentMatch - 1);
    }
    ImGui::SameLine();
    if (ImGui::SmallButton(">")) {
        m_searchCurrentMatch = std::min(m_searchTotalMatches, m_searchCurrentMatch + 1);
    }
    ImGui::SameLine();

    // Case-sensitive toggle
    ImGui::Checkbox("Aa", &m_searchCaseSensitive);
    if (ImGui::IsItemHovered())
        ImGui::SetTooltip("Case sensitive");

    ImGui::SameLine();

    // Close button
    if (ImGui::SmallButton("x")) {
        m_searchActive = false;
        m_searchText[0] = '\0';
    }

    ImGui::PopStyleVar(2);

    // Perform search (placeholder - full implementation would scan cells for matches)
    if (m_searchText[0] != '\0') {
        // Search logic would be here in a full implementation
        // For now, just show the overlay UI
    }
}

// ---------------------------------------------------------------------------
// Copy / Paste
// ---------------------------------------------------------------------------

void TerminalTab::CopySelection() {
    if (!m_terminal || !m_terminal->HasSelection()) return;

    std::string text = m_terminal->GetSelectedText();
    if (text.empty()) return;

    if (SDL_SetClipboardText(text.c_str()) == 0) {
        LOG_DEBUG("Copied %zu bytes from terminal selection", text.size());
    } else {
        LOG_ERROR("Failed to copy to clipboard: %s", SDL_GetError());
    }

    m_terminal->CancelSelection();
}

void TerminalTab::PasteClipboard() {
    if (!m_pty || !m_open) return;

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
            // If scrolled to bottom, reset to bottom
            if (m_scrollY <= 0.01f) {
                if (m_terminal) {
                    int sbSize = m_terminal->GetScrollbackRows();
                    int totalRows = sbSize + m_terminal->GetRows();
                    int maxScroll = std::max(0, totalRows - layout.visibleRows);
                    if (m_scrollY <= 0.01f && maxScroll > 0) {
                        // At bottom - auto scroll on new output
                    }
                }
            }
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

    // Check for Ctrl+Shift actions first
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
                return;  // Handled by TerminalManager
            case KeyMapping::Action::Find:
                m_searchActive = !m_searchActive;
                if (m_searchActive) m_searchText[0] = '\0';
                return;
            case KeyMapping::Action::Clear: {
                const char* clearSeq = "\x0c";
                m_pty->Write(clearSeq, 1);
                return;
            }
            case KeyMapping::Action::CloseTab:
                Close();
                return;
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

    // Send special key sequences
    static const ImGuiKey specialKeys[] = {
        ImGuiKey_Enter, ImGuiKey_Backspace, ImGuiKey_Tab, ImGuiKey_Escape,
        ImGuiKey_UpArrow, ImGuiKey_DownArrow, ImGuiKey_LeftArrow, ImGuiKey_RightArrow,
        ImGuiKey_Home, ImGuiKey_End, ImGuiKey_PageUp, ImGuiKey_PageDown,
        ImGuiKey_Insert, ImGuiKey_Delete,
        ImGuiKey_F1, ImGuiKey_F2, ImGuiKey_F3, ImGuiKey_F4, ImGuiKey_F5, ImGuiKey_F6,
        ImGuiKey_F7, ImGuiKey_F8, ImGuiKey_F9, ImGuiKey_F10, ImGuiKey_F11, ImGuiKey_F12,
        ImGuiKey_LeftBracket, ImGuiKey_RightBracket, ImGuiKey_Backslash, ImGuiKey_Slash
    };

    for (int key = ImGuiKey_A; key <= ImGuiKey_Z; key++) {
        auto imguiKey = (ImGuiKey)key;
        if (!ImGui::IsKeyPressed(imguiKey, false)) continue;

        if (KeyMapping::GetAction(imguiKey, mods) != KeyMapping::Action::None)
            continue;

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
    } else if (m_reconnectPending) {
        status = "Disconnected - click to reconnect";
        ImGui::PushStyleColor(ImGuiCol_Text, ImVec4(1.0f, 0.6f, 0.0f, 1.0f));
        ImGui::TextUnformatted(status.c_str());
        ImGui::PopStyleColor();
        // Reconnect button
        ImGui::SameLine();
        if (ImGui::SmallButton("Reconnect")) {
            Reconnect();
        }
        return;
    } else {
        status = "Disconnected";
    }

    ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "%s", status.c_str());

    if (ImGui::IsItemHovered()) {
        ImGui::SetTooltip("Ctrl+Shift+C  Copy selection\n"
                          "Ctrl+Shift+V  Paste\n"
                          "Ctrl+Shift+L  Clear screen\n"
                          "Ctrl+F        Search\n"
                          "Mouse drag    Select text\n"
                          "Right-click   Context menu");
    }
}

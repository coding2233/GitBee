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

void TerminalTab::RenderTerminal() {
    ImVec2 avail = ImGui::GetContentRegionAvail();
    ImVec2 pos = ImGui::GetCursorScreenPos();

    // Reserve space for status bar
    float statusH = ImGui::GetFrameHeight() + 4;
    ImVec2 termSize(avail.x, avail.y - statusH);

    if (termSize.x <= 0 || termSize.y <= 0) return;

    // Begin a child window to track focus
    ImGui::PushStyleVar(ImGuiStyleVar_WindowPadding, ImVec2(0, 0));
    ImGui::PushStyleVar(ImGuiStyleVar_WindowBorderSize, 0);
    ImGui::BeginChild("##terminal_view", termSize, false,
        ImGuiWindowFlags_NoScrollbar | ImGuiWindowFlags_NoScrollWithMouse);

    m_focused = ImGui::IsWindowFocused(ImGuiFocusedFlags_RootAndChildWindows);

    // Resize terminal emulator if window size changed
    if (m_terminal) {
        auto* font = ImGui::GetFont();
        float cellW = font->CalcTextSizeA(ImGui::GetFontSize(), FLT_MAX, -1.0f, "M", nullptr, nullptr).x;
        float cellH = ImGui::GetTextLineHeight();
        int newCols = std::max(16, (int)(termSize.x / cellW));
        int newRows = std::max(4, (int)(termSize.y / cellH));
        m_terminal->Resize(newCols, newRows);

        // Render
        ImVec2 renderPos = ImGui::GetCursorScreenPos();
        m_terminal->Render(renderPos, termSize, m_scrollY);
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

void TerminalTab::ProcessKeyboardInput() {
    if (!m_pty || !m_open) return;

    auto& io = ImGui::GetIO();
    ImGuiKeyChord mods = io.KeyMods;

    // Send regular character input (skip if Ctrl/Alt/Super held to avoid duplicates)
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

    // Also check A-Z for Ctrl+letter combos
    for (int key = ImGuiKey_A; key <= ImGuiKey_Z; key++) {
        auto imguiKey = (ImGuiKey)key;
        if (ImGui::IsKeyPressed(imguiKey, false)) {
            // Check for terminal actions (Ctrl+Shift+letter)
            auto action = KeyMapping::GetAction(imguiKey, mods);
            if (action != KeyMapping::Action::None)
                continue;

            // Send VT sequence to PTY
            const char* seq = KeyMapping::ToVtSequence(imguiKey, mods);
            if (seq) {
                m_pty->Write(seq, strlen(seq));
            }
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

void TerminalTab::RenderStatusBar() {
    ImVec2 avail = ImGui::GetContentRegionAvail();

    ImGui::Separator();

    std::string status;
    if (m_open) {
        status = m_type == Type::LocalShell ? "Local Terminal" : "SSH Connected";
        if (m_terminal) {
            status += " | " + std::to_string(m_terminal->GetCols()) + "x" +
                      std::to_string(m_terminal->GetRows());
        }
    } else {
        status = "Disconnected";
    }

    ImVec2 textSize = ImGui::CalcTextSize(status.c_str());
    ImGui::TextColored(ImVec4(0.5f, 0.5f, 0.5f, 1.0f), "%s", status.c_str());
}

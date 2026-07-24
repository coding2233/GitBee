#pragma once

#include <string>
#include <vector>
#include <functional>
#include <imgui.h>
#include <vterm.h>

/// Parses ANSI/VT100 output via libvterm and renders the cell grid with ImGui.
class TerminalEmulator {
public:
    TerminalEmulator(int cols = 80, int rows = 24);
    ~TerminalEmulator();

    /// Feed raw bytes from PTY output into libvterm for parsing.
    void ProcessInput(const char* data, size_t len);

    /// Resize the terminal dimensions.
    void Resize(int cols, int rows);

    /// Render the terminal content using ImGui's draw list.
    /// @param pos       Screen position (top-left of terminal area)
    /// @param size      Screen size of the terminal area
    /// @param scrollY   Vertical scroll offset (0 = bottom, >0 = scroll up)
    void Render(ImVec2 pos, ImVec2 size, float scrollY);

    /// Getters
    int GetCols() const { return m_cols; }
    int GetRows() const { return m_rows; }
    const VTermScreenCell* GetCell(int col, int row) const;
    int GetCursorRow() const { return m_cursorRow; }
    int GetCursorCol() const { return m_cursorCol; }
    bool IsCursorVisible() const { return m_cursorVisible; }

    /// Get total scrollback buffer size in rows.
    int GetScrollbackRows() const;

    /// Emitted when terminal title changes (e.g., OSC 0/1/2 escape).
    std::function<void(const std::string& title)> OnTitleChange;

private:
    VTerm* m_vterm = nullptr;
    VTermScreen* m_screen = nullptr;
    int m_cols = 80;
    int m_rows = 24;
    int m_cursorRow = 0;
    int m_cursorCol = 0;
    bool m_cursorVisible = true;
    std::string m_title;

    /// Cached cell data for efficient rendering.
    mutable std::vector<VTermScreenCell> m_cellCache;
    mutable bool m_cellCacheValid = false;

    // libvterm callbacks (static, forward to instance)
    static int Damage(VTermRect rect, void* user);
    static int MoveCursor(VTermPos pos, VTermPos oldPos, int visible, void* user);
    static int SetTermProp(VTermProp prop, VTermValue* val, void* user);

    void InvalidateCache();
    void BuildCellCache() const;

    // Rendering helpers
    ImU32 VTermColorToImU32(const VTermScreenCell& cell, bool isForeground) const;
};

namespace ImTerm {
    /// Convert a VTermColor (libvterm) to an ImU32 (ImGui).
    inline ImU32 VTermColorToU32(unsigned char r, unsigned char g, unsigned char b) {
        return IM_COL32(r, g, b, 255);
    }
}

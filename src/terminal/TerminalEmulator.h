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
    int GetScrollbackRows() const { return (int)m_scrollback.size(); }

    // ----- Selection API -----
    /// Begin selection at a grid position (screen coordinates, not scrollback-adjusted).
    void BeginSelection(int row, int col);

    /// Extend selection to a grid position.
    void UpdateSelection(int row, int col);

    /// Finalize selection.
    void EndSelection();

    /// Cancel current selection.
    void CancelSelection() { m_selecting = false; m_hasSelection = false; }

    /// Whether there's an active selection.
    bool HasSelection() const { return m_hasSelection; }

    /// Whether the user is currently drag-selecting.
    bool IsSelecting() const { return m_selecting; }

    /// Extract the selected text as a UTF-8 string.
    std::string GetSelectedText() const;

    /// Get selection start/end normalized (min/max).
    void GetSelectionRange(int& startRow, int& startCol, int& endRow, int& endCol) const;

    // ----- Scrollback API -----
    /// Get a cell from scrollback buffer. Returns nullptr if out of range.
    const VTermScreenCell* GetScrollbackCell(int row, int col) const;

    /// Get the row count in the visible screen.
    int GetVisibleRows() const { return m_rows; }

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

    // ----- Scrollback buffer -----
    /// Each row is a vector of cells for one line of scrollback.
    mutable std::vector<std::vector<VTermScreenCell>> m_scrollback;
    static constexpr int MAX_SCROLLBACK_ROWS = 1000;

    // ----- Selection state -----
    bool m_selecting = false;
    bool m_hasSelection = false;
    int m_selStartRow = 0;
    int m_selStartCol = 0;
    int m_selEndRow = 0;
    int m_selEndCol = 0;

    // libvterm callbacks (static, forward to instance)
    static int Damage(VTermRect rect, void* user);
    static int MoveCursor(VTermPos pos, VTermPos oldPos, int visible, void* user);
    static int SetTermProp(VTermProp prop, VTermValue* val, void* user);
    static int ScrollbackPushLine(int cols, const VTermScreenCell* cells, void* user);
    static int ScrollbackPopLine(int cols, VTermScreenCell* cells, void* user);
    static int ScrollbackClear(void* user);

    void InvalidateCache();
    void BuildCellCache() const;

    // Rendering helpers
    ImU32 VTermColorToImU32(const VTermScreenCell& cell, bool isForeground) const;
    bool IsCellSelected(int row, int col) const;

    /// Convert a screen row (0 = top of visible) to a scrollback-adjusted row.
    int ScreenRowToBufferRow(int screenRow) const;
};

namespace ImTerm {
    /// Convert a VTermColor (libvterm) to an ImU32 (ImGui).
    inline ImU32 VTermColorToU32(unsigned char r, unsigned char g, unsigned char b) {
        return IM_COL32(r, g, b, 255);
    }
}

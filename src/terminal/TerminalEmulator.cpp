#include "TerminalEmulator.h"
#include "../dbg_log.h"
#include <vterm.h>
#include <cstring>
#include <algorithm>
#include <cmath>

// ---------------------------------------------------------------------------
// Construction / Destruction
// ---------------------------------------------------------------------------

TerminalEmulator::TerminalEmulator(int cols, int rows)
    : m_cols(cols), m_rows(rows) {
    m_vterm = vterm_new(rows, cols);
    if (!m_vterm) {
        LOG_ERROR("Failed to create VTerm");
        return;
    }

    vterm_set_utf8(m_vterm, 1);

    m_screen = vterm_obtain_screen(m_vterm);
    if (!m_screen) {
        LOG_ERROR("Failed to obtain VTermScreen");
        vterm_free(m_vterm);
        m_vterm = nullptr;
        return;
    }

    // Set screen callbacks — including scrollback
    VTermScreenCallbacks cbs = {0};
    cbs.damage = Damage;
    cbs.movecursor = MoveCursor;
    cbs.settermprop = SetTermProp;
    cbs.sb_pushline = ScrollbackPushLine;
    cbs.sb_popline = ScrollbackPopLine;
    cbs.sb_clear = ScrollbackClear;
    vterm_screen_set_callbacks(m_screen, &cbs, this);

    // Enable reflow and alt screen
    vterm_screen_enable_reflow(m_screen, true);
    vterm_screen_enable_altscreen(m_screen, 1);

    vterm_screen_reset(m_screen, 1);

    // Pre-allocate cell cache
    m_cellCache.resize((size_t)(cols * rows));
    m_scrollback.reserve(256);

    LOG_DEBUG("TerminalEmulator created: %dx%d", cols, rows);
}

TerminalEmulator::~TerminalEmulator() {
    if (m_vterm) {
        vterm_free(m_vterm);
        m_vterm = nullptr;
        m_screen = nullptr;
    }
}

// ---------------------------------------------------------------------------
// Input Processing (from PTY)
// ---------------------------------------------------------------------------

void TerminalEmulator::ProcessInput(const char* data, size_t len) {
    if (!m_vterm) return;
    vterm_input_write(m_vterm, data, len);
}

// ---------------------------------------------------------------------------
// Resize
// ---------------------------------------------------------------------------

void TerminalEmulator::Resize(int cols, int rows) {
    if (!m_vterm) return;
    if (cols == m_cols && rows == m_rows) return;

    m_cols = cols;
    m_rows = rows;
    vterm_set_size(m_vterm, rows, cols);
    InvalidateCache();
}

// ---------------------------------------------------------------------------
// Cell Access
// ---------------------------------------------------------------------------

const VTermScreenCell* TerminalEmulator::GetCell(int col, int row) const {
    if (!m_screen) return nullptr;
    if (col < 0 || col >= m_cols || row < 0 || row >= m_rows) return nullptr;

    if (!m_cellCacheValid)
        BuildCellCache();

    size_t idx = (size_t)(row * m_cols + col);
    if (idx < m_cellCache.size())
        return &m_cellCache[idx];
    return nullptr;
}

const VTermScreenCell* TerminalEmulator::GetScrollbackCell(int row, int col) const {
    if (row < 0 || row >= (int)m_scrollback.size()) return nullptr;
    if (col < 0 || col >= m_cols) return nullptr;
    const auto& sbRow = m_scrollback[row];
    if ((size_t)col >= sbRow.size()) return nullptr;
    return &sbRow[col];
}

int TerminalEmulator::ScreenRowToBufferRow(int screenRow) const {
    // In the current render, screenRow is the visible row index.
    // Scrollback rows are "above" the screen, so the buffer row is:
    // scrollback.size() + screenRow (when scrollY == 0).
    // But when scrolled back, visible rows show both scrollback + screen.
    // This is handled inside Render() since it knows the scrollY offset.
    return (int)m_scrollback.size() + screenRow;
}

// ---------------------------------------------------------------------------
// Scrollback Callbacks
// ---------------------------------------------------------------------------

int TerminalEmulator::ScrollbackPushLine(int cols, const VTermScreenCell* cells, void* user) {
    auto* self = (TerminalEmulator*)user;
    if ((int)self->m_scrollback.size() >= self->MAX_SCROLLBACK_ROWS) {
        // Remove oldest line
        self->m_scrollback.erase(self->m_scrollback.begin());
    }
    // Copy the line into scrollback
    std::vector<VTermScreenCell> row(cells, cells + cols);
    self->m_scrollback.push_back(std::move(row));
    return 1;
}

int TerminalEmulator::ScrollbackPopLine(int cols, VTermScreenCell* cells, void* user) {
    auto* self = (TerminalEmulator*)user;
    if (self->m_scrollback.empty()) return 0;
    const auto& row = self->m_scrollback.back();
    size_t copyLen = std::min((size_t)cols, row.size());
    memcpy(cells, row.data(), copyLen * sizeof(VTermScreenCell));
    self->m_scrollback.pop_back();
    return 1;
}

int TerminalEmulator::ScrollbackClear(void* user) {
    auto* self = (TerminalEmulator*)user;
    self->m_scrollback.clear();
    return 1;
}

// ---------------------------------------------------------------------------
// Selection API
// ---------------------------------------------------------------------------

void TerminalEmulator::BeginSelection(int row, int col) {
    m_selecting = true;
    m_selStartRow = row;
    m_selStartCol = col;
    m_selEndRow = row;
    m_selEndCol = col;
    m_hasSelection = true;
}

void TerminalEmulator::UpdateSelection(int row, int col) {
    if (!m_selecting) return;
    m_selEndRow = row;
    m_selEndCol = col;
}

void TerminalEmulator::EndSelection() {
    m_selecting = false;
    // Normalize: ensure start <= end
    if (m_selStartRow > m_selEndRow ||
        (m_selStartRow == m_selEndRow && m_selStartCol > m_selEndCol)) {
        std::swap(m_selStartRow, m_selEndRow);
        std::swap(m_selStartCol, m_selEndCol);
    }
}

void TerminalEmulator::GetSelectionRange(int& startRow, int& startCol,
                                          int& endRow, int& endCol) const {
    startRow = m_selStartRow;
    startCol = m_selStartCol;
    endRow = m_selEndRow;
    endCol = m_selEndCol;
    // Normalize
    if (startRow > endRow || (startRow == endRow && startCol > endCol)) {
        std::swap(startRow, endRow);
        std::swap(startCol, endCol);
    }
}

bool TerminalEmulator::IsCellSelected(int row, int col) const {
    if (!m_hasSelection) return false;

    int sr = m_selStartRow, sc = m_selStartCol;
    int er = m_selEndRow, ec = m_selEndCol;
    if (sr > er || (sr == er && sc > ec)) {
        std::swap(sr, er);
        std::swap(sc, ec);
    }

    if (row < sr || row > er) return false;
    if (row == sr && col < sc) return false;
    if (row == er && col > ec) return false;
    return true;
}

std::string TerminalEmulator::GetSelectedText() const {
    if (!m_hasSelection || !m_screen) return {};

    int sr, sc, er, ec;
    GetSelectionRange(sr, sc, er, ec);

    // We need to figure out if the selection spans the scrollback buffer
    // or only the visible screen. sr/er are in scrollback-adjusted coordinates.
    int sbSize = (int)m_scrollback.size();
    std::string result;

    for (int row = sr; row <= er; row++) {
        std::string line;
        if (row < sbSize) {
            // Row is in scrollback buffer
            const auto& sbRow = m_scrollback[row];
            int startCol = (row == sr) ? sc : 0;
            int endCol = (row == er) ? ec : (int)sbRow.size() - 1;
            for (int c = startCol; c <= endCol && c < (int)sbRow.size(); c++) {
                // Convert cell chars to UTF-8
                char utf8[8];
                int len = 0;
                uint32_t cp = sbRow[c].chars[0];
                if (cp == 0) cp = ' ';
                if (cp < 0x80) {
                    utf8[len++] = (char)cp;
                } else if (cp < 0x800) {
                    utf8[len++] = 0xC0 | (cp >> 6);
                    utf8[len++] = 0x80 | (cp & 0x3F);
                } else if (cp < 0x10000) {
                    utf8[len++] = 0xE0 | (cp >> 12);
                    utf8[len++] = 0x80 | ((cp >> 6) & 0x3F);
                    utf8[len++] = 0x80 | (cp & 0x3F);
                } else {
                    utf8[len++] = 0xF0 | (cp >> 18);
                    utf8[len++] = 0x80 | ((cp >> 12) & 0x3F);
                    utf8[len++] = 0x80 | ((cp >> 6) & 0x3F);
                    utf8[len++] = 0x80 | (cp & 0x3F);
                }
                line.append(utf8, len);
            }
        } else {
            // Row is on the visible screen
            int screenRow = row - sbSize;
            if (screenRow >= m_rows) break;
            int startCol = (row == sr) ? sc : 0;
            int endCol = (row == er) ? ec : m_cols - 1;
            // Use vterm_screen_get_text for cleaner extraction
            VTermRect rect;
            rect.start_row = screenRow;
            rect.end_row = screenRow + 1;
            rect.start_col = startCol;
            rect.end_col = endCol + 1;
            char buf[4096];
            size_t n = vterm_screen_get_text(m_screen, buf, sizeof(buf) - 1, rect);
            buf[n] = '\0';
            line = buf;
        }

        // Trim trailing whitespace for non-last lines
        if (row < er) {
            while (!line.empty() && (line.back() == ' ' || line.back() == '\0'))
                line.pop_back();
            line += "\r\n";
        }
        result += line;
    }

    return result;
}

// ---------------------------------------------------------------------------
// Rendering
// ---------------------------------------------------------------------------

void TerminalEmulator::Render(ImVec2 pos, ImVec2 size, float scrollY) {
    if (!m_screen) {
        auto* dl = ImGui::GetWindowDrawList();
        dl->AddRectFilled(pos, ImVec2(pos.x + size.x, pos.y + size.y),
                          IM_COL32(16, 16, 20, 255));
        dl->AddText(ImVec2(pos.x + 8, pos.y + 8), IM_COL32(128, 128, 128, 255),
                    "Terminal not initialized");
        return;
    }

    // Ensure cell cache is up to date
    if (!m_cellCacheValid)
        BuildCellCache();

    auto* dl = ImGui::GetWindowDrawList();
    auto* font = ImGui::GetFont();
    float fontSize = ImGui::GetFontSize();
    float cellWidth = font->CalcTextSizeA(fontSize, FLT_MAX, -1.0f, "M", nullptr, nullptr).x;
    float cellHeight = ImGui::GetTextLineHeight();
    float lineSpacing = ImGui::GetTextLineHeightWithSpacing();

    // Compute visible rows/cols
    int visibleCols = std::min(m_cols, (int)(size.x / cellWidth));
    int visibleRows = std::min(m_rows, (int)(size.y / cellHeight));
    if (visibleCols <= 0 || visibleRows <= 0) return;

    int sbSize = (int)m_scrollback.size();
    int totalRows = sbSize + m_rows;

    // Scroll offset: 0 = bottom (show only visible screen), max = show top of scrollback
    int maxScroll = std::max(0, totalRows - visibleRows);
    int scrollRow = (int)(scrollY * maxScroll + 0.5f);
    scrollRow = std::max(0, std::min(scrollRow, maxScroll));

    // Default colors (matching the app's dark theme)
    ImU32 defaultBg = IM_COL32(16, 16, 20, 255);
    ImU32 defaultFg = IM_COL32(220, 220, 220, 255);
    ImU32 selectionBg = IM_COL32(60, 80, 120, 255);  // blue-tinted selection

    // Fill background
    dl->AddRectFilled(pos, ImVec2(pos.x + size.x, pos.y + size.y), defaultBg);

    // Track cursor position for rendering (only if not scrolled back)
    int cursorScreenRow = (scrollRow <= sbSize) ? (m_cursorRow + sbSize - scrollRow) : -1;
    int cursorScreenCol = m_cursorCol;

    // Render each visible row
    for (int r = 0; r < visibleRows; r++) {
        int bufferRow = scrollRow + r;
        float y = pos.y + r * lineSpacing;

        if (bufferRow < 0 || bufferRow >= totalRows) continue;

        bool isScrollback = (bufferRow < sbSize);
        int srcRow = isScrollback ? bufferRow : (bufferRow - sbSize);

        // Scan row and group cells with same attributes for efficient rendering
        int c = 0;
        while (c < visibleCols) {
            const VTermScreenCell* cell = nullptr;
            if (isScrollback) {
                cell = GetScrollbackCell(srcRow, c);
            } else {
                cell = GetCell(c, srcRow);
            }
            if (!cell) { c++; continue; }

            // Find run of cells with identical styles
            int runEnd = c + 1;
            while (runEnd < visibleCols) {
                const VTermScreenCell* next = nullptr;
                if (isScrollback) {
                    next = GetScrollbackCell(srcRow, runEnd);
                } else {
                    next = GetCell(runEnd, srcRow);
                }
                if (!next) break;

                // Check if styles match
                bool same = (next->bg.type == cell->bg.type &&
                    next->bg.rgb.red == cell->bg.rgb.red &&
                    next->bg.rgb.green == cell->bg.rgb.green &&
                    next->bg.rgb.blue == cell->bg.rgb.blue &&
                    next->fg.type == cell->fg.type &&
                    next->fg.rgb.red == cell->fg.rgb.red &&
                    next->fg.rgb.green == cell->fg.rgb.green &&
                    next->fg.rgb.blue == cell->fg.rgb.blue &&
                    next->attrs.bold == cell->attrs.bold &&
                    next->attrs.italic == cell->attrs.italic &&
                    next->attrs.underline == cell->attrs.underline);

                if (!same) break;
                runEnd++;
            }

            // Compute background color
            ImU32 bgColor;
            if (cell->bg.type & VTERM_COLOR_DEFAULT_BG) {
                bgColor = defaultBg;
            } else {
                VTermColor bgConv = cell->bg;
                vterm_screen_convert_color_to_rgb(m_screen, &bgConv);
                bgColor = IM_COL32(bgConv.rgb.red, bgConv.rgb.green, bgConv.rgb.blue, 255);
            }

            // Compute foreground color
            ImU32 fgColor;
            if (cell->fg.type & VTERM_COLOR_DEFAULT_FG) {
                fgColor = defaultFg;
            } else {
                VTermColor fgConv = cell->fg;
                vterm_screen_convert_color_to_rgb(m_screen, &fgConv);
                fgColor = IM_COL32(fgConv.rgb.red, fgConv.rgb.green, fgConv.rgb.blue, 255);
            }

            // Handle reverse video
            if (cell->attrs.reverse) {
                std::swap(bgColor, fgColor);
            }

            // Check selection highlight
            bool hasSelectionInRun = false;
            if (m_hasSelection) {
                for (int ci = c; ci < runEnd; ci++) {
                    if (IsCellSelected(bufferRow, ci)) {
                        hasSelectionInRun = true;
                        break;
                    }
                }
            }

            float x = pos.x + c * cellWidth;
            float runWidth = (runEnd - c) * cellWidth;

            // Draw background
            if (hasSelectionInRun) {
                dl->AddRectFilled(ImVec2(x, y), ImVec2(x + runWidth, y + cellHeight), selectionBg);
            } else if (bgColor != defaultBg) {
                dl->AddRectFilled(ImVec2(x, y), ImVec2(x + runWidth, y + cellHeight), bgColor);
            }

            // Draw text for this run
            if (fgColor != bgColor || hasSelectionInRun) {
                float textX = x;
                for (int ci = c; ci < runEnd; ci++) {
                    const VTermScreenCell* cc = nullptr;
                    if (isScrollback) {
                        cc = GetScrollbackCell(srcRow, ci);
                    } else {
                        cc = GetCell(ci, srcRow);
                    }
                    if (!cc) break;

                    char utf8Buf[32] = {};
                    int utf8Len = 0;
                    for (int chi = 0; chi < VTERM_MAX_CHARS_PER_CELL && cc->chars[chi]; chi++) {
                        uint32_t cp = cc->chars[chi];
                        if (cp < 0x80) {
                            utf8Buf[utf8Len++] = (char)cp;
                        } else if (cp < 0x800) {
                            utf8Buf[utf8Len++] = 0xC0 | (cp >> 6);
                            utf8Buf[utf8Len++] = 0x80 | (cp & 0x3F);
                        } else if (cp < 0x10000) {
                            utf8Buf[utf8Len++] = 0xE0 | (cp >> 12);
                            utf8Buf[utf8Len++] = 0x80 | ((cp >> 6) & 0x3F);
                            utf8Buf[utf8Len++] = 0x80 | (cp & 0x3F);
                        } else {
                            utf8Buf[utf8Len++] = 0xF0 | (cp >> 18);
                            utf8Buf[utf8Len++] = 0x80 | ((cp >> 12) & 0x3F);
                            utf8Buf[utf8Len++] = 0x80 | ((cp >> 6) & 0x3F);
                            utf8Buf[utf8Len++] = 0x80 | (cp & 0x3F);
                        }
                    }
                    utf8Buf[utf8Len] = '\0';

                    if (utf8Len > 0) {
                        ImU32 textColor = hasSelectionInRun ? defaultFg : fgColor;
                        dl->AddText(ImVec2(textX, y), textColor, utf8Buf);
                    }

                    textX += cc->width * cellWidth;
                    if (cc->width == 2) ci++;
                }
            }

            // Apply underline
            if (cell->attrs.underline > 0) {
                ImU32 ulColor = fgColor;
                float underlineY = y + cellHeight - 1.5f;
                if (cell->attrs.underline == VTERM_UNDERLINE_DOUBLE) {
                    dl->AddLine(ImVec2(x, underlineY - 2), ImVec2(x + runWidth, underlineY - 2), ulColor, 1.0f);
                }
                dl->AddLine(ImVec2(x, underlineY), ImVec2(x + runWidth, underlineY), ulColor, 1.0f);
            }

            // Apply strikethrough
            if (cell->attrs.strike) {
                float strikeY = y + cellHeight * 0.5f;
                dl->AddLine(ImVec2(x, strikeY), ImVec2(x + runWidth, strikeY), fgColor, 1.0f);
            }

            c = runEnd;
        }

        // Draw cursor on this row if visible
        bool cursorOnThisRow = (cursorScreenRow == r);
        if (m_cursorVisible && cursorOnThisRow && cursorScreenCol >= 0 && cursorScreenCol < m_cols) {
            float cx = pos.x + cursorScreenCol * cellWidth;
            // Blink cursor: 2 cycles per second
            float t = (float)(ImGui::GetTime() * 2.0);
            bool cursorOn = (fmod(t, 2.0) < 1.0);
            if (cursorOn) {
                dl->AddRectFilled(ImVec2(cx, y), ImVec2(cx + cellWidth, y + cellHeight),
                                  IM_COL32(200, 200, 200, 180));
            }
        }
    }
}

// ---------------------------------------------------------------------------
// libvterm Callbacks
// ---------------------------------------------------------------------------

int TerminalEmulator::Damage(VTermRect rect, void* user) {
    auto* self = (TerminalEmulator*)user;
    self->InvalidateCache();
    return 1;
}

int TerminalEmulator::MoveCursor(VTermPos pos, VTermPos oldPos, int visible, void* user) {
    auto* self = (TerminalEmulator*)user;
    self->m_cursorRow = pos.row;
    self->m_cursorCol = pos.col;
    self->m_cursorVisible = visible != 0;
    return 1;
}

int TerminalEmulator::SetTermProp(VTermProp prop, VTermValue* val, void* user) {
    auto* self = (TerminalEmulator*)user;
    if (prop == VTERM_PROP_TITLE) {
        if (val->string.str) {
            self->m_title = std::string(val->string.str, val->string.len);
            if (self->OnTitleChange)
                self->OnTitleChange(self->m_title);
        }
    }
    return 1;
}

// ---------------------------------------------------------------------------
// Cache Management
// ---------------------------------------------------------------------------

void TerminalEmulator::InvalidateCache() {
    m_cellCacheValid = false;
}

void TerminalEmulator::BuildCellCache() const {
    if (!m_screen) return;

    size_t needed = (size_t)(m_cols * m_rows);
    if (m_cellCache.size() != needed)
        m_cellCache.resize(needed);

    VTermPos pos;
    for (pos.row = 0; pos.row < m_rows; pos.row++) {
        for (pos.col = 0; pos.col < m_cols; pos.col++) {
            size_t idx = (size_t)(pos.row * m_cols + pos.col);
            if (idx >= m_cellCache.size()) break;

            int ret = vterm_screen_get_cell(m_screen, pos, &m_cellCache[idx]);
            if (!ret) {
                // Empty cell - fill with space
                memset(&m_cellCache[idx], 0, sizeof(VTermScreenCell));
                m_cellCache[idx].chars[0] = ' ';
                m_cellCache[idx].width = 1;
            }
        }
    }

    m_cellCacheValid = true;
}

// ---------------------------------------------------------------------------
// Rendering Helpers
// ---------------------------------------------------------------------------

ImU32 TerminalEmulator::VTermColorToImU32(const VTermScreenCell& cell, bool isForeground) const {
    const auto& col = isForeground ? cell.fg : cell.bg;
    VTermColor c = col;
    vterm_screen_convert_color_to_rgb(m_screen, &c);
    return IM_COL32(c.rgb.red, c.rgb.green, c.rgb.blue, 255);
}

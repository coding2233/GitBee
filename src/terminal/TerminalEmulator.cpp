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

    // Set screen callbacks
    VTermScreenCallbacks cbs = {0};
    cbs.damage = Damage;
    cbs.movecursor = MoveCursor;
    cbs.settermprop = SetTermProp;
    vterm_screen_set_callbacks(m_screen, &cbs, this);

    // Enable reflow and alt screen
    vterm_screen_enable_reflow(m_screen, true);
    vterm_screen_enable_altscreen(m_screen, 1);

    vterm_screen_reset(m_screen, 1);

    // Pre-allocate cell cache
    m_cellCache.resize((size_t)(cols * rows));

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

int TerminalEmulator::GetScrollbackRows() const {
    if (!m_screen) return 0;
    // libvterm doesn't directly expose scrollback size via screen API.
    // We'll use the screen damage/scrollback callbacks in a future iteration.
    // For now, 0 means "no scrollback rows shown beyond screen rows".
    return 0;
}

// ---------------------------------------------------------------------------
// Rendering
// ---------------------------------------------------------------------------

void TerminalEmulator::Render(ImVec2 pos, ImVec2 size, float scrollY) {
    if (!m_screen) {
        // Render placeholder
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

    // Scroll offset (clamp to valid range)
    int maxScroll = m_rows - visibleRows;
    int scrollRow = std::max(0, std::min((int)(scrollY * maxScroll + 0.5f), maxScroll));

    // Default colors (matching the app's dark theme)
    ImU32 defaultBg = IM_COL32(16, 16, 20, 255);
    ImU32 defaultFg = IM_COL32(220, 220, 220, 255);

    // Fill background
    dl->AddRectFilled(pos, ImVec2(pos.x + size.x, pos.y + size.y), defaultBg);

    // Track cursor position for rendering
    int cursorScreenRow = m_cursorRow - scrollRow;
    int cursorScreenCol = m_cursorCol;

    // Render each visible row
    for (int r = 0; r < visibleRows; r++) {
        int gridRow = scrollRow + r;
        if (gridRow < 0 || gridRow >= m_rows) continue;

        float y = pos.y + r * lineSpacing;

        // Scan row and group cells with same attributes for efficient rendering
        int c = 0;
        while (c < visibleCols) {
            const VTermScreenCell* cell = GetCell(c, gridRow);
            if (!cell) { c++; continue; }

            // Find run of cells with identical styles
            int runEnd = c + 1;
            while (runEnd < visibleCols) {
                const VTermScreenCell* next = GetCell(runEnd, gridRow);
                if (!next) break;

                // Check if styles match (same bg, fg, bold, italic, underline)
                bool same = true;
                // Compare background color
                if (next->bg.type != cell->bg.type ||
                    next->bg.rgb.red != cell->bg.rgb.red ||
                    next->bg.rgb.green != cell->bg.rgb.green ||
                    next->bg.rgb.blue != cell->bg.rgb.blue)
                    same = false;
                // Compare foreground
                if (same && (next->fg.type != cell->fg.type ||
                    next->fg.rgb.red != cell->fg.rgb.red ||
                    next->fg.rgb.green != cell->fg.rgb.green ||
                    next->fg.rgb.blue != cell->fg.rgb.blue))
                    same = false;
                // Compare attributes
                if (same && next->attrs.bold != cell->attrs.bold)
                    same = false;
                if (same && next->attrs.italic != cell->attrs.italic)
                    same = false;
                if (same && next->attrs.underline != cell->attrs.underline)
                    same = false;

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

            float x = pos.x + c * cellWidth;
            float runWidth = (runEnd - c) * cellWidth;

            // Draw background
            if (bgColor != defaultBg || (cursorScreenRow == r && cursorScreenCol >= c && cursorScreenCol < runEnd)) {
                dl->AddRectFilled(ImVec2(x, y), ImVec2(x + runWidth, y + cellHeight), bgColor);
            }

            // Draw text for this run
            if (fgColor != bgColor) {  // only draw if visible
                // Build a UTF-8 string for the run
                // We process each cell individually to handle combining chars and wide chars
                float textX = x;
                for (int ci = c; ci < runEnd; ci++) {
                    const VTermScreenCell* cc = GetCell(ci, gridRow);
                    if (!cc) break;

                    // Convert chars[0..n] to UTF-8
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
                        dl->AddText(ImVec2(textX, y), fgColor, utf8Buf);
                    }

                    // Advance position
                    textX += cc->width * cellWidth;
                    if (cc->width == 2) ci++;  // skip the padding cell for wide chars
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

            // Advance column
            c = runEnd;
        }

        // Draw cursor on this row if visible
        if (m_cursorVisible && cursorScreenRow == r && cursorScreenCol >= 0 && cursorScreenCol < m_cols) {
            float cx = pos.x + cursorScreenCol * cellWidth;
            // Draw cursor as a white block with alpha blending, or an underline
            float t = (float)(ImGui::GetTime() * 2.0);
            if (fmod(t, 2.0) < 1.0) {  // blink every 0.5s
                dl->AddRectFilled(ImVec2(cx, y), ImVec2(cx + cellWidth, y + cellHeight),
                                  IM_COL32(200, 200, 200, 160));
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

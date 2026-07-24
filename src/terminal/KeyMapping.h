#pragma once

#include <string>
#include <imgui.h>

/// Maps ImGui keyboard events to VT100/xterm escape sequences.
class KeyMapping {
public:
    /// Convert an ImGui key + modifiers to a VT escape sequence.
    /// Returns nullptr if the key should be ignored.
    static const char* ToVtSequence(ImGuiKey key, ImGuiKeyChord mods);

    /// Check if a key combo should be captured by the terminal (vs used by ImGui).
    static bool IsTerminalKey(ImGuiKey key, ImGuiKeyChord mods);

    /// Get the terminal action for Ctrl+Shift+letter (copy, paste, new tab, etc.)
    enum class Action { None, Copy, Paste, NewTab, CloseTab, Find, Clear };
    static Action GetAction(ImGuiKey key, ImGuiKeyChord mods);
};

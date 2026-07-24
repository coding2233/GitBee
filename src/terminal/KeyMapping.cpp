#include "KeyMapping.h"
#include <cstring>
#include <cstdio>

// Helper to check if Ctrl is held
static inline bool IsCtrl(ImGuiKeyChord mods) {
    return (mods & ImGuiMod_Ctrl) != 0;
}
static inline bool IsShift(ImGuiKeyChord mods) {
    return (mods & ImGuiMod_Shift) != 0;
}
static inline bool IsAlt(ImGuiKeyChord mods) {
    return (mods & ImGuiMod_Alt) != 0;
}
static inline bool IsSuper(ImGuiKeyChord mods) {
    return (mods & ImGuiMod_Super) != 0;
}

const char* KeyMapping::ToVtSequence(ImGuiKey key, ImGuiKeyChord mods) {
    // Ctrl+Shift+letter combos that are terminal actions (not sent to PTY)
    if (IsCtrl(mods) && IsShift(mods)) return nullptr;

    // Alt+letter → ESC + letter
    if (IsAlt(mods) && !IsCtrl(mods) && !IsShift(mods)) {
        if (key >= ImGuiKey_A && key <= ImGuiKey_Z) {
            static char buf[4];
            buf[0] = '\x1b';
            buf[1] = 'a' + (key - ImGuiKey_A);
            buf[2] = '\0';
            return buf;
        }
    }

    // Handle special keys
    switch (key) {
        case ImGuiKey_Enter:       return "\r";
        case ImGuiKey_Backspace:   return "\x7f";
        case ImGuiKey_Tab:         return "\t";

        // Cursor keys (with modifier variants)
        case ImGuiKey_UpArrow: {
            if (IsCtrl(mods))  return "\x1b[1;5A";  // Ctrl+Up
            if (IsShift(mods)) return "\x1b[1;2A";  // Shift+Up
            if (IsAlt(mods))   return "\x1b[1;3A";  // Alt+Up
            return "\x1b[A";
        }
        case ImGuiKey_DownArrow: {
            if (IsCtrl(mods))  return "\x1b[1;5B";
            if (IsShift(mods)) return "\x1b[1;2B";
            if (IsAlt(mods))   return "\x1b[1;3B";
            return "\x1b[B";
        }
        case ImGuiKey_RightArrow: {
            if (IsCtrl(mods))  return "\x1b[1;5C";
            if (IsShift(mods)) return "\x1b[1;2C";
            if (IsAlt(mods))   return "\x1b[1;3C";
            return "\x1b[C";
        }
        case ImGuiKey_LeftArrow: {
            if (IsCtrl(mods))  return "\x1b[1;5D";
            if (IsShift(mods)) return "\x1b[1;2D";
            if (IsAlt(mods))   return "\x1b[1;3D";
            return "\x1b[D";
        }

        case ImGuiKey_Home:        return "\x1b[H";
        case ImGuiKey_End:         return "\x1b[F";
        case ImGuiKey_PageUp:      return "\x1b[5~";
        case ImGuiKey_PageDown:    return "\x1b[6~";
        case ImGuiKey_Insert:      return "\x1b[2~";
        case ImGuiKey_Delete:      return "\x1b[3~";
        case ImGuiKey_Escape:      return "\x1b";

        // Function keys
        case ImGuiKey_F1:          return "\x1b[11~";
        case ImGuiKey_F2:          return "\x1b[12~";
        case ImGuiKey_F3:          return "\x1b[13~";
        case ImGuiKey_F4:          return "\x1b[14~";
        case ImGuiKey_F5:          return "\x1b[15~";
        case ImGuiKey_F6:          return "\x1b[17~";
        case ImGuiKey_F7:          return "\x1b[18~";
        case ImGuiKey_F8:          return "\x1b[19~";
        case ImGuiKey_F9:          return "\x1b[20~";
        case ImGuiKey_F10:         return "\x1b[21~";
        case ImGuiKey_F11:         return "\x1b[23~";
        case ImGuiKey_F12:         return "\x1b[24~";

        default: break;
    }

    // Ctrl+A-Z → 0x01-0x1a (all control characters)
    if (IsCtrl(mods) && !IsAlt(mods) && !IsShift(mods)) {
        if (key >= ImGuiKey_A && key <= ImGuiKey_Z) {
            static char buf[2];
            buf[0] = (char)(key - ImGuiKey_A + 1);
            buf[1] = '\0';
            return buf;
        }
    }

    // Ctrl+[ → ESC (0x1b)
    if (IsCtrl(mods) && key == ImGuiKey_LeftBracket)
        return "\x1b";

    // Ctrl+\ → FS (0x1c)
    if (IsCtrl(mods) && key == ImGuiKey_Backslash)
        return "\x1c";

    // Ctrl+] → GS (0x1d)
    if (IsCtrl(mods) && key == ImGuiKey_RightBracket)
        return "\x1d";

    // Ctrl+/ → Skip or delete
    if (IsCtrl(mods) && key == ImGuiKey_Slash)
        return "\x1f";

    return nullptr;
}

bool KeyMapping::IsTerminalKey(ImGuiKey key, ImGuiKeyChord mods) {
    // These combos are captured by the terminal, not passed to ImGui
    if (IsSuper(mods)) return false;  // reserved for OS

    // Ctrl+Shift+Letter → terminal actions (copy, paste, etc.)
    if (IsCtrl(mods) && IsShift(mods)) return true;

    // All regular keys + Ctrl combos (except ImGui's own shortcuts)
    return true;
}

KeyMapping::Action KeyMapping::GetAction(ImGuiKey key, ImGuiKeyChord mods) {
    if (IsCtrl(mods) && IsShift(mods)) {
        switch (key) {
            case ImGuiKey_C: return Action::Copy;
            case ImGuiKey_V: return Action::Paste;
            case ImGuiKey_N: return Action::NewTab;
            case ImGuiKey_W: return Action::CloseTab;
            case ImGuiKey_F: return Action::Find;
            case ImGuiKey_L: return Action::Clear;
            default: break;
        }
    }
    return Action::None;
}

#pragma once

#include <cstdio>
#include <cstdarg>
#include <ctime>
#include <cstring>
#include <string>
#include <sstream>
#include <mutex>

// -----------------------------------------------------------------------
// Simple single-header debug logger for GitBee
// Writes timestamped messages to stderr.
// Define GITBEE_DEBUG to enable verbose debug logging.
// -----------------------------------------------------------------------

namespace dbg {

// NOTE: enum values use LvlXxx prefix to avoid conflicts with Windows
// macros ERROR (wingdi.h) and DEBUG (often defined as 0).
enum Level {
    LvlDebug = 0,
    LvlInfo  = 1,
    LvlWarn  = 2,
    LvlError = 3
};

inline const char* LevelStr(Level lv) {
    switch (lv) {
        case LvlDebug: return "DEBUG";
        case LvlInfo:  return "INFO";
        case LvlWarn:  return "WARN";
        case LvlError: return "ERROR";
        default:       return "?";
    }
}

inline const char* LevelColor(Level lv) {
    switch (lv) {
        case LvlDebug: return "\033[90m";    // grey
        case LvlInfo:  return "\033[36m";    // cyan
        case LvlWarn:  return "\033[33m";    // yellow
        case LvlError: return "\033[31m";    // red
        default:       return "\033[0m";
    }
}

#ifndef _WIN32
#  define DBG_COLOR_RESET "\033[0m"
#else
#  define DBG_COLOR_RESET ""
#  undef LevelColor
#  define LevelColor(lv) ""
#endif

// Thread-safe logging to stderr
inline void Log(Level lv, const char* file, int line, const char* fmt, ...) {
    static std::mutex s_mutex;
    std::lock_guard<std::mutex> lock(s_mutex);

    // Timestamp
    std::time_t now = std::time(nullptr);
    std::tm tm;
#ifdef _WIN32
    localtime_s(&tm, &now);
#else
    localtime_r(&now, &tm);
#endif
    char ts[32];
    std::strftime(ts, sizeof(ts), "%H:%M:%S", &tm);

    // Format message
    va_list args;
    va_start(args, fmt);
    char buf[4096];
    vsnprintf(buf, sizeof(buf), fmt, args);
    va_end(args);

    // Short filename only
    const char* shortFile = file;
    if (const char* slash = std::strrchr(file, '/'))
        shortFile = slash + 1;
    else if (const char* back = std::strrchr(file, '\\'))
        shortFile = back + 1;

    fprintf(stderr, "%s%s [%s] %s:%d  %s%s\n",
            LevelColor(lv), ts, LevelStr(lv), shortFile, line, buf, DBG_COLOR_RESET);
    fflush(stderr);
}

} // namespace dbg

// -----------------------------------------------------------------------
// Convenience macros — always emit ERROR/WARN, conditionally emit INFO/DEBUG
// -----------------------------------------------------------------------

#define LOG_ERROR(fmt, ...)   dbg::Log(dbg::LvlError, __FILE__, __LINE__, fmt, ##__VA_ARGS__)
#define LOG_WARN(fmt, ...)    dbg::Log(dbg::LvlWarn,  __FILE__, __LINE__, fmt, ##__VA_ARGS__)

#ifdef GITBEE_DEBUG
#  define LOG_INFO(fmt, ...)  dbg::Log(dbg::LvlInfo,  __FILE__, __LINE__, fmt, ##__VA_ARGS__)
#  define LOG_DEBUG(fmt, ...) dbg::Log(dbg::LvlDebug, __FILE__, __LINE__, fmt, ##__VA_ARGS__)
#else
#  define LOG_INFO(fmt, ...)  do {} while(0)
#  define LOG_DEBUG(fmt, ...) do {} while(0)
#endif

// Shorthand for logging a GitResult
#define LOG_GIT_RESULT(level, repoPath, args, result)                      \
    do {                                                                   \
        std::ostringstream _dbg_ss;                                        \
        _dbg_ss << "git";                                                  \
        for (auto& _a : args) _dbg_ss << " " << _a;                        \
        if ((result).ok) {                                                  \
            LOG_DEBUG("[OK] %s  (in %s)", _dbg_ss.str().c_str(),           \
                      (repoPath).empty() ? "<global>" : (repoPath).c_str());\
        } else {                                                            \
            LOG_##level("[FAIL] %s  (in %s)  stderr: %s",                  \
                        _dbg_ss.str().c_str(),                              \
                        (repoPath).empty() ? "<global>" : (repoPath).c_str(),\
                        (result).err.c_str());                              \
        }                                                                   \
    } while(0)

// Shorthand for logging a catch block
#define LOG_EXCEPTION(where)                                               \
    do {                                                                   \
        try { throw; } catch (const std::exception& _e) {                  \
            LOG_ERROR("EXCEPTION in %s: %s", where, _e.what());            \
        } catch (...) {                                                     \
            LOG_ERROR("EXCEPTION in %s: (unknown)", where);                 \
        }                                                                   \
    } while(0)

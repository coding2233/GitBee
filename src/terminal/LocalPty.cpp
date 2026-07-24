#include "LocalPty.h"
#include "../dbg_log.h"
#include <cstring>
#include <cstdlib>
#include <algorithm>

// Platform-specific includes
#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#else
#include <unistd.h>
#include <fcntl.h>
#include <sys/ioctl.h>
#include <sys/wait.h>
#include <signal.h>
#include <termios.h>
#include <poll.h>
#endif

LocalPty::LocalPty() {
    m_ringBuf = new char[RING_SIZE];
    memset(m_ringBuf, 0, RING_SIZE);
}

LocalPty::~LocalPty() {
    Close();
    delete[] m_ringBuf;
}

bool LocalPty::Start(const PtyConfig& cfg) {
    m_config = cfg;
    if (!OpenPty(cfg.cols, cfg.rows))
        return false;
    return true;
}

void LocalPty::Close() {
    m_running = false;

    if (m_pumpThread.joinable()) {
        m_ringCv.notify_all();
        m_pumpThread.join();
    }

    ClosePty();
}

int LocalPty::Write(const char* data, size_t len) {
    if (m_masterFd < 0) return -1;

#ifdef _WIN32
    DWORD written;
    if (!WriteFile((HANDLE)m_masterFd, data, (DWORD)len, &written, NULL))
        return -1;
    return (int)written;
#else
    ssize_t n = ::write(m_masterFd, data, len);
    return (int)n;
#endif
}

int LocalPty::Read(char* buf, size_t bufsize) {
    std::lock_guard<std::mutex> lock(m_ringMutex);

    if (m_ringHead == m_ringTail)
        return 0;  // no data available

    size_t available;
    if (m_ringHead > m_ringTail) {
        available = m_ringHead - m_ringTail;
    } else {
        available = (RING_SIZE - m_ringTail) + m_ringHead;
    }

    size_t toRead = std::min(bufsize, available);

    // Read from tail to end of ring
    size_t firstChunk = std::min(toRead, RING_SIZE - m_ringTail);
    memcpy(buf, m_ringBuf + m_ringTail, firstChunk);

    if (toRead > firstChunk) {
        // Wrap around
        memcpy(buf + firstChunk, m_ringBuf, toRead - firstChunk);
    }

    m_ringTail = (m_ringTail + toRead) % RING_SIZE;
    return (int)toRead;
}

bool LocalPty::Resize(int cols, int rows) {
    if (!IsOpen()) return false;

#ifdef _WIN32
    // ResizePseudoConsole on Windows
    // COORD size = {(SHORT)cols, (SHORT)rows};
    // ResizePseudoConsole(m_hpc, size);
    // For now, not implemented. Always succeed for MVP.
    return true;
#else
    struct winsize ws;
    memset(&ws, 0, sizeof(ws));
    ws.ws_col = (unsigned short)cols;
    ws.ws_row = (unsigned short)rows;
    ws.ws_xpixel = 0;
    ws.ws_ypixel = 0;
    return ioctl(m_masterFd, TIOCSWINSZ, &ws) == 0;
#endif
}

bool LocalPty::IsOpen() const {
    return m_masterFd >= 0;
}

// ---------------------------------------------------------------------------
// Private helpers
// ---------------------------------------------------------------------------

#ifdef _WIN32

bool LocalPty::OpenPty(int cols, int rows) {
    LOG_WARN("LocalPty: ConPTY not yet implemented on Windows");
    return false;
}

void LocalPty::ClosePty() {
    // TODO: cleanup ConPTY handles
    m_masterFd = -1;
}

void LocalPty::PumpOutput() {
    // TODO: read from ConPTY output pipe
}

#else
// Linux / macOS

bool LocalPty::OpenPty(int cols, int rows) {
    // Open pseudo-terminal
    m_masterFd = posix_openpt(O_RDWR | O_NOCTTY);
    if (m_masterFd < 0) {
        LOG_ERROR("posix_openpt failed: %s", strerror(errno));
        return false;
    }

    // Grant access to slave
    if (grantpt(m_masterFd) < 0) {
        LOG_ERROR("grantpt failed: %s", strerror(errno));
        close(m_masterFd);
        m_masterFd = -1;
        return false;
    }

    // Unlock slave
    if (unlockpt(m_masterFd) < 0) {
        LOG_ERROR("unlockpt failed: %s", strerror(errno));
        close(m_masterFd);
        m_masterFd = -1;
        return false;
    }

    // Get slave name
    const char* slaveName = ptsname(m_masterFd);
    if (!slaveName) {
        LOG_ERROR("ptsname failed: %s", strerror(errno));
        close(m_masterFd);
        m_masterFd = -1;
        return false;
    }

    // Fork
    m_pid = fork();
    if (m_pid < 0) {
        LOG_ERROR("fork failed: %s", strerror(errno));
        close(m_masterFd);
        m_masterFd = -1;
        return false;
    }

    if (m_pid == 0) {
        // ---- Child process ----
        // Create a new session
        setsid();

        // Open slave PTY as controlling terminal
        int slaveFd = open(slaveName, O_RDWR);
        if (slaveFd < 0) {
            _exit(1);
        }

        // Set up stdin/stdout/stderr
        dup2(slaveFd, STDIN_FILENO);
        dup2(slaveFd, STDOUT_FILENO);
        dup2(slaveFd, STDERR_FILENO);

        if (slaveFd > STDERR_FILENO)
            close(slaveFd);

        // Set TERM environment variable
        setenv("TERM", "xterm-256color", 1);
        setenv("COLORTERM", "truecolor", 1);

        // Change to working directory if specified
        if (!m_config.workingDir.empty()) {
            chdir(m_config.workingDir.c_str());
        }

        // Determine shell
        const char* shell = nullptr;
        if (!m_config.shellCommand.empty()) {
            shell = m_config.shellCommand.c_str();
        } else {
            shell = getenv("SHELL");
            if (!shell) shell = "/bin/bash";
        }

        // Execute shell
        execlp(shell, shell, "-i", nullptr);

        // If execlp fails, try /bin/sh
        execl("/bin/sh", "sh", "-i", nullptr);
        _exit(1);
    }

    // ---- Parent process ----
    // Set master fd to non-blocking
    int flags = fcntl(m_masterFd, F_GETFL, 0);
    fcntl(m_masterFd, F_SETFL, flags | O_NONBLOCK);

    // Set initial window size
    Resize(cols, rows);

    // Start the output pump thread
    m_running = true;
    m_pumpThread = std::thread(&LocalPty::PumpOutput, this);

    LOG_INFO("LocalPty opened (pid=%d, fd=%d, shell=%s)", m_pid, m_masterFd, getenv("SHELL"));
    return true;
}

void LocalPty::ClosePty() {
    if (m_masterFd >= 0) {
        close(m_masterFd);
        m_masterFd = -1;
    }

    if (m_pid > 0) {
        // Try to gracefully terminate
        kill(m_pid, SIGHUP);
        int status;
        waitpid(m_pid, &status, WNOHANG);
        m_pid = -1;
    }
}

void LocalPty::PumpOutput() {
    LOG_DEBUG("LocalPty pump thread started");
    struct pollfd pfd;
    pfd.fd = m_masterFd;
    pfd.events = POLLIN;

    char tempBuf[65536];

    while (m_running) {
        pfd.revents = 0;
        int ret = poll(&pfd, 1, 100);  // 100ms timeout

        if (!m_running) break;

        if (ret < 0) {
            if (errno == EINTR) continue;
            LOG_ERROR("LocalPty poll error: %s", strerror(errno));
            break;
        }

        if (ret == 0) continue;  // timeout

        if (pfd.revents & POLLIN) {
            ssize_t n = read(m_masterFd, tempBuf, sizeof(tempBuf));
            if (n > 0) {
                // Write into ring buffer
                std::lock_guard<std::mutex> lock(m_ringMutex);
                size_t available = 0;

                // Calculate available space
                if (m_ringHead >= m_ringTail) {
                    available = (RING_SIZE - m_ringHead) + m_ringTail - 1;
                } else {
                    available = m_ringTail - m_ringHead - 1;
                }

                size_t toWrite = std::min((size_t)n, available);

                size_t firstChunk = std::min(toWrite, RING_SIZE - m_ringHead);
                memcpy(m_ringBuf + m_ringHead, tempBuf, firstChunk);

                if (toWrite > firstChunk) {
                    memcpy(m_ringBuf, tempBuf + firstChunk, toWrite - firstChunk);
                }

                m_ringHead = (m_ringHead + toWrite) % RING_SIZE;
                m_ringCv.notify_one();
            } else if (n == 0) {
                // EOF - child process exited
                LOG_INFO("LocalPty child process exited");
                break;
            } else {
                if (errno != EAGAIN && errno != EINTR) {
                    LOG_ERROR("LocalPty read error: %s", strerror(errno));
                    break;
                }
            }
        }

        if (pfd.revents & (POLLHUP | POLLERR)) {
            LOG_INFO("LocalPty hangup/error");
            break;
        }
    }

    LOG_DEBUG("LocalPty pump thread exiting");
    m_running = false;
    if (OnClosed) OnClosed();
}

#endif // _WIN32

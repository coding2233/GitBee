#include "SshPty.h"
#include "../dbg_log.h"
#include <cstring>
#include <cstdlib>
#include <algorithm>

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

SshPty::SshPty() {
    m_ringBuf = new char[RING_SIZE];
    memset(m_ringBuf, 0, RING_SIZE);
}

SshPty::~SshPty() {
    Close();
    delete[] m_ringBuf;
}

bool SshPty::Start(const PtyConfig& cfg) {
    // Build the SSH command from config
    std::string sshCmd = "ssh";

    if (cfg.port != 22)
        sshCmd += " -p " + std::to_string(cfg.port);

    if (!cfg.privateKeyPath.empty())
        sshCmd += " -i \"" + cfg.privateKeyPath + "\"";

    // Force PTY allocation
    sshCmd += " -t";

    // User@Host
    if (!cfg.username.empty())
        sshCmd += " " + cfg.username + "@" + cfg.host;
    else
        sshCmd += " " + cfg.host;

    LOG_INFO("SshPty starting: %s", sshCmd.c_str());
    return OpenPty(sshCmd, cfg.cols, cfg.rows);
}

void SshPty::Close() {
    m_running = false;
    if (m_pumpThread.joinable()) {
        m_ringCv.notify_all();
        m_pumpThread.join();
    }
    ClosePty();
}

int SshPty::Write(const char* data, size_t len) {
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

int SshPty::Read(char* buf, size_t bufsize) {
    std::lock_guard<std::mutex> lock(m_ringMutex);
    if (m_ringHead == m_ringTail)
        return 0;

    size_t available;
    if (m_ringHead > m_ringTail)
        available = m_ringHead - m_ringTail;
    else
        available = (RING_SIZE - m_ringTail) + m_ringHead;

    size_t toRead = std::min(bufsize, available);
    size_t firstChunk = std::min(toRead, RING_SIZE - m_ringTail);
    memcpy(buf, m_ringBuf + m_ringTail, firstChunk);
    if (toRead > firstChunk)
        memcpy(buf + firstChunk, m_ringBuf, toRead - firstChunk);

    m_ringTail = (m_ringTail + toRead) % RING_SIZE;
    return (int)toRead;
}

bool SshPty::Resize(int cols, int rows) {
    if (!IsOpen()) return false;

#ifdef _WIN32
    // TODO: ConPTY resize
    return true;
#else
    struct winsize ws;
    memset(&ws, 0, sizeof(ws));
    ws.ws_col = (unsigned short)cols;
    ws.ws_row = (unsigned short)rows;
    return ioctl(m_masterFd, TIOCSWINSZ, &ws) == 0;
#endif
}

bool SshPty::IsOpen() const {
    return m_masterFd >= 0;
}

// ---------------------------------------------------------------------------
// Private helpers
// ---------------------------------------------------------------------------

#ifndef _WIN32

bool SshPty::OpenPty(const std::string& sshCommand, int cols, int rows) {
    // Open pseudo-terminal
    m_masterFd = posix_openpt(O_RDWR | O_NOCTTY);
    if (m_masterFd < 0) {
        LOG_ERROR("SshPty posix_openpt failed: %s", strerror(errno));
        return false;
    }

    if (grantpt(m_masterFd) < 0) {
        LOG_ERROR("SshPty grantpt failed: %s", strerror(errno));
        close(m_masterFd); m_masterFd = -1;
        return false;
    }

    if (unlockpt(m_masterFd) < 0) {
        LOG_ERROR("SshPty unlockpt failed: %s", strerror(errno));
        close(m_masterFd); m_masterFd = -1;
        return false;
    }

    const char* slaveName = ptsname(m_masterFd);
    if (!slaveName) {
        LOG_ERROR("SshPty ptsname failed: %s", strerror(errno));
        close(m_masterFd); m_masterFd = -1;
        return false;
    }

    // Fork
    m_pid = fork();
    if (m_pid < 0) {
        LOG_ERROR("SshPty fork failed: %s", strerror(errno));
        close(m_masterFd); m_masterFd = -1;
        return false;
    }

    if (m_pid == 0) {
        // ---- Child process ----
        setsid();

        int slaveFd = open(slaveName, O_RDWR);
        if (slaveFd < 0) _exit(1);

        dup2(slaveFd, STDIN_FILENO);
        dup2(slaveFd, STDOUT_FILENO);
        dup2(slaveFd, STDERR_FILENO);
        if (slaveFd > STDERR_FILENO) close(slaveFd);

        setenv("TERM", "xterm-256color", 1);
        setenv("COLORTERM", "truecolor", 1);

        // Parse the SSH command string into arguments for execlp
        // The command is built as: ssh -p <port> -i <key> -t user@host
        // We need to shell it or parse it
        // Easiest: use sh -c "ssh ..."
        execl("/bin/sh", "sh", "-c", sshCommand.c_str(), nullptr);
        _exit(1);
    }

    // ---- Parent process ----
    int flags = fcntl(m_masterFd, F_GETFL, 0);
    fcntl(m_masterFd, F_SETFL, flags | O_NONBLOCK);

    Resize(cols, rows);

    // Start pump thread
    m_running = true;
    m_pumpThread = std::thread(&SshPty::PumpOutput, this);

    LOG_INFO("SshPty opened (pid=%d, fd=%d): %s", m_pid, m_masterFd, sshCommand.c_str());
    return true;
}

void SshPty::ClosePty() {
    if (m_masterFd >= 0) {
        close(m_masterFd);
        m_masterFd = -1;
    }
    if (m_pid > 0) {
        kill(m_pid, SIGHUP);
        int status;
        waitpid(m_pid, &status, WNOHANG);
        m_pid = -1;
    }
}

void SshPty::PumpOutput() {
    LOG_DEBUG("SshPty pump thread started");
    struct pollfd pfd;
    pfd.fd = m_masterFd;
    pfd.events = POLLIN;

    char tempBuf[65536];

    while (m_running) {
        pfd.revents = 0;
        int ret = poll(&pfd, 1, 100);

        if (!m_running) break;

        if (ret < 0) {
            if (errno == EINTR) continue;
            break;
        }
        if (ret == 0) continue;

        if (pfd.revents & POLLIN) {
            ssize_t n = read(m_masterFd, tempBuf, sizeof(tempBuf));
            if (n > 0) {
                std::lock_guard<std::mutex> lock(m_ringMutex);
                size_t available;
                if (m_ringHead >= m_ringTail)
                    available = (RING_SIZE - m_ringHead) + m_ringTail - 1;
                else
                    available = m_ringTail - m_ringHead - 1;

                size_t toWrite = std::min((size_t)n, available);
                size_t firstChunk = std::min(toWrite, RING_SIZE - m_ringHead);
                memcpy(m_ringBuf + m_ringHead, tempBuf, firstChunk);
                if (toWrite > firstChunk)
                    memcpy(m_ringBuf, tempBuf + firstChunk, toWrite - firstChunk);

                m_ringHead = (m_ringHead + toWrite) % RING_SIZE;
                m_ringCv.notify_one();
            } else if (n == 0) {
                LOG_INFO("SshPty child process exited");
                break;
            } else {
                if (errno != EAGAIN && errno != EINTR) break;
            }
        }

        if (pfd.revents & (POLLHUP | POLLERR)) {
            LOG_INFO("SshPty hangup");
            break;
        }
    }

    LOG_DEBUG("SshPty pump thread exiting");
    m_running = false;
    if (OnClosed) OnClosed();
}

#else // _WIN32
bool SshPty::OpenPty(const std::string& sshCommand, int cols, int rows) {
    LOG_WARN("SshPty not yet implemented on Windows");
    return false;
}
void SshPty::ClosePty() { m_masterFd = -1; }
void SshPty::PumpOutput() {}
#endif

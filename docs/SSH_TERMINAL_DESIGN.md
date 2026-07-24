# SSH Terminal Tab — Technical Design Document

> Version: 1.0  
> Status: Implemented (Phase 1-2)  
> Project: GitBee  

---

## 1. Overview

为 GitBee 新增终端（Terminal）Tab，支持：

- **本地终端** — 在当前机器上打开 shell（bash / zsh / pwsh / cmd）
- **远程 SSH** — 配置并连接到远程服务器
- **凭据管理** — 本地存储连接配置（账号、密码、密钥路径）

参考产品：**[Terminus](https://github.com/Eugeny/tabby)**（多标签终端管理器，支持 SSH、串口等连接类型）。

---

## 2. Architecture Overview

```
┌───────────────────────────────────────────────────────────────┐
│ GitBeeApp                                                     │
│  ├── Home Tab (现有)                                           │
│  ├── RepoView Tab (现有)     ← 每个仓库一个 Tab                │
│  ├── Global Config Tab (现有)                                  │
│  └── TerminalTab (新增)      ← 每个终端会话一个 Tab             │
│       │                                                       │
│       ├── TerminalEmulator     ← libvterm 封装 + ImGui 渲染    │
│       ├── PtyAdapter           ← 统一 PTY 接口                 │
│       │    ├── LocalPty        ← forkpty / ConPTY              │
│       │    └── SshPty          ← libssh2 channel               │
│       ├── SshSession           ← SSH 连接生命周期               │
│       └── ConnectionStore      ← 连接配置持久化 (JSON)          │
│                                                                │
│  └── TerminalManager (新增)    ← 管理所有终端 Tab + 连接列表   │
└───────────────────────────────────────────────────────────────┘
```

### 2.1 数据流

```
          ┌────── 输入 ──────
          │
     ImGui Keyboard Events
          │
          ▼
    TerminalTab::OnInput(char)
          │
          ▼
    PtyAdapter::write(data)
          │
    ┌─────┴─────┐
    │ LocalPty   │  → write() → PTY fd → shell process
    │   or       │
    │ SshPty     │  → write() → libssh2_channel_write()
    └─────┬─────┘
          │
          │  ┌────── 输出 ──────
          ▼
    PtyAdapter::read()  ← non-blocking poll
          │
          ▼
    libvterm: vterm_input_write() → parse ANSI/VT100
          │
          ▼
    VTermScreen: cell grid (80×24 ~ 200×60)
          │
          ▼
    TerminalEmulator::Render()
      → ImDrawList::AddRectFilled (cell backgrounds)
      → ImDrawList::AddText (cell characters)
```

---

## 3. Component Design

### 3.1 PTY Abstraction Layer

#### 3.1.1 Interface

```cpp
// src/terminal/PtyAdapter.h

enum class PtyType { Local, Ssh };

struct PtyConfig {
    int cols = 80;
    int rows = 24;
    // For local:
    std::string shellCommand;   // empty = $SHELL or cmd.exe
    std::string workingDir;     // initial cwd
    // For SSH:
    std::string host;
    int port = 22;
    std::string username;
    // Auth (one of):
    std::string password;
    std::string privateKeyPath;
    std::string passphrase;     // for encrypted key
    bool useAgent = false;
};

class PtyAdapter {
public:
    virtual ~PtyAdapter() = default;

    virtual bool Start(const PtyConfig& cfg) = 0;
    virtual void Close() = 0;

    // Returns bytes written; -1 on error
    virtual int Write(const char* data, size_t len) = 0;

    // Returns bytes read; 0 = no data, -1 = error/closed
    virtual int Read(char* buf, size_t bufsize) = 0;

    // Resize terminal dimensions
    virtual bool Resize(int cols, int rows) = 0;

    virtual bool IsOpen() const = 0;
    virtual PtyType Type() const = 0;
};
```

#### 3.1.2 LocalPty Implementation

```
平台差异：

  Linux / macOS:
    pid_t pid = forkpty(&master_fd, nullptr, nullptr, &winsize);
    if (pid == 0) {
        // child: exec $SHELL
        execlp(shell, shell, nullptr);
    }
    // parent: master_fd 用于 read/write

  Windows (Win10 1809+):
    CreatePseudoConsole(cmdSize, inputReadSide, outputWriteSide, ...);
    CreateProcess(..., EXTENDED_STARTUPINFO_PRESENT, ...);
    // 通过管道 read/write
```

关键技术点：
- **非阻塞 I/O**: `fcntl(master_fd, F_SETFL, O_NONBLOCK)` + `poll()` / Windows `PeekNamedPipe()`
- **resize**: `ioctl(master_fd, TIOCSWINSZ, &winsize)` / `ResizePseudoConsole()`
- **子进程退出检测**: `waitpid(pid, &status, WNOHANG)` / `WaitForSingleObject(pi.hProcess, 0)`

#### 3.1.3 SshPty Implementation

基于 **libssh2**（C 库，BSD 授权，curl/git 也使用它）：

```cpp
// 伪代码流程
bool SshPty::Start(const PtyConfig& cfg) {
    // 1. 建立 TCP socket
    sock = socket(AF_INET, SOCK_STREAM, 0);
    connect(sock, cfg.host, cfg.port);

    // 2. 创建 SSH session
    session = libssh2_session_init();
    libssh2_session_handshake(session, sock);

    // 3. 认证（按优先级尝试）
    if (cfg.useAgent)
        libssh2_agent_connect(agent);
        libssh2_agent_list_identities(agent);
        libssh2_agent_userauth(agent, cfg.username, identity);
    else if (!cfg.privateKeyPath.empty())
        libssh2_userauth_publickey_fromfile(session, cfg.username,
            nullptr, cfg.privateKeyPath, cfg.passphrase);
    else if (!cfg.password.empty())
        libssh2_userauth_password(session, cfg.username, cfg.password);
    else
        libssh2_userauth_keyboard_interactive(session, cfg.username, callback);

    // 4. 打开 channel（PTY 模式）
    channel = libssh2_channel_open_session(session);
    libssh2_channel_request_pty(channel, "xterm-256color");
    libssh2_channel_shell(channel);  // 或 exec($SHELL)
}
```

I/O 模型：
```cpp
int SshPty::Read(char* buf, size_t bufsize) {
    ssize_t n = libssh2_channel_read(channel, buf, bufsize);
    if (n == LIBSSH2_ERROR_EAGAIN) return 0;  // 无数据
    if (n < 0) return -1;                     // 错误
    return (int)n;
}
int SshPty::Write(const char* data, size_t len) {
    return libssh2_channel_write(channel, data, len);
}
bool SshPty::Resize(int cols, int rows) {
    return libssh2_channel_request_pty_size(channel, cols, rows) == 0;
}
```

---

### 3.2 Terminal Emulator (libvterm)

#### 3.2.1 Why libvterm

| 候选           | 结论        | 原因                                              |
| -------------- | ----------- | ------------------------------------------------- |
| libvterm       | ✅ 推荐     | Neovim 在用，成熟稳定，纯 C，支持 256-col / true-color |
| libtsm         | ⚠️ 备选    | 更轻量但维护不活跃                                 |
| 手写简易 ANSI  | ❌         | vim/tmux/彩色输出等复杂场景不可行                   |
| ImTerm         | ⚠️ 参考    | Dear ImGui 专用终端组件，可参考其渲染方式          |

#### 3.2.2 Encapsulation

```cpp
// src/terminal/TerminalEmulator.h

class TerminalEmulator {
public:
    TerminalEmulator(int cols = 80, int rows = 24);
    ~TerminalEmulator();

    // Input from PTY → parse
    void ProcessInput(const char* data, size_t len);

    // Input from user → send to PTY (called by PtyAdapter::Write)
    void SendKey(const char* bytes, size_t len, VTermModifier mod);

    // Resize
    void Resize(int cols, int rows);

    // Render using ImGui
    void Render(ImVec2 screenPos, ImVec2 screenSize,
                float cellWidth, float cellHeight,
                float scrollOffset, int& cursorCol, int& cursorRow);

    // Access cell grid
    int GetCols() const;
    int GetRows() const;
    const VTermScreenCell& GetCell(int col, int row) const;

    // Callbacks
    std::function<void(const char* data, size_t len)> OnOutput;    // → PtyAdapter::Write
    std::function<void(const char* title)> OnTitleChange;

private:
    VTerm* m_vterm;
    VTermScreen* m_vtermScreen;
    int m_cols, m_rows;
    VTermPos m_cursorPos;

    static void OutputCallback(const char* s, size_t n, void* user);
    static int DamageCallback(VTermRect rect, void* user);
    static int MoveCursorCallback(VTermPos pos, VTermPos old, int visible, void* user);
    static int SetTermPropCallback(VTermProp prop, VTermValue* val, void* user);
    static int BellCallback(void* user);
};
```

#### 3.2.3 ImGui Rendering Strategy

终端渲染的核心挑战：一个 200×60 的 cell grid = 12,000 个 cell，每个 cell 都需要背景色块 + 文字。必须高效批量渲染。

**策略：逐行渲染 + 合并同色背景**

```
算法 (per frame):
  for each row in visible range (scrollOffset → scrollOffset + visibleRows):
    1. 扫描该行所有 cell，按 (bg_color, fg_color, font_attrs) 分组
    2. 对每组连续 cell：
       - AddRectFilled(组起始位置, 组结束位置, bg_color)
       - 构建文字字符串，AddText(组起始位置, fg_color, text)
    3. 如果有光标 + 所在行，渲染闪烁光标
```

伪代码：
```cpp
void TerminalEmulator::Render(...) {
    auto* drawList = ImGui::GetWindowDrawList();

    float cellW = ImGui::GetFont()->CalcTextSize("M", ...).x;  // monospace
    float cellH = ImGui::GetTextLineHeight();

    int startRow = (int)scrollOffset;
    int endRow = std::min(startRow + (int)(screenSize.y / cellH), m_rows);

    for (int row = startRow; row < endRow; row++) {
        float y = screenPos.y + (row - startRow) * cellH;

        int col = 0;
        while (col < m_cols) {
            const VTermScreenCell& cell = GetCell(col, row);

            // 收集连续同属性的 cell
            int runEnd = col;
            while (runEnd < m_cols && CellEqualStyle(GetCell(runEnd, row), cell))
                runEnd++;

            int width = runEnd - col;
            float x = screenPos.x + col * cellW;

            // 绘制背景
            ImU32 bg = PackColor(cell.bg);
            drawList->AddRectFilled(
                ImVec2(x, y),
                ImVec2(x + width * cellW, y + cellH),
                bg
            );

            // 构建并绘制文字
            std::string text;
            for (int i = col; i < runEnd; i++) {
                const auto& c = GetCell(i, row);
                char ch[8] = {};
                // 处理宽字符 (CJK)
                int chLen = vterm_screen_cell_to_utf8(c, ch, sizeof(ch));
                text.append(ch, chLen);
                if (c.width == 2) i++;  // skip the padding cell
            }
            ImU32 fg = PackColor(cell.fg);
            drawList->AddText(ImVec2(x, y), fg, text.c_str());

            // 应用字体属性（粗体/斜体/下划线）
            // ...

            col = runEnd;
        }
    }

    // 渲染光标
    if (cursorVisible) {
        float cx = screenPos.x + m_cursorPos.col * cellW;
        float cy = screenPos.y + (m_cursorPos.row - startRow) * cellH;
        drawList->AddRect(ImVec2(cx, cy),
                          ImVec2(cx + cellW, cy + cellH),
                          IM_COL32_WHITE);
    }
}
```

性能优化：
- 只渲染可见行（滚动视图裁剪）
- 只 `damage` 变化的 cell（libvterm 的 `DamageCallback` 提供脏矩形）
- 使用 `ImDrawList` 批量绘制而非多次 `ImGui::Text`（后者有布局开销）
- 可选的：渲染到纹理（FBO/RenderTarget），只在内容变化时更新

---

### 3.3 SSH Session Management

#### 3.3.1 Connection Lifecycle

```
                    ┌─────────┐
    用户点击连接 ───→│ Resolving│
                    └────┬────┘
                         │ host resolved
                    ┌────▼────┐
                    │Connecting│──→ TCP connect
                    └────┬────┘
                         │ TCP connected
                    ┌────▼────┐
                    │Handshake │──→ SSH key exchange
                    └────┬────┘
                         │
                    ┌────▼────┐
                    │   Auth   │──→ password / key / agent / interactive
                    └────┬────┘
                         │ authenticated
                    ┌────▼────┐
                    │Connected │──→ PTY open, shell running
                    └────┬────┘
                         │
                    ┌────▼────┐
                    │  Closed  │
                    └─────────┘
```

```cpp
enum class SshState {
    Idle,
    Resolving,
    Connecting,
    Handshaking,
    Authenticating,
    Connected,
    Closed,
    Error
};

class SshSession {
public:
    void Connect(const SshConnection& conn);
    void Disconnect();
    SshState GetState() const;

    // Events (called from worker thread)
    std::function<void(SshState state)> OnStateChange;
    std::function<void(const std::string& error)> OnError;
    std::function<void(const std::string& banner)> OnBanner;

    // Stream
    LIBSSH2_SESSION* GetRawSession() const;
    LIBSSH2_CHANNEL* GetRawChannel() const;

private:
    LIBSSH2_SESSION* m_session = nullptr;
    LIBSSH2_CHANNEL* m_channel = nullptr;
    SshState m_state = SshState::Idle;

    // Non-blocking I/O via poll/select
    void Poll();
};
```

#### 3.3.2 Authentication Flow

```
1. Try agent (ssh-agent / Pageant)         ← no user input needed
2. Try publickey with key files            ← may need passphrase (cached)
3. Try keyboard-interactive                ← may need password
4. Try password                            ← stored password
5. If all fail → prompt user via UI dialog
```

libssh2 认证 API 返回 `LIBSSH2_ERROR_AUTHENTICATION_FAILED` 时自动尝试下一个方法。

---

### 3.4 Credential Storage

#### 3.4.1 Data Model

```cpp
// src/terminal/ConnectionStore.h

struct SshConnection {
    std::string id;            // UUID
    std::string name;          // display name, e.g. "Production Server"
    std::string host;
    int port = 22;
    std::string username;

    enum AuthMethod {
        Agent,
        PublicKey,
        Password,
        KeyboardInteractive
    };
    AuthMethod authMethod = PublicKey;

    std::string privateKeyPath;   // e.g. ~/.ssh/id_ed25519
    std::string encryptedPassphrase;  // AES-256-GCM encrypted
    std::string encryptedPassword;    // AES-256-GCM encrypted (for password auth)

    std::string group;           // optional grouping
    std::string jumpHost;        // optional SSH proxy
    int order = 0;               // sort order

    std::string startupCommand;  // optional command to run on connect
    bool keepAlive = true;
    int keepAliveInterval = 60;  // seconds

    // Serialization
    nlohmann::json ToJson() const;
    static SshConnection FromJson(const nlohmann::json& j);
};
```

#### 3.4.2 Storage Format

文件位置：`~/.config/GitBee/connections.json`

```json
{
  "version": 1,
  "connections": [
    {
      "id": "a1b2c3d4-...",
      "name": "My VPS",
      "host": "192.168.1.100",
      "port": 22,
      "username": "root",
      "authMethod": "publickey",
      "privateKeyPath": "~/.ssh/id_ed25519",
      "encryptedPassphrase": "base64...",
      "group": "Production",
      "order": 0
    },
    {
      "id": "...",
      "name": "Local Dev Container",
      "host": "localhost",
      "port": 2222,
      "username": "dev",
      "authMethod": "password",
      "encryptedPassword": "base64...",
      "group": "Development"
    }
  ]
}
```

#### 3.4.3 Encryption for Sensitive Fields

```
方案：AES-256-GCM + 机器级密钥派生

Key derivation:
  key = SHA256(machine_id + salt)
  machine_id = /etc/machine-id (Linux) or MachineGuid (Windows registry)

Encrypt:
  iv = random 12 bytes
  ciphertext, tag = AES-256-GCM(plaintext, key, iv)
  stored = base64(iv + ciphertext + tag)

Decrypt:
  reverse above

Crypto library: OpenSSL / mbedtls / tiny-AES-c (embedded)
```

安全等级说明：
- ⚠️ 这不是端到端加密 — key 可以从文件系统读取
- 作用是防止**明文泄露**（例如不小心把配置文件提交到 Git）
- 未来可升级到系统 Keychain（Windows Credential Manager / libsecret）

---

### 3.5 TerminalTab — Main Tab Controller

```cpp
// src/terminal/TerminalTab.h

class TerminalTab {
public:
    enum class TabType { LocalShell, RemoteSsh };

    TerminalTab(TabType type, const SshConnection* conn = nullptr);
    ~TerminalTab();

    void Render();
    bool IsOpen() const;
    const std::string& GetTitle() const;
    TabType GetType() const;

private:
    TabType m_type;
    std::string m_title;

    // Core components
    std::unique_ptr<PtyAdapter> m_pty;
    std::unique_ptr<TerminalEmulator> m_terminal;

    // SSH session (only for RemoteSsh)
    std::unique_ptr<SshSession> m_sshSession;
    SshConnection m_connection;  // copy of config

    // Rendering
    float m_scrollOffset = 0;
    bool m_autoScroll = true;
    int m_cursorCol = 0, m_cursorRow = 0;
    float m_fontSize = 14;
    char m_inputBuffer[1024] = {};

    // State
    bool m_connected = false;
    SshState m_connectionState = SshState::Idle;
    std::string m_statusText;

    // Methods
    void RenderConnectionDialog();   // if not connected
    void RenderTerminalOutput();     // the terminal grid
    void RenderStatusLine();
    void ProcessPtyOutput();         // called each frame
    void ProcessKeyboardInput();     // ImGui keyboard → PTY
    void Connect();
    void Disconnect();
};
```

#### 3.5.1 Frame Loop

```cpp
void TerminalTab::Render() {
    if (!m_connected) {
        RenderConnectionDialog();
        return;
    }

    // 1. Read PTY output → libvterm
    ProcessPtyOutput();

    // 2. Render terminal grid
    RenderTerminalOutput();

    // 3. Process keyboard
    ProcessKeyboardInput();

    // 4. Status line
    RenderStatusLine();
}

void TerminalTab::ProcessPtyOutput() {
    char buf[4096];
    int n;
    while ((n = m_pty->Read(buf, sizeof(buf))) > 0) {
        m_terminal->ProcessInput(buf, n);
    }
    if (n < 0) {
        // Connection closed
        m_connected = false;
    }
}

void TerminalTab::ProcessKeyboardInput() {
    auto& io = ImGui::GetIO();

    // Captured keyboard input when terminal area is focused
    if (ImGui::IsWindowFocused()) {
        for (int i = 0; i < io.InputQueueCharacters.Size; i++) {
            unsigned int c = io.InputQueueCharacters[i];
            char utf8[8] = {};
            int len = EncodeUtf8(c, utf8);
            m_pty->Write(utf8, len);
        }
        // Special keys
        for (int key = ImGuiKey_NamedKey_BEGIN; key < ImGuiKey_NamedKey_END; key++) {
            if (ImGui::IsKeyPressed((ImGuiKey)key)) {
                const char* seq = KeyToVtSequence((ImGuiKey)key, io.KeyMods);
                if (seq) m_pty->Write(seq, strlen(seq));
            }
        }
    }
}
```

---

### 3.6 TerminalManager — Global Tab Manager

```cpp
// src/terminal/TerminalManager.h

class TerminalManager {
public:
    TerminalManager();

    void Render();                           // renders the tab bar + active terminal
    ConnectionStore& GetConnectionStore();

    // Actions
    void OpenLocalTerminal();
    void OpenSshTerminal(const SshConnection& conn);
    void CloseTerminal(int index);

private:
    // Connection profile store
    ConnectionStore m_store;

    // Active tabs
    std::vector<std::unique_ptr<TerminalTab>> m_tabs;
    int m_activeTabIndex = -1;

    // UI state
    bool m_showConnectionList = true;        // left sidebar
    bool m_showNewConnectionDialog = false;
    char m_searchFilter[256] = {};
    SplitView m_splitView{ SplitView::Type::Horizontal, 200, 80 };

    // Rendering
    void RenderSidebar();
    void RenderConnectionList();
    void RenderTabBar();
    void RenderActiveTab();
    void RenderNewConnectionDialog();
};
```

---

## 4. UI Layout

参照 Terminus 的双面板设计：

```
┌──────────────────────────────────────────────────────────────────┐
│  [Home] [repo-a] [repo-b] [*  Terminal]                          │  ← ImGuiTabBar
├───────────┬──────────────────────────────────────────────────────┤
│           │  [Local] [server-a ×] [server-b ×]  [+]              │  ← 终端内 TabBar
│  🔍 Search│──────────────────────────────────────────────────────│
│           │                                                      │
│  📁 Local │  user@host:~ $ ls -la                                │
│    bash   │  total 48                                            │
│    [+]    │  drwxr-xr-x  12 user user 4096 Jul 24 10:00 .        │
│           │  -rw-r--r--   1 user user  220 Jul 24 10:00 .bashrc  │
│  📁 Prod  │  user@host:~ $ █                                     │
│    web-01 │                                                      │
│    db-01  │                                                      │
│           │                                                      │
│  📁 Dev   │                                                      │
│    devbox │                                                      │
│           │                                                      │
├───────────┴──────────────────────────────────────────────────────┤
│  ✅ Connected │ server-a │ ssh │ ⬆ 1.2K │ ⬇ 340B │ 00:05:32    │  ← 状态栏
└──────────────────────────────────────────────────────────────────┘
```

### 4.1 左侧连接面板

- 显示 Local 分组 + 所有 SSH 连接分组
- 点击即打开/切换到对应终端
- `+` 按钮新增连接 → 弹出配置对话框
- 搜索过滤

### 4.2 右侧终端区

- 使用 `ImGui::BeginChild` + ImDrawList 自定义渲染
- 自适应列/行数（窗口 resize 时触发 `PtyAdapter::Resize`）
- 支持滚动查看历史缓冲区

### 4.3 底部状态栏

- 连接图标 + 状态文本
- 当前使用协议
- 传输速率（可选）
- 连接时长（可选）

---

## 5. Keyboard Mapping (VT Key Sequences)

将 ImGui 按键映射到标准 xterm/vt100 转义序列：

| ImGui Key            | VT Sequence              | 说明         |
| -------------------- | ------------------------ | ------------ |
| Enter                | `\r`                     |              |
| Backspace            | `\x7f`                   | DEL          |
| Tab                  | `\t`                     |              |
| Escape               | `\x1b`                   |              |
| Up/Down/Left/Right   | `\x1b[A` ~ `\x1b[D`     | 方向键       |
| Home                 | `\x1b[H`                 |              |
| End                  | `\x1b[F`                 |              |
| PageUp               | `\x1b[5~`                |              |
| PageDown             | `\x1b[6~`                |              |
| Insert               | `\x1b[2~`                |              |
| Delete               | `\x1b[3~`                |              |
| F1-F12               | `\x1b[11~` ~ `\x1b[24~` | 功能键       |
| Ctrl+C               | `\x03`                   |              |
| Ctrl+D               | `\x04`                   | EOF          |
| Ctrl+Z               | `\x1a`                   | SUSP         |
| Ctrl+L               | `\x0c`                   | clear screen |
| Ctrl+Shift+C         | —                       | 复制（不发送到 PTY） |
| Ctrl+Shift+V         | —                       | 粘贴          |
| Ctrl+Shift+N         | —                       | 新终端窗口    |

---

## 6. Directory Structure

```
src/
  terminal/
    PtyAdapter.h              # PTY 抽象接口
    LocalPty.h / .cpp         # 本地 PTY 实现 (forkpty / ConPTY)
    SshPty.h / .cpp           # SSH PTY 实现 (libssh2 channel)
    SshSession.h / .cpp       # SSH 会话管理
    TerminalEmulator.h / .cpp # libvterm 封装 + ImGui 渲染
    ConnectionStore.h / .cpp  # 连接配置 JSON 存储 + 加密
    CryptoUtil.h / .cpp       # AES-256-GCM 加密工具
    TerminalTab.h / .cpp      # 单个终端 Tab
    TerminalManager.h / .cpp  # Tab 管理器 + 连接列表 UI
    KeyMapping.h / .cpp       # ImGui按键 → VT 序列映射

  ui/
    terminal_settings_dialog.h / .cpp   # 连接配置编辑对话框
```

---

## 7. Dependency Changes

### 7.1 xmake.lua

```lua
-- 新增依赖
add_requires("libvterm")
add_requires("libssh2")
add_requires("openssl", {system = true})  -- 或 mbedtls

target("GitBee")
    -- ... existing ...
    add_packages("libvterm", "libssh2", "openssl")
    add_files("src/terminal/*.cpp")
    add_files("src/ui/terminal_settings_dialog.cpp")
```

### 7.2 可选替代

| 场景               | 默认        | 替代方案       |
| ------------------ | ----------- | -------------- |
| 加密库 (AES)      | OpenSSL     | mbedtls, tiny-AES-c |
| SSH 库             | libssh2     | libssh         |
| JSON 序列化        | nlohmann/json (现有) | —       |

---

## 8. Implementation Phases

### Phase 1: 本地终端（MVP）

**状态**: ✅ 已完成

- [x] `PtyAdapter` + `LocalPty` (Linux forkpty)
- [ ] `LocalPty` (Windows ConPTY) — 待实现
- [x] `TerminalEmulator` (libvterm integration)
- [x] 终端 ImGui 自定义渲染（逐行绘制）
- [x] 键盘事件 → VT 序列映射
- [x] TerminalTab + 集成到主 TabBar
- [x] 窗口 resize 联动 PTY resize

**实际工作量**: ~1 天

### Phase 2: SSH 远程连接 + 连接管理

**状态**: ✅ 已完成（无 libssh2，使用系统 ssh 命令）

- [x] `SshPty` (forkpty 启动 `ssh user@host`)
- [x] 认证：通过系统 ssh 支持 publickey / password / agent
- [x] `ConnectionStore` (JSON 持久化连接配置)
- [x] 左侧 SSH 连接侧栏（分组、搜索）
- [x] 新建/编辑连接对话框
- [x] 双击连接 / 右键菜单启动 SSH 终端
- [ ] `CryptoUtil` (AES 加密) — 无密码存储需求，ssh-agent 模式更安全
- [ ] 文件对话框选择密钥路径 — 需复用 FileDialog

**实际工作量**: ~1 天

### Phase 3: 增强

**目标**：完善的终端体验

- [ ] 光标闪烁
- [ ] 选中文本 + 复制（Ctrl+Shift+C）
- [ ] 粘贴（Ctrl+Shift+V）
- [ ] 终端内搜索（Ctrl+F）
- [ ] 256色 / TrueColor 支持
- [ ] 字体配置（等宽字体 + CJK fallback）
- [ ] 滚动缓冲区 + 滚轮回滚
- [ ] 右键上下文菜单
- [ ] Session 恢复（断线自动重连）
- [ ] Windows ConPTY 支持
- [ ] 文件对话框集成（选择密钥路径）

**预计工作量**: ~2-3 周

---

## 9. Integration Points

### 9.1 与现有架构的融合

```cpp
// GitBeeApp 改动（最小化入侵）

// 1. 新增成员
TerminalManager m_terminalManager;

// 2. OnCreate() → TerminalManager 初始化
m_terminalManager = std::make_unique<TerminalManager>();

// 3. OnRender() → 新增 Terminal Tab
if (ImGui::BeginTabBar("##MainTabs", ...)) {
    // ... existing Home, Repo, Config tabs ...

    // Terminal tab (always visible)
    bool terminalOpen = true;
    if (ImGui::BeginTabItem("Terminal", &terminalOpen)) {
        m_terminalManager->Render();
        ImGui::EndTabItem();
    }

    ImGui::EndTabBar();
}

// 4. Menu bar → View → Terminal (Ctrl+`)
// 5. RepoView toolbar "Terminal" button → 调起终端并 cd 到仓库路径
```

### 9.2 与 RepoView 的联动

在 `RepoView::DoGitAction("Terminal")` 中，不再启动外部终端，而是：

```cpp
// 打开内嵌终端并 cd 到仓库路径
TerminalTab* tab = m_terminalManager->OpenLocalTerminal();
tab->SetWorkingDirectory(m_repoPath);
```

---

## 10. Risk Assessment

| 风险                           | 概率 | 影响 | 缓解措施                                       |
| ------------------------------ | ---- | ---- | ---------------------------------------------- |
| Windows ConPTY 兼容性问题       | 中   | 高   | 备选方案：winpty；检测 OS 版本降级             |
| libvterm 构建困难               | 低   | 中   | xmake 集成；fallback 到 libtsm                  |
| ImGui 终端渲染性能不足          | 中   | 中   | 脏矩形增量渲染；渲染到纹理 FBO                 |
| libssh2 与服务端兼容性          | 低   | 低   | libssh2 广泛使用，兼容性好；可切 libssh         |
| CJK/Unicode 宽字符显示          | 中   | 中   | libvterm 原生支持 wcwidth；渲染时处理 width=2 |
| 密码存储安全性                  | —    | 低   | AES 加密 + 文档标注安全边界；未来升系统keychain |

---

## 11. References

- [libvterm](https://www.leonerd.org.uk/code/libvterm/) — Terminal emulation library (Paul Evans / Neovim)
- [libssh2](https://libssh2.org/) — SSH2 client C library
- [ImTerm](https://github.com/mellinoe/ImTerm) — Dear ImGui terminal widget (参考)
- [Terminus](https://github.com/Eugeny/tabby) — 参考产品
- [ConPTY](https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session) — Windows Pseudo Console API
- [xterm control sequences](https://invisible-island.net/xterm/ctlseqs/ctlseqs.html) — VT/xterm escape codes reference
- [wcwidth](https://www.cl.cam.ac.uk/~mgk25/ucs/wcwidth.c) — Markus Kuhn's wcwidth implementation (CJK width)

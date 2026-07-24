#include "ConnectionStore.h"
#include "../dbg_log.h"
#include <fstream>
#include <sstream>
#include <cstring>
#include <cstdlib>
#include <algorithm>
#include <chrono>
#include <filesystem>

// ---------------------------------------------------------------------------
// Minimal JSON helpers (just enough for SshConnection serialization)
// ---------------------------------------------------------------------------

namespace {

std::string JsonEscape(const std::string& s) {
    std::string out;
    out.reserve(s.size() + 4);
    for (char c : s) {
        switch (c) {
            case '"':  out += "\\\""; break;
            case '\\': out += "\\\\"; break;
            case '\n': out += "\\n";  break;
            case '\r': out += "\\r";  break;
            case '\t': out += "\\t";  break;
            default:   out += c;
        }
    }
    return out;
}

std::string JsonUnescape(const std::string& s) {
    std::string out;
    out.reserve(s.size());
    for (size_t i = 0; i < s.size(); i++) {
        if (s[i] == '\\' && i + 1 < s.size()) {
            switch (s[i + 1]) {
                case '"':  out += '"';  i++; break;
                case '\\': out += '\\'; i++; break;
                case 'n':  out += '\n'; i++; break;
                case 'r':  out += '\r'; i++; break;
                case 't':  out += '\t'; i++; break;
                default:   out += s[i];
            }
        } else {
            out += s[i];
        }
    }
    return out;
}

// Trim whitespace
std::string Trim(const std::string& s) {
    size_t start = s.find_first_not_of(" \t\r\n");
    if (start == std::string::npos) return "";
    size_t end = s.find_last_not_of(" \t\r\n");
    return s.substr(start, end - start + 1);
}

// Simple recursive-descent JSON parser. Only handles the subset we need:
// objects, arrays, strings, numbers, booleans.
class MiniJson {
public:
    struct Value {
        enum Type { Null, String, Number, Bool, Object, Array } type = Null;
        std::string str;
        double num = 0;
        bool flag = false;
        std::vector<std::pair<std::string, Value>> obj;   // object members
        std::vector<Value> arr;                            // array items
    };

    static Value Parse(const std::string& json) {
        size_t pos = 0;
        return ParseValue(json, pos);
    }

    static std::string Serialize(const Value& v, int indent = 0) {
        std::string ind(indent, ' ');
        switch (v.type) {
            case Value::Null: return "null";
            case Value::String:
                return "\"" + JsonEscape(v.str) + "\"";
            case Value::Number: {
                char buf[64];
                snprintf(buf, sizeof(buf), "%g", v.num);
                // Ensure integer numbers don't have trailing ".0"
                if (strchr(buf, '.') || strchr(buf, 'e')) return buf;
                return std::string(buf);
            }
            case Value::Bool: return v.flag ? "true" : "false";
            case Value::Object: {
                if (v.obj.empty()) return "{}";
                std::string s = "{\n";
                for (size_t i = 0; i < v.obj.size(); i++) {
                    s += ind + "  \"" + JsonEscape(v.obj[i].first) + "\": " + Serialize(v.obj[i].second, indent + 2);
                    if (i + 1 < v.obj.size()) s += ",";
                    s += "\n";
                }
                s += ind + "}";
                return s;
            }
            case Value::Array: {
                if (v.arr.empty()) return "[]";
                std::string s = "[\n";
                for (size_t i = 0; i < v.arr.size(); i++) {
                    s += ind + "  " + Serialize(v.arr[i], indent + 2);
                    if (i + 1 < v.arr.size()) s += ",";
                    s += "\n";
                }
                s += ind + "]";
                return s;
            }
        }
        return "null";
    }

    static std::string GetString(const Value& v, const std::string& key, const std::string& def = "") {
        if (v.type != Value::Object) return def;
        for (auto& m : v.obj)
            if (m.first == key && m.second.type == Value::String) return m.second.str;
        return def;
    }

    static int GetInt(const Value& v, const std::string& key, int def = 0) {
        if (v.type != Value::Object) return def;
        for (auto& m : v.obj)
            if (m.first == key && m.second.type == Value::Number) return (int)m.second.num;
        return def;
    }

    static bool GetBool(const Value& v, const std::string& key, bool def = false) {
        if (v.type != Value::Object) return def;
        for (auto& m : v.obj)
            if (m.first == key && m.second.type == Value::Bool) return m.second.flag;
        return def;
    }

    static std::string GetStringOr(const Value& v, const std::string& key, const char* def) {
        return GetString(v, key, def ? std::string(def) : std::string());
    }

private:
    static void SkipSpace(const std::string& j, size_t& pos) {
        while (pos < j.size() && (j[pos] == ' ' || j[pos] == '\t' || j[pos] == '\r' || j[pos] == '\n'))
            pos++;
    }

    static char Peek(const std::string& j, size_t& pos) {
        SkipSpace(j, pos);
        return pos < j.size() ? j[pos] : '\0';
    }

    static char Next(const std::string& j, size_t& pos) {
        SkipSpace(j, pos);
        if (pos >= j.size()) return '\0';
        return j[pos++];
    }

    static Value ParseValue(const std::string& j, size_t& pos) {
        char c = Peek(j, pos);
        if (c == '"') return ParseString(j, pos);
        if (c == '{') return ParseObject(j, pos);
        if (c == '[') return ParseArray(j, pos);
        if (c == 't' || c == 'f') return ParseBool(j, pos);
        if (c == 'n') return ParseNull(j, pos);
        return ParseNumber(j, pos);
    }

    static Value ParseString(const std::string& j, size_t& pos) {
        Value v;
        v.type = Value::String;
        if (Next(j, pos) != '"') return v;  // consume opening quote
        while (pos < j.size()) {
            if (j[pos] == '"') { pos++; break; }
            if (j[pos] == '\\' && pos + 1 < j.size()) {
                v.str += j[pos];
                v.str += j[pos + 1];
                pos += 2;
            } else {
                v.str += j[pos];
                pos++;
            }
        }
        // Unescape
        v.str = JsonUnescape(v.str);
        return v;
    }

    static Value ParseNumber(const std::string& j, size_t& pos) {
        Value v;
        v.type = Value::Number;
        size_t start = pos;
        if (j[pos] == '-') pos++;
        while (pos < j.size() && (j[pos] >= '0' && j[pos] <= '9')) pos++;
        if (pos < j.size() && j[pos] == '.') {
            pos++;
            while (pos < j.size() && (j[pos] >= '0' && j[pos] <= '9')) pos++;
        }
        if (pos < j.size() && (j[pos] == 'e' || j[pos] == 'E')) {
            pos++;
            if (pos < j.size() && (j[pos] == '+' || j[pos] == '-')) pos++;
            while (pos < j.size() && (j[pos] >= '0' && j[pos] <= '9')) pos++;
        }
        v.num = std::atof(j.substr(start, pos - start).c_str());
        return v;
    }

    static Value ParseBool(const std::string& j, size_t& pos) {
        Value v;
        v.type = Value::Bool;
        if (j.substr(pos, 4) == "true") { v.flag = true; pos += 4; }
        else if (j.substr(pos, 5) == "false") { v.flag = false; pos += 5; }
        return v;
    }

    static Value ParseNull(const std::string& j, size_t& pos) {
        Value v;
        if (j.substr(pos, 4) == "null") pos += 4;
        return v;
    }

    static Value ParseObject(const std::string& j, size_t& pos) {
        Value v;
        v.type = Value::Object;
        if (Next(j, pos) != '{') return v;  // consume '{'
        char c = Peek(j, pos);
        if (c == '}') { Next(j, pos); return v; }
        while (pos < j.size()) {
            c = Peek(j, pos);
            if (c == '}') { Next(j, pos); break; }
            if (v.obj.size() > 0) {
                if (Next(j, pos) != ',') break;  // skip comma
            }
            auto key = ParseString(j, pos);
            if (Peek(j, pos) == ':') Next(j, pos);
            auto val = ParseValue(j, pos);
            v.obj.push_back({key.str, val});
        }
        return v;
    }

    static Value ParseArray(const std::string& j, size_t& pos) {
        Value v;
        v.type = Value::Array;
        if (Next(j, pos) != '[') return v;
        char c = Peek(j, pos);
        if (c == ']') { Next(j, pos); return v; }
        while (pos < j.size()) {
            c = Peek(j, pos);
            if (c == ']') { Next(j, pos); break; }
            if (v.arr.size() > 0) {
                c = Next(j, pos);
                if (c != ',') { pos--; break; }
            }
            v.arr.push_back(ParseValue(j, pos));
        }
        return v;
    }
};

// Convert SshConnection::AuthMethod to/from string
const char* AuthMethodToString(SshConnection::AuthMethod m) {
    switch (m) {
        case SshConnection::Agent:     return "agent";
        case SshConnection::PublicKey: return "publickey";
        case SshConnection::Password:  return "password";
        case SshConnection::Default:   return "default";
    }
    return "default";
}

SshConnection::AuthMethod AuthMethodFromString(const std::string& s) {
    if (s == "agent")     return SshConnection::Agent;
    if (s == "publickey") return SshConnection::PublicKey;
    if (s == "password")  return SshConnection::Password;
    return SshConnection::Default;
}

} // anonymous namespace

// ---------------------------------------------------------------------------
// ConnectionStore implementation
// ---------------------------------------------------------------------------

std::string ConnectionStore::GetFilePath() {
    std::string dataDir;
#ifdef _WIN32
    char* appData = nullptr;
    size_t appDataLen = 0;
    if (_dupenv_s(&appData, &appDataLen, "APPDATA") == 0 && appData) {
        dataDir = std::string(appData) + "/GitBee";
        free(appData);
    } else {
        dataDir = "./GitBee";
    }
#else
    const char* xdgHome = std::getenv("XDG_CONFIG_HOME");
    if (xdgHome)
        dataDir = std::string(xdgHome) + "/GitBee";
    else
        dataDir = std::string(std::getenv("HOME")) + "/.config/GitBee";
#endif
    return dataDir + "/connections.json";
}

std::string ConnectionStore::GenerateId() {
    // Simple UUID-like string (enough for uniqueness)
    static int counter = 0;
    auto now = std::chrono::system_clock::now();
    auto ms = std::chrono::duration_cast<std::chrono::milliseconds>(
        now.time_since_epoch()).count();
    char buf[48];
    snprintf(buf, sizeof(buf), "conn_%lld_%d", (long long)ms, counter++);
    return buf;
}

ConnectionStore::ConnectionStore()
    : m_filePath(GetFilePath()) {
}

ConnectionStore::~ConnectionStore() {
}

void ConnectionStore::Load() {
    m_connections.clear();

    std::ifstream file(m_filePath);
    if (!file.is_open()) {
        LOG_DEBUG("No connections.json found at %s", m_filePath.c_str());
        return;
    }

    std::stringstream buffer;
    buffer << file.rdbuf();
    std::string content = buffer.str();

    auto root = MiniJson::Parse(content);
    auto arr = root;
    if (root.type == MiniJson::Value::Object) {
        // Handle {"version": 1, "connections": [...]} format
        arr = MiniJson::Value();
        arr.type = MiniJson::Value::Array;
        bool found = false;
        for (auto& m : root.obj) {
            if (m.first == "connections" && m.second.type == MiniJson::Value::Array) {
                arr = m.second;
                found = true;
                break;
            }
        }
        if (!found) {
            LOG_WARN("connections.json: no 'connections' array found");
            return;
        }
    }

    if (arr.type != MiniJson::Value::Array) {
        LOG_WARN("connections.json: expected array");
        return;
    }

    for (auto& item : arr.arr) {
        if (item.type != MiniJson::Value::Object) continue;
        SshConnection conn;
        conn.id = MiniJson::GetString(item, "id", GenerateId());
        conn.name = MiniJson::GetString(item, "name");
        conn.host = MiniJson::GetString(item, "host");
        conn.port = MiniJson::GetInt(item, "port", 22);
        conn.username = MiniJson::GetString(item, "username");
        conn.authMethod = AuthMethodFromString(MiniJson::GetString(item, "authMethod", "default"));
        conn.privateKeyPath = MiniJson::GetString(item, "privateKeyPath");
        conn.group = MiniJson::GetString(item, "group");
        conn.order = MiniJson::GetInt(item, "order", 0);
        conn.notes = MiniJson::GetString(item, "notes");

        if (!conn.host.empty()) {
            m_connections.push_back(conn);
        }
    }

    // Sort by order
    std::sort(m_connections.begin(), m_connections.end(),
              [](const SshConnection& a, const SshConnection& b) {
                  if (a.group != b.group) return a.group < b.group;
                  if (a.order != b.order) return a.order < b.order;
                  return a.name < b.name;
              });

    LOG_INFO("Loaded %zu SSH connections from %s", m_connections.size(), m_filePath.c_str());
}

void ConnectionStore::Save() const {
    // Build JSON manually
    std::string json;
    json += "{\n";
    json += "  \"version\": 1,\n";
    json += "  \"connections\": [\n";

    for (size_t i = 0; i < m_connections.size(); i++) {
        const auto& c = m_connections[i];
        json += "    {\n";
        json += "      \"id\": \"" + JsonEscape(c.id) + "\",\n";
        json += "      \"name\": \"" + JsonEscape(c.name) + "\",\n";
        json += "      \"host\": \"" + JsonEscape(c.host) + "\",\n";
        json += "      \"port\": " + std::to_string(c.port) + ",\n";
        json += "      \"username\": \"" + JsonEscape(c.username) + "\",\n";
        json += "      \"authMethod\": \"" + std::string(AuthMethodToString(c.authMethod)) + "\",\n";
        json += "      \"privateKeyPath\": \"" + JsonEscape(c.privateKeyPath) + "\",\n";
        json += "      \"group\": \"" + JsonEscape(c.group) + "\",\n";
        json += "      \"order\": " + std::to_string(c.order) + ",\n";
        json += "      \"notes\": \"" + JsonEscape(c.notes) + "\"\n";
        json += "    }";
        if (i + 1 < m_connections.size()) json += ",";
        json += "\n";
    }

    json += "  ]\n";
    json += "}\n";

    // Ensure directory exists
    std::error_code ec;
    std::filesystem::path dir = std::filesystem::path(m_filePath).parent_path();
    if (!std::filesystem::exists(dir, ec)) {
        std::filesystem::create_directories(dir, ec);
    }

    std::ofstream file(m_filePath);
    if (file.is_open()) {
        file << json;
        LOG_DEBUG("Saved %zu SSH connections to %s", m_connections.size(), m_filePath.c_str());
    } else {
        LOG_ERROR("Failed to write connections.json to %s", m_filePath.c_str());
    }
}

void ConnectionStore::SaveConnection(const SshConnection& conn) {
    SshConnection copy = conn;
    if (copy.id.empty())
        copy.id = GenerateId();

    for (auto& c : m_connections) {
        if (c.id == copy.id) {
            c = copy;
            Save();
            return;
        }
    }
    m_connections.push_back(copy);
    Save();
}

void ConnectionStore::RemoveConnection(const std::string& id) {
    for (auto it = m_connections.begin(); it != m_connections.end(); ++it) {
        if (it->id == id) {
            m_connections.erase(it);
            Save();
            return;
        }
    }
}

const SshConnection* ConnectionStore::FindById(const std::string& id) const {
    for (auto& c : m_connections) {
        if (c.id == id) return &c;
    }
    return nullptr;
}

std::vector<std::string> ConnectionStore::GetGroups() const {
    std::vector<std::string> groups;
    for (auto& c : m_connections) {
        if (c.group.empty()) continue;
        if (std::find(groups.begin(), groups.end(), c.group) == groups.end())
            groups.push_back(c.group);
    }
    return groups;
}

std::vector<const SshConnection*> ConnectionStore::GetByGroup(const std::string& group) const {
    std::vector<const SshConnection*> result;
    for (auto& c : m_connections) {
        if (c.group == group)
            result.push_back(&c);
    }
    return result;
}

std::vector<const SshConnection*> ConnectionStore::GetUngrouped() const {
    std::vector<const SshConnection*> result;
    for (auto& c : m_connections) {
        if (c.group.empty())
            result.push_back(&c);
    }
    return result;
}

std::string ConnectionStore::BuildSshCommand(const SshConnection& conn) {
    std::string cmd = "ssh";

    // Port
    if (conn.port != 22) {
        cmd += " -p " + std::to_string(conn.port);
    }

    // Identity file
    if (conn.authMethod == SshConnection::PublicKey && !conn.privateKeyPath.empty()) {
        cmd += " -i \"" + conn.privateKeyPath + "\"";
    }

    // Force PTY allocation
    cmd += " -t";

    // User@Host
    if (!conn.username.empty()) {
        cmd += " " + conn.username + "@" + conn.host;
    } else {
        cmd += " " + conn.host;
    }

    return cmd;
}

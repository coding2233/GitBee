#pragma once

#include <imgui.h>
#include <filesystem>
#include <vector>
#include <string>
#include <algorithm>
#include <ctime>
#include <chrono>
#include <sstream>
#include <iomanip>

static char* FormatLastWriteTime(const std::filesystem::directory_entry& entry, char* buf, size_t bufSize)
{
    auto ftime = entry.last_write_time();
    auto sctp = std::chrono::time_point_cast<std::chrono::system_clock::duration>(
        ftime - decltype(ftime)::clock::now() + std::chrono::system_clock::now());
    std::time_t tt = std::chrono::system_clock::to_time_t(sctp);
    std::tm mt;
#ifdef _WIN32
    localtime_s(&mt, &tt);
#else
    localtime_r(&tt, &mt);
#endif
    std::strftime(buf, bufSize, "%Y-%m-%d %H:%M", &mt);
    return buf;
}

static std::string FormatFileSize(uintmax_t size)
{
    if (size < 1024) return std::to_string(size) + " B";
    if (size < 1024 * 1024) return std::to_string(size / 1024) + " KB";
    if (size < 1024ULL * 1024 * 1024) return std::to_string(size / (1024 * 1024)) + " MB";
    return std::to_string(size / (1024ULL * 1024 * 1024)) + " GB";
}

struct FileDialog
{
    enum class Type { OpenFile, SelectFolder };
    enum class SortOrder { None, Asc, Desc };

    bool open = false;
    Type type = Type::OpenFile;
    char resultBuffer[1024] = {};

    FileDialog() = default;

    void OpenDialog(Type t, const char* initialPath = nullptr)
    {
        open = true;
        type = t;
        fileSelectIndex = 0;
        folderSelectIndex = 0;
        currentFile.clear();
        currentFolder.clear();
        error[0] = 0;
        sortColumns = {SortOrder::None, SortOrder::None, SortOrder::None, SortOrder::None};
        if (initialPath && strlen(initialPath) > 0)
        {
            auto p = std::filesystem::path(initialPath);
            if (std::filesystem::is_directory(p))
                currentPath = initialPath;
            else if (std::filesystem::exists(p))
                currentPath = p.remove_filename().string();
            else
                currentPath = std::filesystem::current_path().string();
        }
        else
        {
            currentPath = std::filesystem::current_path().string();
        }
    }

    bool Render()
    {
        if (!open) return false;

        ImGui::SetNextWindowSize(ImVec2(740, 440), ImGuiCond_Appearing);
        const char* title = (type == Type::OpenFile) ? "Select a file" : "Select a folder";
        if (!ImGui::Begin(title, &open, ImGuiWindowFlags_NoResize))
        {
            ImGui::End();
            return false;
        }

        std::vector<std::filesystem::directory_entry> folders, files;
        try
        {
            for (auto& p : std::filesystem::directory_iterator(currentPath))
            {
                if (p.is_directory()) folders.push_back(p);
                else files.push_back(p);
            }
        }
        catch (...) {}

        ImGui::TextUnformatted(currentPath.c_str());
        ImGui::Separator();

        float leftW = 200.0f;
        float rightW = ImGui::GetContentRegionAvail().x - leftW - ImGui::GetStyle().ItemSpacing.x;

        ImGui::BeginChild("##dirs", ImVec2(leftW, ImGui::GetContentRegionAvail().y - 64), true, ImGuiWindowFlags_HorizontalScrollbar);

        if (ImGui::Selectable("..", false, ImGuiSelectableFlags_AllowDoubleClick, ImVec2(ImGui::GetContentRegionAvail().x, 0)))
        {
            if (ImGui::IsMouseDoubleClicked(0))
                currentPath = std::filesystem::path(currentPath).parent_path().string();
        }
        for (int i = 0; i < (int)folders.size(); ++i)
        {
            bool selected = (i == folderSelectIndex);
            if (ImGui::Selectable(folders[i].path().filename().string().c_str(), selected,
                ImGuiSelectableFlags_AllowDoubleClick, ImVec2(ImGui::GetContentRegionAvail().x, 0)))
            {
                currentFile.clear();
                if (ImGui::IsMouseDoubleClicked(0))
                {
                    currentPath = folders[i].path().string();
                    folderSelectIndex = 0;
                    fileSelectIndex = 0;
                    ImGui::SetScrollHereY(0.0f);
                    currentFolder.clear();
                }
                else
                {
                    folderSelectIndex = i;
                    currentFolder = folders[i].path().filename().string();
                }
            }
        }
        ImGui::EndChild();

        ImGui::SameLine();

        ImGui::BeginChild("##files", ImVec2(rightW, ImGui::GetContentRegionAvail().y - 64), true, ImGuiWindowFlags_HorizontalScrollbar);

        ImGui::Columns(4);
        ImGui::SetColumnWidth(0, rightW * 0.45f);
        ImGui::SetColumnWidth(1, rightW * 0.12f);
        ImGui::SetColumnWidth(2, rightW * 0.15f);
        ImGui::SetColumnWidth(3, rightW * 0.28f);

        if (ImGui::Selectable("Name")) DoSort(0);
        ImGui::NextColumn();
        if (ImGui::Selectable("Size")) DoSort(1);
        ImGui::NextColumn();
        if (ImGui::Selectable("Type")) DoSort(2);
        ImGui::NextColumn();
        if (ImGui::Selectable("Date")) DoSort(3);
        ImGui::NextColumn();
        ImGui::Separator();

        ApplySort(files);

        char timeBuf[64];
        for (int i = 0; i < (int)files.size(); ++i)
        {
            bool selected = (i == fileSelectIndex);
            if (ImGui::Selectable(files[i].path().filename().string().c_str(), selected,
                ImGuiSelectableFlags_AllowDoubleClick, ImVec2(ImGui::GetContentRegionAvail().x, 0)))
            {
                fileSelectIndex = i;
                currentFile = files[i].path().filename().string();
                currentFolder.clear();
            }
            ImGui::NextColumn();
            ImGui::TextUnformatted(FormatFileSize(files[i].file_size()).c_str());
            ImGui::NextColumn();
            ImGui::TextUnformatted(files[i].path().extension().string().c_str());
            ImGui::NextColumn();
            ImGui::TextUnformatted(FormatLastWriteTime(files[i], timeBuf, sizeof(timeBuf)));
            ImGui::NextColumn();
        }
        ImGui::EndChild();

        std::string selectedPath = currentPath;
        if (!selectedPath.empty() && selectedPath.back() != '/' && selectedPath.back() != '\\')
            selectedPath += '/';
        selectedPath += !currentFolder.empty() ? currentFolder : currentFile;

        ImGui::PushItemWidth(ImGui::GetContentRegionAvail().x);
        ImGui::InputText("##path", resultBuffer, sizeof(resultBuffer));
        ImGui::PopItemWidth();
        ImGui::SetCursorPosY(ImGui::GetCursorPosY() + 4);

        if (ImGui::Button("New Folder"))
            ImGui::OpenPopup("NewFolderPopup");
        ImGui::SameLine();

        bool disableDelete = currentFolder.empty();
        if (disableDelete) { ImGui::PushItemFlag(ImGuiItemFlags_Disabled, true); ImGui::PushStyleVar(ImGuiStyleVar_Alpha, 0.5f); }
        if (ImGui::Button("Delete Folder"))
            ImGui::OpenPopup("DeleteFolderPopup");
        if (disableDelete) { ImGui::PopStyleVar(); ImGui::PopItemFlag(); }

        DrawNewFolderPopup();
        DrawDeleteFolderPopup();

        ImGui::SameLine();
        ImGui::SetCursorPosX(ImGui::GetWindowWidth() - 150);

        bool shouldClose = false;
        if (ImGui::Button("Cancel"))
        {
            shouldClose = true;
        }
        ImGui::SameLine();
        if (ImGui::Button("Choose"))
        {
            if (type == Type::SelectFolder)
            {
                if (!currentFolder.empty() || !currentFile.empty())
                {
                    snprintf(resultBuffer, sizeof(resultBuffer), "%s/%s",
                        currentPath.c_str(), (currentFolder.empty() ? currentFile : currentFolder).c_str());
                    shouldClose = true;
                }
                else if (strlen(resultBuffer) > 0)
                {
                    shouldClose = true;
                }
                else
                {
                    snprintf(resultBuffer, sizeof(resultBuffer), "%s", currentPath.c_str());
                    shouldClose = true;
                }
            }
            else
            {
                if (currentFile.empty())
                {
                    snprintf(error, sizeof(error), "%s", "Error: You must select a file!");
                }
                else
                {
                    snprintf(resultBuffer, sizeof(resultBuffer), "%s/%s",
                        currentPath.c_str(), currentFile.c_str());
                    shouldClose = true;
                }
            }
        }

        if (error[0])
            ImGui::TextColored(ImColor(1.0f, 0.0f, 0.2f, 1.0f), "%s", error);

        ImGui::End();

        if (shouldClose)
        {
            open = false;
            return true;
        }
        return false;
    }

private:
    std::string currentPath;
    std::string currentFile;
    std::string currentFolder;
    int fileSelectIndex = 0;
    int folderSelectIndex = 0;
    char error[256] = {};
    struct { SortOrder name, size, type, date; } sortColumns;

    void DoSort(int col)
    {
        sortColumns = {SortOrder::None, SortOrder::None, SortOrder::None, SortOrder::None};
        auto& target = (col == 0) ? sortColumns.name : (col == 1) ? sortColumns.size : (col == 2) ? sortColumns.type : sortColumns.date;
        target = (target == SortOrder::Desc) ? SortOrder::Asc : SortOrder::Desc;
    }

    void ApplySort(std::vector<std::filesystem::directory_entry>& files)
    {
        if (sortColumns.name != SortOrder::None)
        {
            bool desc = sortColumns.name == SortOrder::Desc;
            std::sort(files.begin(), files.end(), [desc](auto& a, auto& b) {
                return desc ? a.path().filename().string() > b.path().filename().string()
                            : a.path().filename().string() < b.path().filename().string();
            });
        }
        else if (sortColumns.size != SortOrder::None)
        {
            bool desc = sortColumns.size == SortOrder::Desc;
            std::sort(files.begin(), files.end(), [desc](auto& a, auto& b) {
                return desc ? a.file_size() > b.file_size() : a.file_size() < b.file_size();
            });
        }
        else if (sortColumns.type != SortOrder::None)
        {
            bool desc = sortColumns.type == SortOrder::Desc;
            std::sort(files.begin(), files.end(), [desc](auto& a, auto& b) {
                return desc ? a.path().extension().string() > b.path().extension().string()
                            : a.path().extension().string() < b.path().extension().string();
            });
        }
        else if (sortColumns.date != SortOrder::None)
        {
            bool desc = sortColumns.date == SortOrder::Desc;
            std::sort(files.begin(), files.end(), [desc](auto& a, auto& b) {
                return desc ? a.last_write_time() > b.last_write_time()
                            : a.last_write_time() < b.last_write_time();
            });
        }
    }

    void DrawNewFolderPopup()
    {
        if (!ImGui::BeginPopupModal("NewFolderPopup", nullptr, ImGuiWindowFlags_AlwaysAutoResize))
            return;

        ImGui::Text("Enter a name for the new folder");
        static char newName[256] = {};
        static char newError[256] = {};
        ImGui::InputText("##newfolder", newName, sizeof(newName));
        if (ImGui::Button("Create"))
        {
            if (strlen(newName) == 0)
                snprintf(newError, sizeof(newError), "%s", "Folder name can't be empty");
            else
            {
                std::filesystem::create_directory(currentPath + "/" + newName);
                newName[0] = 0;
                newError[0] = 0;
                ImGui::CloseCurrentPopup();
            }
        }
        ImGui::SameLine();
        if (ImGui::Button("Cancel"))
        {
            newName[0] = 0;
            newError[0] = 0;
            ImGui::CloseCurrentPopup();
        }
        if (newError[0])
            ImGui::TextColored(ImColor(1.0f, 0.0f, 0.2f, 1.0f), "%s", newError);
        ImGui::EndPopup();
    }

    void DrawDeleteFolderPopup()
    {
        if (!ImGui::BeginPopupModal("DeleteFolderPopup", nullptr, ImGuiWindowFlags_AlwaysAutoResize))
            return;

        ImGui::TextColored(ImColor(1.0f, 0.0f, 0.2f, 1.0f), "Are you sure you want to delete this folder?");
        ImGui::TextUnformatted(currentFolder.c_str());
        if (ImGui::Button("Yes"))
        {
            std::filesystem::remove(currentPath + "/" + currentFolder);
            ImGui::CloseCurrentPopup();
        }
        ImGui::SameLine();
        if (ImGui::Button("No"))
            ImGui::CloseCurrentPopup();
        ImGui::EndPopup();
    }
};

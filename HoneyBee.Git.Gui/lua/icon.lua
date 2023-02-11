local fileIcons = {}
fileIcons["file_type_text"] = {".txt",".meta"}
fileIcons["file_type_json"] = {".json"}
fileIcons["file_type_c"] = {".c",".C"}
fileIcons["file_type_cheader"] = {".h"}
fileIcons["file_type_cpp"] = {".h++",".hpp"}
fileIcons["file_type_csharp"] = {".cs"}
fileIcons["file_type_lua"] = {".lua"}
fileIcons["file_type_sln"] = {".sln"}
fileIcons["file_type_csproj"] = {".csproj"}
fileIcons["file_type_binary"] = {".dll",".so",".lib",".a",".bytes",".byte"}
fileIcons["file_type_yaml"] = {".yaml",".yml"}
fileIcons["file_type_xml"] = {".xml"}
fileIcons["file_type_protobuf"] = {".proto"}
fileIcons["file_type_photoshop2"] = {".psd",".PSD"}
fileIcons["file_type_image"] = {".jpg",".png",".jpeg",".tga",".bmp",".psd",".gif",".hdr","pic",".svg",".JPG",".PNG",".TGA",".BMP",".PSD",".GIF",".HDR",".PIC"}
fileIcons["file_type_light_shaderlab"] = {".unity",".unity3d",".prefab",".mat",".shader"}

local function file_exists(name)
   local f=io.open(name,"r")
   if f~=nil then io.close(f) return true else return false end
end



function GetFolderIcon( folderName )
    local folderIcon = "lua/style/icons/"..folderName..".png"
	if file_exists(folderIcon) then
		return folderIcon
	end

	return "lua/style/icons/default_folder.png"
end

function GetFileIcon( fileExtension )
	local fileIconName = GetFileIconName(fileExtension)
	local fileIcon = "lua/style/icons/"..fileIconName..".png"
	if file_exists(fileIcon) then
		return fileIcon
	end
	return "lua/style/icons/default_file.png"
end


function GetFileIconName( fileExtension )
	for k,v in pairs(fileIcons) do
	   for i,ext in ipairs(v) do
			if ext == fileExtension then
				return k
			end
       end
    end
	return "default_file"
end


local cimgui = require("cimgui")
print(cimgui)


if ffi.os == "Windows" then
	print("ffi widdow")
end

function OnDraw()
	cimgui.igText("tesstt")
end



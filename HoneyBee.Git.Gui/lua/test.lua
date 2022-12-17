local ffi = require("ffi")

ffi.cdef[[
	void igText(const char* fmt,...);
]]


local cimgui = ffi.load("cimgui")

print(ffi)
print(ffi.C)
print(cimgui)

if ffi.os == "Windows" then
	print("ffi widdow")
end

function OnDraw()
	cimgui.igText("tesstt")
end



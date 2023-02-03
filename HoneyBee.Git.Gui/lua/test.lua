--[[

local cimgui = require("cimgui")
print(cimgui)


if ffi.os == "Windows" then
	print("ffi widdow")
end

function OnDraw()
	cimgui.igText("tesstt")
end

]]--


local dbg = require("debugger")

print[[
	Welcome to the interactive debugger.lua tutorial.
	You'll want to open tutorial.lua in an editor to follow along.
	
	First of all, just drop debugger.lua in your project. It's one file.
	Load it the usual way using require. Ex:
	local dbg = require("debugger")
	
	debugger.lua doesn't support traditional breakpoints.
	So to get into the debugger, call it like a function.
	Real breakpoints would be better, but this
	keeps debugger.lua simple and very fast.
	At the end you'll find out how to open it automatically on a crash.
	
	Notice how debugger.lua prints out your current file and line
	as well as which function you are in.
	Keep a close watch on this as you follow along.
	It should be stopped a line after the dbg() call.
	(Line 86 unless I forgot to double update it)
	
	Sometimes functions don't have global names.
	It might print the name of a method, local variable
	that held the function, or file:line where it starts.
	
	Type 'w' to show 5 lines of surrounding code directly in
	the debugger. (w = Where) Type 'w 3' to show 3 lines, etc.
	Alternatively, set dbg.auto_where to a number
	to run it automatically every time the program advances.
	
	Once you've tried the where command, type 's' to step to
	the next line. (s = Step to the next executable line)
]]
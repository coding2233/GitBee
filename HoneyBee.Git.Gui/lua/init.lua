print("hello init.lua")

local cimgui = require("cimgui")
print(cimgui)

AddViewCommand("test --- AddViewCommand 忠实的")

function OnBranchPopupItem( ... )
	-- body
	cimgui.igText("tesstt")
	cimgui.igMenuItem_Bool("tesstt0002","",false,true)
end


function OnRemotePopupItem( ... )
	-- body
		cimgui.igText("tesstt")
	cimgui.igMenuItem_Bool("tesstt0002","",false,true)
end


function OnHeadPopupItem( ... )
	-- body
		cimgui.igText("tesstt")
	cimgui.igMenuItem_Bool("tesstt0002","",false,true)
end


function OnCommitPopupItem( ... )
	-- body
		cimgui.igText("tesstt")
	cimgui.igMenuItem_Bool("tesstt0002","",false,true)
end

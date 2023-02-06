--- lua执行优先顺序
--- custom/lua/init.lua
--- lua/init.lua
print("hello init.lua")

--require "robert.debugger"

--local ZBS = "D:\\Program Files (x86)\\ZeroBraneStudioEduPack-1.90-win32"
--package.path = package.path..";./?/?.lua;"..ZBS.."\\lualibs\\?\\?.lua;"..ZBS.."\\lualibs\\?.lua;"
--package.cpath = package.cpath..";"..ZBS.."\\bin\\?.dll;"..ZBS.."\\bin\\clibs\\?.dll;"

--package.path = package.path..";./?/?.lua;"

--print("package.path",package.path)
--print("package.cpath",package.cpath)

-- to debug a script, add the following line to it:
-- require("mobdebug").start()

require "style"

--print(cimgui)
print("Style",Style)

for k,v in pairs(Style) do
	print(k,v)
end


print("lua enable success")

-- AddViewCommand("test --- AddViewCommand 忠实的")



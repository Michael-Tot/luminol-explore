local a = 1
local function a(a, ...)      -- shadows the outer a; parameter shadows the function
  for a = 1, a do print(a) end
  return a
end
local t = {}
function t:m(x) return self, x end
local function rec(n) if n > 0 then return rec(n - 1) end end
print(a, local1)              -- local1 is a global here: your counter must skip it
local a = "Hel" .. "lo" -- greeting         -- comment survives
local b = "a" .. "b" == y                   -- folds to "ab" == y
local c = "a" .. x .. "b"                   -- unchanged
local local
local d = "line1\n" .. "line2"              -- escapes round-trip: "line1\nline2"
local e = x .. "a" .. "b"
local function greet(ply)
    if not IsValid(ply) then return end
    local msg = "Hello, " .. ply:Nick() .. "!"
    ply:ChatPrint(msg)
end

hook.Add("PlayerInitialSpawn", "greet", greet)

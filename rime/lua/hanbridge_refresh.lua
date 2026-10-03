-- Handles the internal F24 refresh signal emitted by the HanBridge bridge.

local function processor(key, env)
    if key:repr() ~= "F24" then
        return 2
    end

    local context = env.engine.context
    if context:is_composing() and context:get_option("hanbridge_translation") then
        context:refresh_non_confirmed_composition()
        return 1
    end

    return 2
end

return { func = processor }
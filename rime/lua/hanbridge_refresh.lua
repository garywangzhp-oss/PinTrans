-- Handles F24 refreshes and explicit translation shortcuts.

local function debug_log(_) end

local selected_translate_keys = {
    ["Control+Alt+Return"] = true,
    ["Control+Alt+Enter"] = true,
    ["Control+Alt+KP_Enter"] = true
}

local pinyin_translate_keys = {
    ["Control+Alt+p"] = true
}

local function processor(key, env)
    local context = env.engine.context
    local representation = key:repr()

    if not key:release() then
        debug_log("key=" .. representation)
    end

    if not key:release() and selected_translate_keys[representation] then
        if context:get_option("hanbridge_translation") then
            local selected = context:get_selected_candidate()
            local commit_text = context:get_commit_text()
            _G.hanbridge_pinyin_target = nil
            _G.hanbridge_pinyin_result_source = nil
            _G.hanbridge_pinyin_result_translation = nil
            _G.hanbridge_auto_source = nil
            _G.hanbridge_auto_translation = nil
            _G.hanbridge_manual_result_source = nil
            _G.hanbridge_manual_result_translation = nil
            _G.hanbridge_manual_target =
                (commit_text and commit_text ~= "" and commit_text)
                or (selected and selected.text)
                or nil
            if _G.hanbridge_manual_target then
                context:refresh_non_confirmed_composition()
                return 1
            end
        end
    end

    if not key:release() and pinyin_translate_keys[representation] then
        if context:get_option("hanbridge_translation") and context.input ~= "" then
            _G.hanbridge_manual_target = nil
            _G.hanbridge_auto_source = nil
            _G.hanbridge_auto_translation = nil
            _G.hanbridge_manual_result_source = nil
            _G.hanbridge_manual_result_translation = nil
            _G.hanbridge_pinyin_result_source = nil
            _G.hanbridge_pinyin_result_translation = nil
            _G.hanbridge_pinyin_target = context.input
            context:refresh_non_confirmed_composition()
            return 1
        end
    end

    if string.find(representation, "F24", 1, true) then
        if context:is_composing() and context:get_option("hanbridge_translation") then
            context:refresh_non_confirmed_composition()
            return 1
        end
    end

    return 2
end

return { func = processor }
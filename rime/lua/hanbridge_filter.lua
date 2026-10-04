-- HanBridge candidate filter.
-- Persistent manual results keep selected/pinyin translations visible for the
-- current composition instead of being replaced by automatic translation.

local local_app_data = os.getenv("LOCALAPPDATA")
if not local_app_data then
    return { func = function(input, env)
        for cand in input:iter() do
            yield(cand)
        end
    end }
end

local ipc_dir = local_app_data .. "\\HanBridge\\ipc"
local request_file = ipc_dir .. "\\request.json"
local request_tmp = request_file .. ".tmp"
local response_file = ipc_dir .. "\\response.txt"

local last_requested_key = nil
local function debug_log(_) end

local function count_han(text)
    local count = 0
    for _ in text:gmatch("[\228-\233][\128-\191][\128-\191]") do
        count = count + 1
    end
    return count
end

local function has_disallowed_shape(text)
    if text:find("\r", 1, true) or text:find("\n", 1, true) then
        return true
    end
    if text:find("@", 1, true) then
        return true
    end
    if text:find("://", 1, true) or text:find("www.", 1, true) then
        return true
    end
    if text:match("^[A-Za-z]:\\") or text:match("^\\\\") then
        return true
    end
    if text:match("[{};]") or text:find("=>", 1, true) or text:find("->", 1, true) then
        return true
    end
    if text:find("sk-", 1, true) then
        return true
    end
    return false
end

local function json_escape(value)
    local replacements = {
        ['"'] = '\\"',
        ["\\"] = "\\\\",
        ["\b"] = "\\b",
        ["\f"] = "\\f",
        ["\n"] = "\\n",
        ["\r"] = "\\r",
        ["\t"] = "\\t"
    }

    return '"' .. value:gsub('[%c\\"]', function(char)
        return replacements[char] or string.format("\\u%04x", string.byte(char))
    end) .. '"'
end

local function make_request_id()
    local clock_part = math.floor((os.clock() % 1) * 1000000)
    return string.format("%d-%06d", os.time(), clock_part)
end

local function write_request(text, mode)
    local request_json = string.format(
        '{"request_id":%s,"source_text":%s,"mode":%s}',
        json_escape(make_request_id()),
        json_escape(text),
        json_escape(mode))

    local file = io.open(request_tmp, "wb")
    if not file then
        return false
    end

    file:write(request_json)
    file:close()

    os.remove(request_file)
    local ok = os.rename(request_tmp, request_file)
    if not ok then
        os.remove(request_tmp)
        return false
    end

    return true
end

local function read_response()
    local file = io.open(response_file, "rb")
    if not file then
        return nil
    end

    local values = {}
    for line in file:lines() do
        local key, value = line:match("^([%w_]+)=(.*)$")
        if key then
            values[key] = value:gsub("\r$", "")
        end
    end
    file:close()
    os.remove(response_file)
    return values
end

local function is_english_candidate(candidate)
    return candidate and candidate.type == "hanbridge_en"
end

local function clear_auto_result()
    _G.hanbridge_auto_source = nil
    _G.hanbridge_auto_translation = nil
end

local function clear_manual_result()
    _G.hanbridge_manual_result_source = nil
    _G.hanbridge_manual_result_translation = nil
end

local function clear_pinyin_result()
    _G.hanbridge_pinyin_result_source = nil
    _G.hanbridge_pinyin_result_translation = nil
end

local function clear_task_state()
    last_requested_key = nil
    _G.hanbridge_manual_target = nil
    _G.hanbridge_pinyin_target = nil
    clear_manual_result()
    clear_pinyin_result()
end

local function filter(input, env)
    local context = env.engine.context
    if not context:get_option("hanbridge_translation") then
        clear_task_state()
        for cand in input:iter() do
            yield(cand)
        end
        return
    end

    local input_text = context.input or ""
    local commit_text = context:get_commit_text() or ""
    local selected = context:get_selected_candidate()

    if _G.hanbridge_pinyin_target and _G.hanbridge_pinyin_target ~= input_text then
        _G.hanbridge_pinyin_target = nil
        clear_pinyin_result()
    end
    if _G.hanbridge_pinyin_result_source and _G.hanbridge_pinyin_result_source ~= input_text then
        clear_pinyin_result()
    end
    if _G.hanbridge_manual_target and _G.hanbridge_manual_target ~= commit_text then
        _G.hanbridge_manual_target = nil
        clear_manual_result()
    end
    if _G.hanbridge_manual_result_source and _G.hanbridge_manual_result_source ~= commit_text then
        clear_manual_result()
    end

    local pinyin_active = _G.hanbridge_pinyin_target
        or _G.hanbridge_pinyin_result_source == input_text
    local manual_active = _G.hanbridge_manual_target
        or _G.hanbridge_manual_result_source == commit_text

    if (selected and is_english_candidate(selected)) and not pinyin_active then
        manual_active = false
        _G.hanbridge_manual_target = nil
        clear_manual_result()
    end

    if pinyin_active then
        manual_active = false
    end

    local mode = pinyin_active and "pinyin" or "chinese"
    local first = true
    local source = nil
    local translated = nil

    for cand in input:iter() do
        local is_first = first
        first = false

        if is_first then
            if pinyin_active then
                source = _G.hanbridge_pinyin_target or _G.hanbridge_pinyin_result_source or input_text
                if _G.hanbridge_pinyin_result_source == source then
                    translated = _G.hanbridge_pinyin_result_translation
                end
            elseif manual_active then
                source = _G.hanbridge_manual_target or _G.hanbridge_manual_result_source or commit_text
                if _G.hanbridge_manual_result_source == source then
                    translated = _G.hanbridge_manual_result_translation
                end
            else
                source = cand.text or ""
                if _G.hanbridge_auto_source and _G.hanbridge_auto_source ~= source then
                    clear_auto_result()
                end
                if _G.hanbridge_auto_source == source then
                    translated = _G.hanbridge_auto_translation
                end
            end

            local translatable = false
            if source and mode == "pinyin" then
                translatable = string.len(source) >= 2 and not has_disallowed_shape(source)
            elseif source then
                translatable = count_han(source) >= 2 and not has_disallowed_shape(source)
            end

            if translatable and not translated then
                local request_class = pinyin_active and "pinyin" or (manual_active and "manual" or "auto")
                local request_key = request_class .. "\n" .. source
                if request_key ~= last_requested_key then
                    if write_request(source, mode) then
                        last_requested_key = request_key
                    end
                end

                local response = read_response()
                if response then
                    debug_log(string.format(
                        "response status=%s mode=%s source_len=%d candidate_len=%d match=%s translation_len=%d",
                        tostring(response.status),
                        tostring(response.mode),
                        string.len(response.source or ""),
                        string.len(source),
                        tostring(response.source == source),
                        string.len(response.translation or "")))
                end

                if response
                    and response.status == "ok"
                    and response.mode == mode
                    and response.source == source
                    and response.translation
                    and response.translation ~= ""
                    and response.translation ~= source then
                    translated = response.translation
                    if pinyin_active then
                        _G.hanbridge_pinyin_result_source = source
                        _G.hanbridge_pinyin_result_translation = translated
                        _G.hanbridge_pinyin_target = nil
                    elseif manual_active then
                        _G.hanbridge_manual_result_source = source
                        _G.hanbridge_manual_result_translation = translated
                        _G.hanbridge_manual_target = nil
                    else
                        _G.hanbridge_auto_source = source
                        _G.hanbridge_auto_translation = translated
                    end
                    debug_log(string.format("yield_translation mode=%s start=%d end=%d", mode, cand.start, cand._end))
                elseif response then
                    debug_log("discard_translation")
                end
            end
        end

        yield(cand)

        local is_target = false
        local full_span = false
        local candidate_comment = " EN · 首选"

        if pinyin_active then
            is_target = is_first
            full_span = true
            candidate_comment = " EN · 拼音"
        elseif manual_active then
            is_target = selected and not is_english_candidate(selected) and cand.text == selected.text
            full_span = true
            candidate_comment = " EN · 选中"
        else
            is_target = is_first
        end

        if translated and is_target then
            local candidate_end = full_span and string.len(input_text) or cand._end
            yield(Candidate(
                "hanbridge_en",
                cand.start,
                candidate_end,
                translated,
                candidate_comment))
        end
    end
end

return { func = filter }
-- HanBridge candidate filter.
-- Writes the current top Chinese candidate to the C# bridge and injects a
-- translated candidate as candidate 2 when a matching response is available.

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

local last_requested_text = nil
local debug_marker = (os.getenv("TEMP") or ".") .. "\\hanbridge-debug.on"
local debug_log_path = (os.getenv("TEMP") or ".") .. "\\hanbridge_debug.log"

local function debug_log(message)
    local marker = io.open(debug_marker, "rb")
    if not marker then
        return
    end
    marker:close()

    local log = io.open(debug_log_path, "a")
    if log then
        log:write(os.date("%Y-%m-%d %H:%M:%S ") .. message .. "\n")
        log:close()
    end
end

local function count_han(text)
    -- LuaJIT compatibility: avoid the Lua 5.3 utf8 library. Count the
    -- three-byte UTF-8 ranges that contain the common CJK characters.
    local count = 0
    for _ in text:gmatch("[\228-\233][\128-\191][\128-\191]") do
        count = count + 1
    end
    return count
end

local function has_han(text)
    return count_han(text) > 0
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

local function write_request(text)
    local request_json = string.format(
        '{"request_id":%s,"source_text":%s}',
        json_escape(make_request_id()),
        json_escape(text))

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

local function filter(input, env)
    local context = env.engine.context
    if not context:get_option("hanbridge_translation") then
        last_requested_text = nil
        for cand in input:iter() do
            yield(cand)
        end
        return
    end

    local first = true
    for cand in input:iter() do
        local translated = nil

        if first then
            first = false
            local source = cand.text or ""
            local han_count = count_han(source)

            if han_count >= 2 and not has_disallowed_shape(source) then
                if source ~= last_requested_text then
                    if write_request(source) then
                        last_requested_text = source
                    end
                end

                local response = read_response()
                if response then
                    debug_log(string.format(
                        "response status=%s source_len=%d candidate_len=%d match=%s translation_len=%d",
                        tostring(response.status),
                        string.len(response.source or ""),
                        string.len(source),
                        tostring(response.source == source),
                        string.len(response.translation or "")))
                end
                if response
                    and response.status == "ok"
                    and response.source == source
                    and response.translation
                    and response.translation ~= ""
                    and response.translation ~= source then
                    translated = response.translation
                    debug_log(string.format("yield_translation start=%d end=%d", cand.start, cand._end))
                elseif response then
                    debug_log("discard_translation")
                end
            end
        end

        yield(cand)

        if translated then
            yield(Candidate(
                "hanbridge_en",
                cand.start,
                cand._end,
                translated,
                " EN · 译首选"))
        end
    end
end

return { func = filter }
-- fl26lateguards.lua -- built by tools/lbpack.py from fl26superguard.lua, fl26resultsguard.lua, fl26ctlguard.lua, fl26kitguard.lua; edit those files, not this one.
-- Each part logs as it always did. Sider's Lua has no pcall, so a part whose init raises
-- stops the ones after it -- the order puts the oldest, most tried guards first.

-- ==== fl26superguard.lua ====
local part0 = (function()
--[[
fl26superguard -- stop the UEFA Super Cup setup from reading entry -1.

What crashed. 0x1413604a0 prepares the UEFA Super Cup (regulation 7). It walks the season's
entry vector (48-byte records, u16 regulation id first) for the Champions League entry (id 4)
and the Europa League entry (id 6), and indexes both results without checking them: a
missing entry leaves -1, `lea rax,[r8+r8*2]; shl rax,4` turns that into -48, and the read at
0x1413605a9 faults. On 23 September a Master League season in our Montenegrin league went
down exactly there between day 81 and day ~100 (fault offset 0x13605a9), in an August-start
world where one of the two entries was not in the vector. Why it is missing is a separate
question; this module only keeps a missing entry from taking the whole game with it.

What this writes. The 8 bytes at 0x14136059a (lea + shl) become a jmp to a trampoline that
leaves the function through its own epilogue when either lookup came back -1, and otherwise
runs the two original instructions and resumes at 0x1413605a2:

    14252ee20  cmp  r8d, -1            ; Champions League entry not found
    14252ee24  je   out
    14252ee26  cmp  ebx, -1            ; Europa League entry not found
    14252ee29  je   out
    14252ee2b  lea  rax, [r8+r8*2]     ; the original two instructions
    14252ee2f  shl  rax, 4
    14252ee33  jmp  0x1413605a2
    14252ee38  out: jmp 0x141360b0a    ; the function's epilogue

Leaving at 0x141360b0a is what the function already does when regulation 7 is absent: the
local vector at [rbp-0x28] is still empty at that point, so nothing is left allocated. The
cost of taking the exit is a Super Cup without its two entrants for that season -- when both
entries exist, nothing changes.

The trampoline sits at 0x14252ee20, after fl26augseason's (0x14252ee00, 0x15 bytes).

Same apply rule as every fl26 module: verify all sites first, write only if all match.
--]]

local m = {}

local patches = {
  {va=0x14252ee20, old="0000000000000000000000000000000000000000000000000000000000", new="4183f8ff741283fbff740d4b8d044048c1e004e96a17e3fee9cd1ce3fe", why="Super Cup trampoline: leave 0x1413604a0 through its epilogue when the UCL (4) or UEL (6) entry is missing, else lea/shl and resume at 0x1413605a2"},
  {va=0x14136059a, old="4b8d044048c1e004", new="e981e81c01909090", why="jmp from the unchecked index into the trampoline at 0x14252ee20"},
}

local function unhex(s)
  local out = {}
  for i = 1, #s, 2 do
    out[#out + 1] = string.char(tonumber(string.sub(s, i, i + 1), 16))
  end
  return table.concat(out)
end

local function tohex(s)
  if s == nil then return "nil" end
  local out = {}
  for i = 1, #s do
    out[#out + 1] = string.format("%02x", string.byte(s, i))
  end
  return table.concat(out)
end

function m.init(ctx)
  local n = #patches
  log(string.format("fl26superguard: set '%s', %d patches, verifying", "superguard", n))

  -- pass 1: read only. Nothing is written until every single site has been confirmed.
  local bad = 0
  for i = 1, n do
    local p = patches[i]
    local want = unhex(p.old)
    local cur = memory.read(p.va, #want)
    if cur ~= want then
      log(string.format("fl26superguard: MISMATCH at 0x%x: found %s, expected %s  [%s]",
                        p.va, tohex(cur), p.old, p.why))
      bad = bad + 1
    end
  end
  if bad > 0 then
    log(string.format("fl26superguard: ABORTED, %d of %d sites did not match. "
                      .. "Nothing was written; the game is unmodified. "
                      .. "This usually means the exe is a different build than the one "
                      .. "the patch set was generated from.", bad, n))
    return
  end

  -- pass 2: write, then read back, because writing to the code section can silently fail
  -- if the page is not made writable for us. Each site is logged as it goes, so that if a
  -- write throws and Sider stops the module, the log still names the last one that worked.
  local written, failed = 0, 0
  for i = 1, n do
    local p = patches[i]
    local new = unhex(p.new)
    memory.write(p.va, new)
    local back = memory.read(p.va, #new)
    if back == new then
      written = written + 1
      log(string.format("fl26superguard: [%d/%d] 0x%x %s -> %s  %s",
                        i, n, p.va, p.old, p.new, p.why))
    else
      failed = failed + 1
      log(string.format("fl26superguard: WRITE FAILED at 0x%x (%s) -- read back %s",
                        p.va, p.why, tohex(back)))
    end
  end

  -- The patch has to land before the game allocates the edit block, and the log proves
  -- that it does: everything above is written during module init, which Sider finishes
  -- before it reports "Sider initialization complete", and the first `read_file::` line
  -- comes thousands of lines later. An earlier version of this file also registered a
  -- livecpk_get_filepath handler to re-read the first site at the first file request. It
  -- worked, but it made Sider log "lua ERROR from module_get_filepath" on every single
  -- file lookup -- 27811 of them in one session -- so it is gone. The ordering was never
  -- in doubt; the check was.

  if failed == 0 then
    log(string.format("fl26superguard: applied all %d patches -- %s", written, "a missing Champions League or Europa League entry skips the Super Cup setup instead of crashing"))
  else
    log(string.format("fl26superguard: PARTIAL: %d written, %d failed. The game is now in an "
                      .. "inconsistent state -- quit and report the addresses above.",
                      written, failed))
  end
end

return m
end)()

-- ==== fl26resultsguard.lua ====
local part1 = (function()
--[[
fl26resultsguard -- keep an empty matchday-results list from ending the game.

What crashed. menu::MatchDayResultsBase (vtable 0x1427215e0) opens the results screen that
follows `Skip Match`. Its slot 0xd8, 0x140ca2060, draws the headline and then picks a page out
of a vector at +0x90 (32-byte entries, each holding a vector of 8-byte items). When the page
index at +0xa8 is out of range it falls back to entry 0 -- and when the vector is empty it
reports "invalid vector<T> subscript" (0x140ca2203), which ends in ucrtbase's fail-fast
0xc0000409 at +0xa527e. The same happens when entry 0's own item vector is empty (0x140ca21db).

On 25 September a Master League career in our world died there twice, both times on day 49
of its third year -- the first leg of the Europa and Conference League knockout play-offs
that fl26swiss puts into regulations 188/189 -- with the same stack (0x14149ff1e ... 0x140e43462
-> 0x140ca2060). A tester hit the same fault (ucrtbase, offset 0xa527e) at the Europa League
knockout in another world (issue #9). Why that screen gets no entries for that day is a
separate question; this module only keeps the empty list from taking the whole game with it.

What this writes. The two `lea rcx, "invalid vector<T> subscript"` in front of the reports
become a jmp to 0x140ca2289, the function's own epilogue (restore rbx/rsi, al = 1, pop rdi,
ret). Nothing else in the function has run yet that needs undoing: the headline has been
set, the page is simply left empty. When the list has entries, nothing changes.

Same apply rule as every fl26 module: verify all sites first, write only if all match.
--]]

local m = {}

local patches = {
  {va=0x140ca21db, old="488d0df6fd8f01", new="e9a90000009090", why="results screen: entry 0 has no items -> leave through the epilogue at 0x140ca2289 instead of reporting a bad subscript"},
  {va=0x140ca2203, old="488d0dcefd8f01", new="e9810000009090", why="results screen: no entries at all -> leave through the epilogue at 0x140ca2289 instead of reporting a bad subscript"},
}

local function unhex(s)
  local out = {}
  for i = 1, #s, 2 do
    out[#out + 1] = string.char(tonumber(string.sub(s, i, i + 1), 16))
  end
  return table.concat(out)
end

local function tohex(s)
  if s == nil then return "nil" end
  local out = {}
  for i = 1, #s do
    out[#out + 1] = string.format("%02x", string.byte(s, i))
  end
  return table.concat(out)
end

function m.init(ctx)
  local n = #patches
  log(string.format("fl26resultsguard: set '%s', %d patches, verifying", "resultsguard", n))

  -- pass 1: read only. Nothing is written until every single site has been confirmed.
  local bad = 0
  for i = 1, n do
    local p = patches[i]
    local want = unhex(p.old)
    local cur = memory.read(p.va, #want)
    if cur ~= want then
      log(string.format("fl26resultsguard: MISMATCH at 0x%x: found %s, expected %s  [%s]",
                        p.va, tohex(cur), p.old, p.why))
      bad = bad + 1
    end
  end
  if bad > 0 then
    log(string.format("fl26resultsguard: ABORTED, %d of %d sites did not match. "
                      .. "Nothing was written; the game is unmodified. "
                      .. "This usually means the exe is a different build than the one "
                      .. "the patch set was generated from.", bad, n))
    return
  end

  -- pass 2: write, then read back, because writing to the code section can silently fail
  -- if the page is not made writable for us. Each site is logged as it goes, so that if a
  -- write throws and Sider stops the module, the log still names the last one that worked.
  local written, failed = 0, 0
  for i = 1, n do
    local p = patches[i]
    local new = unhex(p.new)
    memory.write(p.va, new)
    local back = memory.read(p.va, #new)
    if back == new then
      written = written + 1
      log(string.format("fl26resultsguard: [%d/%d] 0x%x %s -> %s  %s",
                        i, n, p.va, p.old, p.new, p.why))
    else
      failed = failed + 1
      log(string.format("fl26resultsguard: WRITE FAILED at 0x%x (%s) -- read back %s",
                        p.va, p.why, tohex(back)))
    end
  end

  if failed == 0 then
    log(string.format("fl26resultsguard: applied all %d patches -- %s", written, "an empty matchday-results list leaves the screen empty instead of crashing"))
  else
    log(string.format("fl26resultsguard: PARTIAL: %d written, %d failed. The game is now in an "
                      .. "inconsistent state -- quit and report the addresses above.",
                      written, failed))
  end
end

return m
end)()

-- ==== fl26ctlguard.lua ====
local part2 = (function()
--[[
fl26ctlguard -- after a club change, give the old club back to the computer.

What went wrong. Byte +0x41d of a team record says who plays it: 0 is the user's pad, 0xff
is the computer. On 25 September a Master League career had two clubs at 0: the one the
manager runs (club C) and club B, the club the manager had been sacked from. The manager
history at block+0x17b5780 shows A -> B -> C. The first move handed A back to the computer
and the second did not.

What that breaks. The hub's "Next" shows the old club's fixtures (through the day schedule,
below), Skip Match lists its
results with the pad icon, the current-match context keeps picking its games, and the save
list names the career after it: the save header takes the FIRST club at 0, and B (row 548)
comes before C (row 1056).

Why it is not fixed at the source. The club change (0x140adde80) finds the old club
through the global at 0x1436f9a60, which 41 menu call sites overwrite through its setter
0x140e40f90. A sacking and a job offer go through several of those menus, so the handle it
builds can name the wrong club. Proving which one needs a sacking, and a sacking cannot be
produced on demand. This guard makes the result right whatever the path was.

What this writes. The day loop's call to 0x1412fd680 at 0x1413071ff (the routine that
writes the match screen's controller sides onto the two match-context teams) goes through
a trampoline at 0x14252ef00. The trampoline makes the original call and then sweeps the team
array:

    user   = [block+0x1798ab0]             the user club's handle (0x1414bb650 reads it)
    only if that club exists and is itself at 0 (a Master League career, not BAL)
    for every team at 0 that is not the user club:
        skip the two match-context teams, but only when the user club is one of them
        (the match screen may put the pad on the other side of the user's own match)
        otherwise set it to 0xff, and mark the user's day schedule as stale

The day schedule. The hub's fixture strip ("Next") does not read the controller bytes. It
reads a per-controller table of one entry per day, {match id, competition, round, kind,
team}, at block+0x19115f0 + controller*0x16dc (0x1642a28 before the -calendar set of 0.2.0) (365 entries of 16 bytes after an 8-byte
head whose +4 is the owner club's handle). 0x14158cfb0 files every scheduled match into the
table of each side's controller (0x14151ad90 = the team's +0x41d), so while the old club was
still at 0 its whole season went into the user's table, and it stays there after the byte is
fixed. The day loop (0x1413071e1 -> 0x1413007d0 -> 0x141350650) rebuilds table 0 with
0x14158c820 whenever the owner handle at block+0x19115f4 (was 0x1642a2c) is not the user club. Writing -1
there is how this guard asks for that rebuild; it happens the next day, from the corrected
controller bytes.

Nothing else is touched: other controller values (a second pad = 1, 2, ...) stay as they
are. The teams base and count are read from the relocated lookup 0x1414bca80 after
fl26caps has run, so the guard follows whatever set is installed. This module must be
listed AFTER fl26caps in sider.ini.

Cave 0x14252ef00..0x14252eff9, the last free stretch before .rdata (0x14252f000), after
fl26seasonend's flag byte at 0x14252eef0.

Same apply rule as every fl26 module: verify all sites first, write only if all match.
--]]

local m = {}

local CAVE = 0x14252ef00
-- built for teams at +0x1877070 and the count at +0x3b20880; both are replaced below
local TRAMP = "4883ec28e877e7dcfe488b05006f1d014885c00f84dc0000004c8b40484d85c00f84cf00000041bbff"
  .. "ffffffbaffffffff4c8b50504d85d2740e458b9a985a0300418b9228610300458b88b08a79014539cb"
  .. "74104439ca740b41bbffffffffbaffffffff4489c825ff3f00003dfd3f00000f837e0000004869c090"
  .. "060000498d8c00707087014439090f856600000080b91d040000000f8559000000498d887070870141"
  .. "8b808008b20385c00f84430000003d004000000f873800000080b91d040000007524448b114539ca74"
  .. "1c4539da74174139d27412c6811d040000ff41c780f4159101ffffffff4881c190060000ffc875c848"
  .. "83c428c3"
local TEAMS_LE, COUNT_LE = "70708701", "8008b203"

local function unhex(s)
  local out = {}
  for i = 1, #s, 2 do
    out[#out + 1] = string.char(tonumber(string.sub(s, i, i + 1), 16))
  end
  return table.concat(out)
end

local function tohex(s)
  if s == nil then return "nil" end
  local out = {}
  for i = 1, #s do
    out[#out + 1] = string.format("%02x", string.byte(s, i))
  end
  return table.concat(out)
end

function m.init(ctx)
  -- the relocated lookup: `lea rdi,[r8+teams]` at 0x1414bcade, `mov eax,[r8+count]` at 0x1414bcb24
  local lea = memory.read(0x1414bcade, 7)
  local cnt = memory.read(0x1414bcb24, 7)
  if lea == nil or cnt == nil or string.sub(lea, 1, 3) ~= unhex("498db8")
     or string.sub(cnt, 1, 3) ~= unhex("418b80") then
    log(string.format("fl26ctlguard: ABORTED, the team lookup at 0x1414bca80 is not the "
                      .. "expected code (%s / %s); nothing was written", tohex(lea), tohex(cnt)))
    return
  end
  local teams_le, count_le = tohex(string.sub(lea, 4, 7)), tohex(string.sub(cnt, 4, 7))
  local tramp = string.gsub(TRAMP, TEAMS_LE, teams_le)
  tramp = string.gsub(tramp, COUNT_LE, count_le)

  local patches = {
    {va=CAVE, old=string.rep("00", #tramp / 2), new=tramp,
     why="trampoline: call 0x1412fd680, then give every club at controller 0 except the user's back to the computer"},
    {va=0x1413071ff, old="e87c64ffff", new="e8fc7c2201",
     why="the day loop's call to 0x1412fd680 -> the trampoline at 0x14252ef00"},
  }
  local n = #patches
  log(string.format("fl26ctlguard: teams at +0x%s, count at +0x%s, %d patches, verifying",
                    teams_le, count_le, n))

  -- pass 1: read only. Nothing is written until every single site has been confirmed.
  local bad = 0
  for i = 1, n do
    local p = patches[i]
    local want = unhex(p.old)
    local cur = memory.read(p.va, #want)
    if cur ~= want then
      log(string.format("fl26ctlguard: MISMATCH at 0x%x: found %s, expected %s  [%s]",
                        p.va, tohex(cur), p.old, p.why))
      bad = bad + 1
    end
  end
  if bad > 0 then
    log(string.format("fl26ctlguard: ABORTED, %d of %d sites did not match. "
                      .. "Nothing was written; the game is unmodified.", bad, n))
    return
  end

  -- pass 2: write, then read back. The trampoline goes first, so the call is never
  -- redirected into a cave that is not there.
  local written, failed = 0, 0
  for i = 1, n do
    local p = patches[i]
    local new = unhex(p.new)
    memory.write(p.va, new)
    local back = memory.read(p.va, #new)
    if back == new then
      written = written + 1
      log(string.format("fl26ctlguard: [%d/%d] 0x%x %d bytes  %s", i, n, p.va, #new, p.why))
    else
      failed = failed + 1
      log(string.format("fl26ctlguard: WRITE FAILED at 0x%x (%s) -- read back %s",
                        p.va, p.why, tohex(back)))
    end
  end

  if failed == 0 then
    log(string.format("fl26ctlguard: applied all %d patches -- only the user's club keeps the user's controller", written))
  else
    log(string.format("fl26ctlguard: PARTIAL: %d written, %d failed. The game is now in an "
                      .. "inconsistent state -- quit and report the addresses above.",
                      written, failed))
  end
end

return m
end)()

-- ==== fl26kitguard.lua ====
local part3 = (function()
--[[
fl26kitguard -- the Kit Server's goalkeeper pick on a club with no kit of the game.

What went wrong. With "Kits you can edit in the game" (the recipe's editable_kits) a new club
borrows no kit from a club of the game, so it has no licensed kit and ctx.kits.get(team, kit)
answers nil for it. The Kit Server (common\kits.lua) picks the goalkeepers' kits at every
kick-off in choose_gk_kits, which reads p_home.ShirtColor1 off that answer without looking:

    [string "common\kits.lua"]:1143: attempt to index local 'p_home' (a nil value)

and the match never starts (Discord, LumpierTheGunner, 2026-10-07: every match crashed with
the box ticked).

What this does. ctx.kits.get is looked up on every call, so it can be wrapped once here for
every module. Only when the answer is nil AND the caller is choose_gk_kits does the wrapper
hand back a neutral grey kit: that function wants nothing but the two shirt colours, to pick
goalkeeper kits that stand out from them. Every other caller still gets nil, so the Kit
Server's "unlicensed, kits disabled" test (has_kit = ctx.kits.get(team, 0)) and its fallbacks
work as before. Sider has no debug library, so the caller is told apart by the order of calls
instead: choose_gk_kits asks ctx.kits.get_current_kit_id(side) and right away
ctx.kits.get(<that side's team>, <that kit id>). The id is remembered when it is asked and used up
by the next get; get_current_team forgets it, because the licence test in load_configs_for_team is
always reached after a get_current_team or with no kit id asked at all.
]]

local m = {}
local GREY = "#808080"

function m.init(ctx)
  local k = ctx.kits
  if type(k) ~= "table" or type(k.get) ~= "function" or type(k.get_current_kit_id) ~= "function" then
    log("fl26kitguard: ctx.kits.get not there -- nothing to guard")
    return
  end
  local get, cur_kit, cur_team = k.get, k.get_current_kit_id, k.get_current_team
  local armed_side, armed_kit = nil, nil
  local count = 0
  k.get_current_kit_id = function(side, ...)
    local id, is_gk = cur_kit(side, ...)
    armed_side, armed_kit = side, id
    return id, is_gk
  end
  if type(cur_team) == "function" then
    k.get_current_team = function(...)
      armed_side, armed_kit = nil, nil
      return cur_team(...)
    end
  end
  k.get = function(team, kit, ...)
    local side, akit = armed_side, armed_kit
    armed_side, armed_kit = nil, nil
    local r = get(team, kit, ...)
    if r == nil and side ~= nil and kit == akit then
      local want = (side == 0) and ctx.home_team or ctx.away_team
      if want ~= nil and team == want then
        count = count + 1
        if count <= 20 then
          log(string.format("fl26kitguard: team %s has no kit of the game (kit %s) -- grey for the goalkeeper pick",
                            tostring(team), tostring(kit)))
        end
        return { ShirtColor1 = GREY, ShirtColor2 = GREY }
      end
    end
    return r
  end
  log("fl26kitguard: ctx.kits.get wrapped -- a club without a kit of the game no longer stops the goalkeeper pick")
end

return m
end)()

local PARTS = { { "fl26superguard", part0 }, { "fl26resultsguard", part1 }, { "fl26ctlguard", part2 }, { "fl26kitguard", part3 } }
local m = {}
function m.init(ctx)
  for _, p in ipairs(PARTS) do
    if type(p[2]) == "table" and p[2].init then
      p[2].init(ctx)          -- no pcall in Sider's Lua (run023, 2026-10-07)
    end
  end
end
return m

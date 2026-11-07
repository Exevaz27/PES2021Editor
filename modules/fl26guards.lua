-- fl26guards.lua -- built by tools/lbpack.py from fl26nullguard.lua, fl26nullguard2.lua, fl26nullguard4.lua, fl26nullguard5.lua, fl26nullguard7.lua, fl26nullguard8.lua, fl26nullguard9.lua, fl26nullguard10.lua, fl26nullguard11.lua; edit those files, not this one.
-- Each part logs as it always did. Sider's Lua has no pcall, so a part whose init raises
-- stops the ones after it -- the order puts the oldest, most tried guards first.

-- ==== fl26nullguard.lua ====
local part0 = (function()
--[[
fl26caps -- runtime patch applier (nullguard set).

Defensive fix, NOT a capacity patch. Written by hand (not by patchset.py) because it
installs a code-cave trampoline rather than resizing a field; every byte was computed and
verified against the exe before being written here.

Set:     nullguard
Summary: null-check at fn 0x141fea5b0, 2 patches (trampoline + prologue redirect)

The bug: fn 0x141fea5b0 does `mov rdi,[rcx+0x18]` at 0x141fea5ba with no null check, where
rcx is the result of graphics-resource factory 0x141fea410. When the factory returns null
(E_INVALIDARG on the resource create), the deref crashes. This was observed with ZERO of
our patches applied (the `none` set), reached through PES21_Hook.dll -- so it is a stock
game bug that our extra clubs only make more likely to hit.

The fix: overwrite the 10-byte prologue of 0x141fea5b0 with a jump to a trampoline in the
zero padding at the tail of .trace (0x14252e68c, executable, same section we already patch).
The trampoline returns 0 when rcx is null -- identical to the function's own internal
failure paths, which set rbx=0 and return it, an outcome the caller already handles -- and
otherwise runs the original prologue and jumps back to 0x141fea5ba. All-or-nothing: it
verifies every target byte first and writes only if all match.

Install:
  1. copy this file to <game>\SiderAddons\modules\fl26caps.lua
  2. add to sider.ini, in the [modules] list:   lua.module = "fl26caps.lua"
  3. start the game and read sider.log -- it says exactly what happened

The exe has no ASLR (DllCharacteristics 0x8120), so the addresses are absolute and stable.
The applier checks that anyway by verifying the bytes.
--]]

local m = {}

local patches = {
  {va=0x14252e800, old="0000000000000000000000000000000000000000000000", new="4885c9740f48895c2418574883ec40e9a6bdabff31c0c3", why="null-guard trampoline: test rcx,rcx; jz null_ret; <relocated prologue>; jmp 0x141fea5ba; null_ret: xor eax,eax; ret"},
  {va=0x141fea5b0, old="48895c2418574883ec40", new="e94b4254009090909090", why="redirect fn 0x141fea5b0 prologue (10 bytes) to trampoline at 0x14252e68c (jmp rel32 + 5 nop)"},
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
  log(string.format("fl26caps: set '%s', %d patches, verifying", "nullguard", n))

  -- pass 1: read only. Nothing is written until every single site has been confirmed.
  local bad = 0
  for i = 1, n do
    local p = patches[i]
    local want = unhex(p.old)
    local cur = memory.read(p.va, #want)
    if cur ~= want then
      log(string.format("fl26caps: MISMATCH at 0x%x: found %s, expected %s  [%s]",
                        p.va, tohex(cur), p.old, p.why))
      bad = bad + 1
    end
  end
  if bad > 0 then
    log(string.format("fl26caps: ABORTED, %d of %d sites did not match. "
                      .. "Nothing was written; the game is unmodified. "
                      .. "This usually means the exe is a different build than the one "
                      .. "the patch set was generated from.", bad, n))
    return
  end

  -- pass 2: write, then read back, because writing to the code section can silently fail
  -- if the page is not made writable for us. The trampoline (site 1) is written before the
  -- prologue redirect (site 2), so the jump target exists before the jump goes live.
  local written, failed = 0, 0
  for i = 1, n do
    local p = patches[i]
    local new = unhex(p.new)
    memory.write(p.va, new)
    local back = memory.read(p.va, #new)
    if back == new then
      written = written + 1
      log(string.format("fl26caps: [%d/%d] 0x%x %s -> %s  %s",
                        i, n, p.va, p.old, p.new, p.why))
    else
      failed = failed + 1
      log(string.format("fl26caps: WRITE FAILED at 0x%x (%s) -- read back %s",
                        p.va, p.why, tohex(back)))
    end
  end

  if failed == 0 then
    log(string.format("fl26caps: applied all %d patches -- %s", written, "nullguard: null-check at 0x141fea5b0"))
  else
    log(string.format("fl26caps: PARTIAL: %d written, %d failed. The game is now in an "
                      .. "inconsistent state -- quit and report the addresses above.",
                      written, failed))
  end
end

return m
end)()

-- ==== fl26nullguard2.lua ====
local part1 = (function()
--[[
fl26caps -- runtime patch applier (nullguard2 set).

Defensive fix, NOT a capacity patch.  Written by hand, like the first nullguard, because it
installs a code-cave trampoline rather than resizing a field; every byte below was computed
against the exe and disassembled back before being written here.

Set:     nullguard2
Summary: null-check at 0x140fc9238, 2 patches (trampoline + site redirect)

The bug: the function at 0x140fc9210 is handed an id in `dx`, looks it up with
`call 0x141579b90`, and reads the result without checking it:

    140fc922e  call  0x141579b90
    140fc9235  mov   r14, rax
    140fc9238  cmp   dword ptr [rax + 0x3c0], ebp     <-- rax can be 0
    140fc923e  jbe   0x140fc93c6                      <-- nothing to do

On 15 September 2026 that lookup returned null for id 0x405 and killed a season at day 257,
forty-two days into a run -- fault offset 0x140fc9238, a read of address 0x3c0, which is a
null pointer plus the field's own offset rather than a corrupted one.  Unlike 0x1484ed4c0
this is in `.trace`, the game's own code, so it can be reasoned about and guarded.

The fix: the function already has a path for "the list is empty" -- the `jbe` on the very
next line, to the epilogue at 0x140fc93c6.  A null result means there is no list at all,
which is the same outcome, so the guard simply takes that branch.  The six bytes of the
`cmp` are replaced by a jump to a trampoline that tests the pointer, jumps to 0x140fc93c6
when it is null, and otherwise performs the original `cmp` and returns to 0x140fc923e with
the flags it set -- nothing between the `cmp` and the `jbe` touches them.

The trampoline sits in the 26 bytes of zero padding between the date-spread stub's table
(ends 0x14252e7e6) and the first nullguard's trampoline (starts 0x14252e800), verified
empty in the exe.  It is 20 bytes.  All-or-nothing: every target byte is checked first and
nothing is written unless all of them match.

Install:
  1. copy this file to <game>\SiderAddons\modules\fl26nullguard2.lua
  2. add to sider.ini, in the [modules] list:   lua.module = "fl26nullguard2.lua"
     -- after fl26nullguard.lua, so the two trampolines are written in a fixed order
  3. start the game and read sider.log

The exe has no ASLR (DllCharacteristics 0x8120), so the addresses are absolute and stable.
The applier checks that anyway by verifying the bytes.
--]]

local m = {}

-- The trampoline lives at 0x14252e880, on the 0x20 grid the other guards use above
-- 0x14252e800.  It used to sit at 0x14252e7e8, below that line, where fl26caps' fixture-date
-- table ends -- and that table grows with every league added.  Two spare bytes were all that
-- stood between them.  caps runs first, so once the table reached this address, this module's
-- verify pass would find data where it expected padding and abort itself: the guard silently
-- not installed, with nothing in the log to say the crash it prevents was back.  Anything
-- placed here must stay above 0x14252e800, where .trace's file-backed bytes end and the
-- loader's zero-fill begins.
local patches = {
  {va=0x14252e880, old="0000000000000000000000000000000000000000",
   new="4885c00f843daba9fe39a8c0030000e9aaa9a9fe",
   why="null-guard trampoline: test rax,rax; jz 0x140fc93c6; cmp [rax+0x3c0],ebp; jmp 0x140fc923e"},
  {va=0x140fc9238, old="39a8c0030000", new="e94356560190",
   why="redirect the unchecked deref at 0x140fc9238 to the trampoline (jmp rel32 + nop)"},
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
  log(string.format("fl26caps: set '%s', %d patches, verifying", "nullguard2", n))

  -- pass 1: read only.  Nothing is written until every site has been confirmed.
  local bad = 0
  for i = 1, n do
    local p = patches[i]
    local want = unhex(p.old)
    local cur = memory.read(p.va, #want)
    if cur ~= want then
      log(string.format("fl26caps: MISMATCH at 0x%x: found %s, expected %s  [%s]",
                        p.va, tohex(cur), p.old, p.why))
      bad = bad + 1
    end
  end
  if bad > 0 then
    log(string.format("fl26caps: ABORTED, %d of %d sites did not match. "
                      .. "Nothing was written; the game is unmodified. "
                      .. "If site 1 is the mismatch, another module has taken that padding.",
                      bad, n))
    return
  end

  -- pass 2: write, then read back, because a write into the code section can silently fail
  -- if the page is not writable for us.  The trampoline goes first, so the jump target
  -- exists before the jump does.
  local written, failed = 0, 0
  for i = 1, n do
    local p = patches[i]
    local new = unhex(p.new)
    memory.write(p.va, new)
    local back = memory.read(p.va, #new)
    if back == new then
      written = written + 1
      log(string.format("fl26caps: [%d/%d] 0x%x %s -> %s  %s",
                        i, n, p.va, p.old, p.new, p.why))
    else
      failed = failed + 1
      log(string.format("fl26caps: WRITE FAILED at 0x%x (%s) -- read back %s",
                        p.va, p.why, tohex(back)))
    end
  end

  if failed == 0 then
    log(string.format("fl26caps: applied all %d patches -- %s", written,
                      "nullguard2: null-check at 0x140fc9238"))
  else
    log(string.format("fl26caps: PARTIAL: %d written, %d failed. The game is now in an "
                      .. "inconsistent state -- quit and report the addresses above.",
                      written, failed))
  end
end

return m
end)()

-- ==== fl26nullguard4.lua ====
local part2 = (function()
--[[
fl26caps -- runtime patch applier (nullguard4 set).

Defensive fix, NOT a capacity patch.  Written by hand, like the other nullguards, because it
installs a code-cave trampoline rather than resizing a field; every byte below was computed
against the exe and disassembled back before being written here.

Set:     nullguard4
Summary: null-check at 0x1415720d0, 2 patches (trampoline + prologue redirect)

The bug: the function at 0x1415720d0 takes a coach record in `rdx` (stride 0x258, with a
40-byte id table at +0x220), saves it in rbx, and walks that table without ever checking the
pointer:

    1415720d0  mov    qword ptr [rsp + 0x20], rbx      <-- prologue, 5 bytes
    ...
    141572100  mov    rbx, rdx                         <-- argument 2, the coach record
    ...
    14157211e  lea    r8, [rbx + 0x220]
    141572125  movzx  ecx, byte ptr [r8]               <-- rbx can be 0

The fault is at 0x141572125, a read of address 0x220 -- a null pointer plus the field's own
offset rather than a corrupted one.  The null arrives from the caller at 0x140f143a0, which
passes the result of the coach lookup 0x1414bc780 straight through:

    140f14384  call   0x1414bc780
    ...
    140f1439d  mov    rdx, rax                         <-- unchecked
    140f143a0  call   0x1415720d0

Its sibling at 0x141522a51 does exactly the check that is missing:

    141522a27  call   0x1414bc780
    141522a2c  mov    r13, rax
    141522a2f  test   rax, rax
    141522a32  je     0x141522b3c                      <-- skips the call entirely
    ...
    141522a51  call   0x1415720d0

So the game's own answer for "there is no coach record" is: do not call this function.  The
guard gives 0x1415720d0 that same behaviour from the inside -- when rdx is null it returns
at once, which is what the checked caller already arranges.

The return value is safe to fake.  0x1415720d0 has a single exit (0x14157238d, per .pdata
the function spans 0x1415720d0..0x14157238e) and it never sets rax on the way out; whatever
is in rax is the leftover of the stack-cookie check at 0x14159f3e0.  All seven callers --
0x140f143a0, 0x1412638b5, 0x14126fd69, 0x1413184ec, 0x141522a51, 0x14156adf3, 0x14156e9a2 --
discard rax: each either overwrites it before any use or ignores it outright.  The function
is effectively void, so `xor eax, eax; ret` matches the normal path.

The fix: the first five bytes of the prologue (`mov [rsp+0x20], rbx`) are replaced by a jump
to a trampoline.  The trampoline tests rdx, returns 0 when it is null, and otherwise runs the
relocated prologue and jumps back to 0x1415720d5 -- an instruction boundary (`push rbp`), so
nothing is split.  The relocated `mov` is a home-slot spill only; when the guard returns early
it is skipped along with the epilogue that would have restored it, and rbx itself is untouched.

The trampoline sits at 0x14252e840, in the tail of the last page of .trace: past the raw data
the file ends at (0x14252e800), before .rdata begins (0x14252f000), and after every cave range
this project already uses -- the caps date-spread stub and table at 0x14252e690..0x14252e7e6
(until 0.1.7; since then on a page of its own at 0x163000000),
nullguard2's 20 bytes at 0x14252e880, fl26nullguard.lua's 23 bytes at 0x14252e800, and
nullguard3's 23 bytes at 0x14252e820.  The page is zero-filled there by the loader; because it
is beyond the file's raw data it cannot be confirmed from the exe on disk, so pass 1 confirms
it in the live process instead and refuses to write if anything else has claimed it.  The stub
is 18 bytes, taking 0x14252e840..0x14252e851.  All-or-nothing: every target byte is checked
first and nothing is written unless all of them match.

Install:
  1. copy this file to <game>\SiderAddons\modules\fl26nullguard4.lua
  2. add to sider.ini, in the [modules] list:   lua.module = "fl26nullguard4.lua"
     -- after fl26nullguard3.lua, so the trampolines are written in a fixed order
  3. start the game and read sider.log

The exe has no ASLR (DllCharacteristics 0x8120), so the addresses are absolute and stable.
The applier checks that anyway by verifying the bytes.
--]]

local m = {}

local patches = {
  {va=0x14252e840, old="000000000000000000000000000000000000",
   new="4885d2740a48895c2420e9863804ff31c0c3",
   why="null-guard trampoline: test rdx,rdx; jz null_ret; mov [rsp+0x20],rbx; jmp 0x1415720d5; null_ret: xor eax,eax; ret"},
  {va=0x1415720d0, old="48895c2420", new="e96bc7fb00",
   why="redirect fn 0x1415720d0 prologue (5 bytes) to the trampoline at 0x14252e840 (jmp rel32)"},
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
  log(string.format("fl26caps: set '%s', %d patches, verifying", "nullguard4", n))

  -- pass 1: read only.  Nothing is written until every site has been confirmed.
  local bad = 0
  for i = 1, n do
    local p = patches[i]
    local want = unhex(p.old)
    local cur = memory.read(p.va, #want)
    if cur ~= want then
      log(string.format("fl26caps: MISMATCH at 0x%x: found %s, expected %s  [%s]",
                        p.va, tohex(cur), p.old, p.why))
      bad = bad + 1
    end
  end
  if bad > 0 then
    log(string.format("fl26caps: ABORTED, %d of %d sites did not match. "
                      .. "Nothing was written; the game is unmodified. "
                      .. "If site 1 is the mismatch, another module has taken that padding.",
                      bad, n))
    return
  end

  -- pass 2: write, then read back, because a write into the code section can silently fail
  -- if the page is not writable for us.  The trampoline goes first, so the jump target
  -- exists before the jump does.
  local written, failed = 0, 0
  for i = 1, n do
    local p = patches[i]
    local new = unhex(p.new)
    memory.write(p.va, new)
    local back = memory.read(p.va, #new)
    if back == new then
      written = written + 1
      log(string.format("fl26caps: [%d/%d] 0x%x %s -> %s  %s",
                        i, n, p.va, p.old, p.new, p.why))
    else
      failed = failed + 1
      log(string.format("fl26caps: WRITE FAILED at 0x%x (%s) -- read back %s",
                        p.va, p.why, tohex(back)))
    end
  end

  if failed == 0 then
    log(string.format("fl26caps: applied all %d patches -- %s", written,
                      "nullguard4: null-check at 0x1415720d0"))
  else
    log(string.format("fl26caps: PARTIAL: %d written, %d failed. The game is now in an "
                      .. "inconsistent state -- quit and report the addresses above.",
                      written, failed))
  end
end

return m
end)()

-- ==== fl26nullguard5.lua ====
local part3 = (function()
--[[
fl26caps -- runtime patch applier (nullguard5 set).

Defensive fix, NOT a capacity patch.  Written by hand, like the other nullguards, because it
installs a code-cave trampoline rather than resizing a field; every byte below was computed
against the exe and disassembled back before being written here.

Set:     nullguard5
Summary: null-check at 0x1414c66c0, 2 patches (trampoline + entry redirect)

The bug.  Minidump thread 3032, c0000005 at 0x1414c674d,
a read of 0x0000000000000004.  0x1414c66c0 is a jump-table field getter --
get(rcx = record, edx = field_id, r8 = out) -- with 108 cases, table at 0x1414c6cdc.  Entry 0
of that table is 0x1414c674d:

    1414c674d  mov    eax, dword ptr [rcx + 4]         <-- rcx = 0, so this reads 0x4
    1414c6750  shr    eax, 0x1c
    1414c6753  mov    dword ptr [r8], eax

A null record plus the field's own offset, not a corrupted pointer (rax 0, rcx 0 in the dump).

Where the null comes from.  The caller is 0x141500ef0 (AI squad/lineup assignment), return
address 0x14150267d.  It does NOT come from a pointer lookup that failed; it comes from an
*index* sentinel used without a range check:

    1415024d5  call   0x141503cc0      ; "find next unassigned candidate"
    1415024da  mov    r15d, eax        ; 0xff when the candidate vector is empty
    ...
    141502649  mov    edx, r15d        ; <-- no range test
    14150264c  mov    r14, qword ptr [rbp - 0x68]
    141502650  mov    rcx, r14
    141502653  call   0x141502e60
    141502658  mov    rsi, rax
    14150265b  mov    edx, r15d
    14150265e  mov    rcx, r14
    141502661  call   0x141502e60      ; returns 0 for index >= 0x28
    141502666  mov    dword ptr [rsp + 0x68], 0xc
    14150266e  lea    r8, [rsp + 0x68]
    141502673  xor    edx, edx         ; field 0
    141502675  mov    rcx, rax         ; <-- 0
    141502678  call   0x1414c66c0      ; crash

0x141503cc0 returns 0xff for "nothing found": at 0x141503cd9 when the vector is empty
(begin == end) and at 0x141503d6a when the cursor runs off the end.  The dump's r15 = 0xff is
exactly that value.  0x141502e60 then rejects it -- `cmp edx, 0x28; jge 0x141502e8e;
xor eax, eax; ret` -- and returns null for any index past the 40-slot scratch array that
0x141504580 allocates fresh for this pass (`mov edx, 0x17c; call 0x14159eff8` at 0x141504604;
0x17c is the player-record stride, 40 slots of it).

That the sentinel is expected here is not a guess.  0x141503cc0 range-checks the same value
against the same bound inside its own body (`cmp r8d, 0x28; jae` at 0x141503d33), and further
down the very same loop the game checks it properly before marking a slot taken:

    1415027ee  cmp    r15d, 0xff
    1415027f5  je     0x141502809                      <-- do not mark the slot
    1415027f7  cmp    r15d, 0x28
    1415027fb  jae    0x141502809
    141502804  mov    byte ptr [rax + rcx + 0x34], 1

That check is downstream of three unguarded dereferences of the same null (0x141502678,
0x14150271e, and 0x14150273a via 0x141502eb0), so on the 0xff path the function faults before
it can reach its own handling.  Shipped bug: none of the eight functions on the crash chain --
0x141500ef0, 0x141504580, 0x1414c66c0, 0x141502e60, 0x141503cc0, 0x141505be0, 0x141571ec0,
0x140f13e90 -- carries a single byte from any fl26caps set.

Why the guard is here and not at 0x141502649.  Redirecting 0x141502649 to one of the loop's
own bail targets (0x14150281c / 0x141502822) was considered and rejected.  Those targets are
reached only from 0x1415026e6 and 0x1415026c5, where ebx has already been reloaded from
[rsp+0x40]; the edge 0x1415024e7 -> 0x141502649 does not reload it, and rsi is reused as a
candidate index at 0x141502507, so esi, ebx, rdx and r15 have different liveness on the two
predecessor edges.  `inc ebx` at 0x14150282c drives the outer loop bound (`cmp ebx, 0xb`), so
a stale ebx there could mis-terminate it.  Jumping into the middle of that function is not
provably safe, and a season limping on a half-updated slot table is worse than a crash.

The fix instead gives the getter the behaviour it already has for a bad request.  Its own
out-of-range path is at 0x1414c6cd8:

    1414c6cd8  xor    al, al
    1414c6cda  ret

-- return false, leave the caller's out buffer untouched.  Every caller already handles that,
because it is the path taken whenever edx > 0x6b.  The guard routes a null rcx to the exact
same two instructions, so a null record reads as "field not available" rather than faulting.
At the three sites in 0x141500ef0 the out slot keeps the 0xc the caller wrote immediately
before the call (0x141502666, 0x14150270c, 0x14150272b), the slot record at [rbp+rbx*4+0x50]
keeps the {0xff, 0xd, 0} the prologue loop at 0x141500f60 initialised it with, and the loop
then falls into the game's own 0xff handling at 0x1415027ee.

Scope.  0x1414c66c0 has 890 call sites, so this guard is broader than nullguard4's.  That
breadth is deliberate: the change is not new behaviour, it is an existing failure path reached
by one more condition, and a null rcx at this function is unconditionally a fault today.  On
any run that does not currently crash here the `je` is never taken and not one byte of
observable state differs.  The cost is that a future null at this getter will be swallowed
instead of dumping, which is the trade this module makes.

The fix: the first nine bytes of 0x1414c66c0 -- `cmp edx, 0x6b` (3) and `ja 0x1414c6cd8` (6),
both whole instructions -- are replaced by a jump to a trampoline plus four nops.  The
trampoline tests rcx, sends null to 0x1414c6cd8, then runs the relocated bounds check and
jumps back to 0x1414c66c9 (`mov eax, edx`), an instruction boundary.  `test rcx, rcx` runs
before the relocated `cmp edx, 0x6b`, so the flags the `ja` reads are the ones it expects.
Nothing jumps into 0x1414c66c0+3..+8: every one of the 108 jump-table targets is >= 0x1414c66df.
The function is a leaf with no prologue, so no unwind data is disturbed.

The trampoline sits at 0x14252e860, in the tail of the last page of .trace: past the raw data
the file ends at (0x14252e800), before .rdata begins (0x14252f000), and after every cave range
this project already uses -- the caps date-spread stub and table at 0x14252e690..0x14252e7e6
(until 0.1.7; since then on a page of its own at 0x163000000),
nullguard2's 20 bytes at 0x14252e880, fl26nullguard.lua's 23 bytes at 0x14252e800, nullguard3's
23 bytes at 0x14252e820, and nullguard4's 18 bytes at 0x14252e840..0x14252e851.  The page is
zero-filled there by the loader; because it is beyond the file's raw data it cannot be confirmed
from the exe on disk, so pass 1 confirms it in the live process instead and refuses to write if
anything else has claimed it.  The stub is 23 bytes, taking 0x14252e860..0x14252e876.
All-or-nothing: every target byte is checked first and nothing is written unless all match.

Install:
  1. copy this file to <game>\SiderAddons\modules\fl26nullguard5.lua
  2. add to sider.ini, in the [modules] list:   lua.module = "fl26nullguard5.lua"
     -- after fl26nullguard4.lua if that one is ever enabled, so the trampolines are written
     in a fixed order; the two caves do not overlap either way
  3. start the game and read sider.log

The exe has no ASLR (DllCharacteristics 0x8120), so the addresses are absolute and stable.
The applier checks that anyway by verifying the bytes.
--]]

local m = {}

local patches = {
  {va=0x14252e860, old="0000000000000000000000000000000000000000000000",
   new="4885c90f846f84f9fe83fa6b0f876684f9fee9527ef9fe",
   why="null-guard trampoline: test rcx,rcx; je 0x1414c6cd8; cmp edx,0x6b; ja 0x1414c6cd8; jmp 0x1414c66c9"},
  {va=0x1414c66c0, old="83fa6b0f870f060000", new="e99b81060190909090",
   why="redirect fn 0x1414c66c0 entry (9 bytes: cmp+ja) to the trampoline at 0x14252e860 (jmp rel32 + 4 nop)"},
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
  log(string.format("fl26caps: set '%s', %d patches, verifying", "nullguard5", n))

  -- pass 1: read only.  Nothing is written until every site has been confirmed.
  local bad = 0
  for i = 1, n do
    local p = patches[i]
    local want = unhex(p.old)
    local cur = memory.read(p.va, #want)
    if cur ~= want then
      log(string.format("fl26caps: MISMATCH at 0x%x: found %s, expected %s  [%s]",
                        p.va, tohex(cur), p.old, p.why))
      bad = bad + 1
    end
  end
  if bad > 0 then
    log(string.format("fl26caps: ABORTED, %d of %d sites did not match. "
                      .. "Nothing was written; the game is unmodified. "
                      .. "If site 1 is the mismatch, another module has taken that padding.",
                      bad, n))
    return
  end

  -- pass 2: write, then read back, because a write into the code section can silently fail
  -- if the page is not writable for us.  The trampoline goes first, so the jump target
  -- exists before the jump does.
  local written, failed = 0, 0
  for i = 1, n do
    local p = patches[i]
    local new = unhex(p.new)
    memory.write(p.va, new)
    local back = memory.read(p.va, #new)
    if back == new then
      written = written + 1
      log(string.format("fl26caps: [%d/%d] 0x%x %s -> %s  %s",
                        i, n, p.va, p.old, p.new, p.why))
    else
      failed = failed + 1
      log(string.format("fl26caps: WRITE FAILED at 0x%x (%s) -- read back %s",
                        p.va, p.why, tohex(back)))
    end
  end

  if failed == 0 then
    log(string.format("fl26caps: applied all %d patches -- %s", written,
                      "nullguard5: null-check at 0x1414c66c0"))
  else
    log(string.format("fl26caps: PARTIAL: %d written, %d failed. The game is now in an "
                      .. "inconsistent state -- quit and report the addresses above.",
                      written, failed))
  end
end

return m
end)()

-- ==== fl26nullguard7.lua ====
local part4 = (function()
--[[
fl26caps -- runtime patch applier (nullguard7 set).

Defensive fix, NOT a capacity patch.  Like the other nullguards it installs a code-cave
trampoline rather than resizing a field; every byte below was computed against the exe and
disassembled back before being written here.

Set:     nullguard7
Summary: empty-vector check at 0x14128a3a3, 2 patches (trampoline + site redirect)

The bug: the board meeting at the start of a Master League season looks a club up in a
table of (handle, value) pairs and pre-loads the first pair before checking that the table
has anything in it.

    14128a3a3  mov   rax, qword ptr [rbp - 0x50]   ; begin
    14128a3a7  mov   r8d, esi
    14128a3aa  mov   r9d, dword ptr [rax + 4]      <-- faults when begin is null
    14128a3ae  mov   ebx, dword ptr [rsp + 0x40]
    14128a3b2  mov   r11, qword ptr [rbp - 0x48]   ; end
    14128a3b6  cmp   rax, r11
    14128a3b9  je    0x14128a411                   <-- the empty case the function already has

The emptiness test is two instructions too late.  On 17 September 2026 a 39-league season
died here 28 confirmations into the board meeting, dump FL_2026.exe.3756.dmp: rax = 0, the
access violation reading address 4.  Nothing is corrupt -- the table is simply empty.

The function's own answer for "this club is not in the table" is 0x14128a411, `or edi, -1`,
so the fix takes that branch.  The empty path still loads ebx from [rsp+0x40], which the
code after the loop reads back at 0x14128a427.

Eleven bytes at 0x14128a3a3 (the two moves plus the faulting load) are replaced by a jump
to a trampoline that performs both moves, tests the pointer, and either continues into the
original loop at 0x14128a3ae or jumps to the not-found branch.  Six nops pad the rest; the
bytes left behind are unreachable and deliberately untouched.

The trampoline sits at 0x14252e8a0, in the zero-filled tail of the last page of .trace,
after nullguard2's 20 bytes at 0x14252e880 and well before .rdata begins at 0x14252f000.

Install:
  1. copy this file to <game>\SiderAddons\modules\fl26nullguard7.lua
  2. add to sider.ini, in the lua.module list:   lua.module = "fl26nullguard7.lua"
     -- after fl26nullguard.lua, so the trampolines are written in a fixed order
  3. start the game and read sider.log

The exe has no ASLR (DllCharacteristics 0x8120), so the addresses are absolute and stable.
The applier checks that anyway by verifying the bytes.
--]]

local m = {}

local patches = {
  {va=0x14252e8a0, old="000000000000000000000000000000000000000000000000000000000000",
   new="488b45b0448bc64885c07409448b4804e9f9bad5fe8b5c2440e953bbd5fe",
   why="squad-table null-guard trampoline: mov rax,[rbp-0x50]; mov r8d,esi; test rax,rax; "
    .. "jz empty; mov r9d,[rax+4]; jmp 0x14128a3ae; empty: mov ebx,[rsp+0x40]; jmp 0x14128a411"},
  {va=0x14128a3a3, old="488b45b0448bc6448b4804", new="e9f8442a01909090909090",
   why="redirect the unchecked first-element load at 0x14128a3a3 to the trampoline (jmp rel32 + 6 nop)"},
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
  log(string.format("fl26caps: set '%s', %d patches, verifying", "nullguard7", n))

  -- pass 1: read only.  Nothing is written until every site has been confirmed.
  local bad = 0
  for i = 1, n do
    local p = patches[i]
    local want = unhex(p.old)
    local cur = memory.read(p.va, #want)
    if cur ~= want then
      log(string.format("fl26caps: MISMATCH at 0x%x: found %s, expected %s  [%s]",
                        p.va, tohex(cur), p.old, p.why))
      bad = bad + 1
    end
  end
  if bad > 0 then
    log(string.format("fl26caps: ABORTED, %d of %d sites did not match. "
                      .. "Nothing was written; the game is unmodified. "
                      .. "If site 1 is the mismatch, another module has taken that padding.",
                      bad, n))
    return
  end

  -- pass 2: write, then read back, because a write into the code section can silently fail
  -- if the page is not writable for us.  The trampoline goes first, so the jump target
  -- exists before the jump does.
  local written, failed = 0, 0
  for i = 1, n do
    local p = patches[i]
    local new = unhex(p.new)
    memory.write(p.va, new)
    local back = memory.read(p.va, #new)
    if back == new then
      written = written + 1
      log(string.format("fl26caps: [%d/%d] 0x%x %s -> %s  %s",
                        i, n, p.va, p.old, p.new, p.why))
    else
      failed = failed + 1
      log(string.format("fl26caps: WRITE FAILED at 0x%x (%s) -- read back %s",
                        p.va, p.why, tohex(back)))
    end
  end

  if failed == 0 then
    log(string.format("fl26caps: applied all %d patches -- %s", written,
                      "nullguard7: empty squad table guarded at 0x14128a3a3"))
  else
    log(string.format("fl26caps: PARTIAL: %d written, %d failed. The game is now in an "
                      .. "inconsistent state -- quit and report the addresses above.",
                      written, failed))
  end
end

return m
end)()

-- ==== fl26nullguard8.lua ====
local part5 = (function()
--[[
fl26caps -- runtime patch applier (nullguard8 set).

Defensive fix, NOT a capacity patch.  Like the other nullguards it installs a code-cave
trampoline rather than resizing a field; every byte below was computed against the exe and
disassembled back before being written here.

Set:     nullguard8
Summary: negative-position guard at 0x1413236e1, 2 patches (trampoline + site redirect)

The bug: a league position is used as an array index without being checked.
0x1413235a0 walks the twenty standings entries of a competition looking for a club, and
when it finds one it takes the entry's `+4` field -- the club's position in the table --
and indexes a table with it:

    1413235ca  mov    dword ptr [rbp + 0x77], 3        ; the default
    14132366f  mov    dword ptr [rbp + 0x77], r12d     ; = 0 before the loop
    141323682  <loop over r13d = 0x14 = twenty entries>
    1413236b2  test   ecx, 0xffffc000                  ; packed club id, low 14 bits ignored
    1413236c3  mov    eax, dword ptr [r14 + 4]         ; found: the position field
    1413236c7  mov    dword ptr [rbp + 0x77], eax
    1413236e1  mov    eax, dword ptr [rbp + 0x77]
    1413236e4  sub    ecx, dword ptr [r8 + rax*4]      <-- faults

A standings pair that has never been filled in carries -1 in that field, and -1 is exactly
what the read at +4 returns for a club whose competition has produced no results yet.  On
17 September 2026 a fresh 39-league world died here on day 360 of its first season, fault
0x13236e4: rax = rcx = rdx = 0xffffffff, an access violation reading 0x5428e52dc, which is
0x1428e52e0 + 0xffffffff * 4 -- the element one before the start of the table.

The guard does not invent a position.  It skips the subtraction when the index is negative,
which leaves ecx holding the value it was given three instructions earlier (`mov ecx, edx`
at 0x1413236df), i.e. the same result as subtracting zero: an unranked club contributes no
adjustment.  The code immediately after the site (`cmp edx, 5` / `cmp ecx, edx`) reads both
registers and is unaffected by taking the branch.

Seven bytes at 0x1413236e1 -- the load of the position plus the faulting subtraction -- are
replaced by a jump to a trampoline that does both with a sign test between them.  Two nops
pad the rest.

The trampoline sits at 0x14252e8c0.  .trace holds 0x252d800 bytes of virtual data and so
ends at 0x14252e800; everything from there to where .rdata begins at 0x14252f000 is the
zero-filled tail of the last page.  Reading those addresses out of the exe *file* shows
non-zero bytes, because the file offset runs on into .rdata's raw data -- the running
process is the authority there, and the applier verifies the zeros before writing.
nullguard5, 2 and 7 hold 0x14252e860, 0x14252e880 and 0x14252e8a0; this is the next slot.

Install:
  1. copy this file to <game>\SiderAddons\modules\fl26nullguard8.lua
  2. add to sider.ini, in the lua.module list:   lua.module = "fl26nullguard8.lua"
     -- after fl26nullguard7.lua, so the trampolines are written in a fixed order
  3. start the game and read sider.log

The exe has no ASLR (DllCharacteristics 0x8120), so the addresses are absolute and stable.
The applier checks that anyway by verifying the bytes.
--]]

local m = {}

local patches = {
  {va=0x14252e8c0, old="00000000000000000000000000000000",
   new="8b457785c07c04412b0c80e9184edffe",
   why="standings-position guard trampoline: mov eax,[rbp+0x77]; test eax,eax; "
    .. "jl skip; sub ecx,[r8+rax*4]; skip: jmp 0x1413236e8"},
  {va=0x1413236e1, old="8b4577412b0c80", new="e9dab120019090",
   why="redirect the unchecked position index at 0x1413236e1 to the trampoline (jmp rel32 + 2 nop)"},
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
  log(string.format("fl26caps: set '%s', %d patches, verifying", "nullguard8", n))

  -- pass 1: read only.  Nothing is written until every site has been confirmed.
  local bad = 0
  for i = 1, n do
    local p = patches[i]
    local want = unhex(p.old)
    local cur = memory.read(p.va, #want)
    if cur ~= want then
      log(string.format("fl26caps: MISMATCH at 0x%x: found %s, expected %s  [%s]",
                        p.va, tohex(cur), p.old, p.why))
      bad = bad + 1
    end
  end
  if bad > 0 then
    log(string.format("fl26caps: ABORTED, %d of %d sites did not match. "
                      .. "Nothing was written; the game is unmodified. "
                      .. "If site 1 is the mismatch, another module has taken that padding.",
                      bad, n))
    return
  end

  -- pass 2: write, then read back, because a write into the code section can silently fail
  -- if the page is not writable for us.  The trampoline goes first, so the jump target
  -- exists before the jump does.
  local written, failed = 0, 0
  for i = 1, n do
    local p = patches[i]
    local new = unhex(p.new)
    memory.write(p.va, new)
    local back = memory.read(p.va, #new)
    if back == new then
      written = written + 1
      log(string.format("fl26caps: [%d/%d] 0x%x %s -> %s  %s",
                        i, n, p.va, p.old, p.new, p.why))
    else
      failed = failed + 1
      log(string.format("fl26caps: WRITE FAILED at 0x%x (%s) -- read back %s",
                        p.va, p.why, tohex(back)))
    end
  end

  if failed == 0 then
    log(string.format("fl26caps: applied all %d patches -- %s", written,
                      "nullguard8: negative standings position guarded at 0x1413236e1"))
  else
    log(string.format("fl26caps: PARTIAL: %d written, %d failed. The game is now in an "
                      .. "inconsistent state -- quit and report the addresses above.",
                      written, failed))
  end
end

return m
end)()

-- ==== fl26nullguard9.lua ====
local part6 = (function()
--[[
fl26caps -- runtime patch applier (nullguard9 set).

Defensive fix, NOT a capacity patch.  Like the other nullguards it installs a code-cave
trampoline rather than resizing a field; every byte below was computed against the exe and
disassembled back before being written here.

Set:     nullguard9
Summary: empty-schedule guard at 0x140cd6a18, 2 patches (trampoline + site redirect)

The bug: a lookup that finds nothing is used as if it had found something.
0x140cd69cc builds the name "d_schedule_" and hands it to 0x14150d620, which is expected
to fill two out-parameters: an index at [rsp+0x40] and the list it indexes at [rsp+0x48].
The code checks the function's RETURN value and has its own exit for "not found" --
and then indexes the list anyway, without ever checking the list pointer:

    140cd69cc  lea    rdx, [rip + 0x1a5914d]     ; "d_schedule_"
    140cd69f0  call   0x14150d620                ; fills [rsp+0x40] and [rsp+0x48]
    140cd6a07  call   0x140e4cd10
    140cd6a0f  test   rax, rax
    140cd6a12  je     0x140cd6ead                ; the game's own "nothing here" path
    140cd6a18  mov    edx, dword ptr [rsp + 0x40]    ; the index  -- 0
    140cd6a1c  mov    rcx, qword ptr [rsp + 0x48]    ; the list   -- NULL
    140cd6a21  movzx  edx, word ptr [rcx + rdx*4]    <-- faults

On 22 September 2026 a season created from the Master League menu died here during the
board meeting, fault offset 0xcd6a21: rcx = rdx = 0, an access violation reading address
zero.  The world had no schedule entry under that name, so the lookup returned empty.

The guard invents nothing.  When the list pointer is null it takes the branch the game
already has for this case, 0x140cd6ead -- the same place the function goes when the
preceding lookup comes back empty.  When the pointer is real the original three
instructions run unchanged.

Thirteen bytes at 0x140cd6a18 -- the two loads plus the faulting index -- are replaced by
a jump to a trampoline that does all three with a null test between them.  Eight nops pad
the rest.

The trampoline sits at 0x14252e8e0.  .trace holds 0x252d800 bytes of virtual data and so
ends at 0x14252e800; everything from there to where .rdata begins at 0x14252f000 is the
zero-filled tail of the last page.  Reading those addresses out of the exe *file* shows
non-zero bytes, because the file offset runs on into .rdata's raw data -- the running
process is the authority there, and the applier verifies the zeros before writing.
nullguard5, 2, 7 and 8 hold 0x14252e860 .. 0x14252e8c0; this is the next slot.

Install:
  1. copy this file to <game>\SiderAddons\modules\fl26nullguard9.lua
  2. add to sider.ini, in the lua.module list:   lua.module = "fl26nullguard9.lua"
     -- after fl26nullguard8.lua, so the trampolines are written in a fixed order
  3. start the game and read sider.log

The exe has no ASLR (DllCharacteristics 0x8120), so the addresses are absolute and stable.
The applier checks that anyway by verifying the bytes.
--]]

local m = {}

local patches = {
  {va=0x14252e8e0,
   old="0000000000000000000000000000000000000000000000000000000000000000",
   new="8b542440488b4c24484885c974090fb71491e92e817afee9b1857afe00000000",
   why="empty-schedule guard trampoline: mov edx,[rsp+0x40]; mov rcx,[rsp+0x48]; "
    .. "test rcx,rcx; je out; movzx edx,word [rcx+rdx*4]; jmp 0x140cd6a25; "
    .. "out: jmp 0x140cd6ead"},
  {va=0x140cd6a18, old="8b542440488b4c24480fb71491",
   new="e9c37e85019090909090909090",
   why="redirect the unchecked schedule read at 0x140cd6a18 to the trampoline "
    .. "(jmp rel32 + 8 nop)"},
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
  log(string.format("fl26caps: set '%s', %d patches, verifying", "nullguard9", n))

  -- pass 1: read only.  Nothing is written until every site has been confirmed.
  local bad = 0
  for i = 1, n do
    local p = patches[i]
    local want = unhex(p.old)
    local cur = memory.read(p.va, #want)
    if cur ~= want then
      log(string.format("fl26caps: MISMATCH at 0x%x: found %s, expected %s  [%s]",
                        p.va, tohex(cur), p.old, p.why))
      bad = bad + 1
    end
  end
  if bad > 0 then
    log(string.format("fl26caps: ABORTED, %d of %d sites did not match. "
                      .. "Nothing was written; the game is unmodified. "
                      .. "If site 1 is the mismatch, another module has taken that padding.",
                      bad, n))
    return
  end

  -- pass 2: write, then read back, because a write into the code section can silently fail
  -- if the page is not writable for us.  The trampoline goes first, so the jump target
  -- exists before the jump does.
  local written, failed = 0, 0
  for i = 1, n do
    local p = patches[i]
    local new = unhex(p.new)
    memory.write(p.va, new)
    local back = memory.read(p.va, #new)
    if back == new then
      written = written + 1
      log(string.format("fl26caps: [%d/%d] 0x%x %s -> %s  %s",
                        i, n, p.va, p.old, p.new, p.why))
    else
      failed = failed + 1
      log(string.format("fl26caps: WRITE FAILED at 0x%x (%s) -- read back %s",
                        p.va, p.why, tohex(back)))
    end
  end

  if failed == 0 then
    log(string.format("fl26caps: applied all %d patches -- %s", written,
                      "nullguard9: empty schedule list guarded at 0x140cd6a18"))
  else
    log(string.format("fl26caps: PARTIAL: %d written, %d failed. The game is now in an "
                      .. "inconsistent state -- quit and report the addresses above.",
                      written, failed))
  end
end

return m
end)()

-- ==== fl26nullguard10.lua ====
local part7 = (function()
--[[
fl26caps -- runtime patch applier (nullguard10 set).

Defensive fix, NOT a capacity patch.  Two jumps get a new target; no trampoline, no spare
code bytes used.

Set:     nullguard10
Summary: endless league-link walk in 0x140caee00 stopped, 2 patches (two jmp targets)

The bug: a loop that never ends.  0x140caee00 goes over every regulation and, for each one,
walks a chain of linked leagues.  It starts from the league named by +0x78 of the record;
when that is 0xffff it starts from +0x7c instead (the league above).  It then follows +0x78
from league to league.  The walk is meant to stop at 0xffff, but the test for 0xffff sits at
the head of the loop, 0x140cafad0, which is also the place that falls back to +0x7c:

    140cafad0  cmp    bx, ax                    ; next league == 0xffff ?
    140cafad3  jne    0x140cafae9
    140cafad5  movzx  ebx, word ptr [r12 + 0x7c]  ; yes: take the league above ... again
    140cafae0  cmp    bx, ax
    140cafae3  je     0x140cafdb1               ; only a missing league above ends it

So for a league whose +0x78 is 0xffff and whose +0x7c is set (a second division pointing
at the first), the walk goes: league above, its +0x78 is 0xffff, back to the league above,
and so on forever.  Two of the three ways back to the head do this:

    140cafc35  jmp 0x140cafac0      ; after "movzx ebx, word [rax+0x78]"
    140cafdac  jmp 0x140cafac5      ; after "movzx ebx, word [rsi+0x78]" and a free

The third, 0x140cafc5f .. 0x140cafc76, already does the right thing: it stores the next
league, ends the walk on 0xffff (je 0x140cafdb1) and only otherwise goes back to 0x140cafad0.
Only a finished competition leads there, so the walk ends as long as the league above has
finished its season.

The two loops differ in what they cost.  One turn of the second one pushes a 32-byte entry
plus a copy of the league's date list into a vector.  On 30 September 2026 a Korea career
(K League 2 pointing up at K League 1, February-December seasons) died on day 30 of its
second year, while the Europa League play-off was being drawn: an out-of-memory write inside
the 12-byte copy 0x140affed0, fault offset 0xaffe1b.  Loading the save from day 356 and
playing on repeated it -- the game grew from 3.4 to more than 8 GB in under a minute on the
same day.  RIP samples during the growth sit in the "is this
competition finished" check 0x14151c300 / 0x14150b860, the regulation lookup 0x1414bb000 and
that copy.  Day 30 of the first year, before any European draw, was fine.

The fix sends both jumps to the existing correct tail instead of the loop head:

    140cafc35  jmp 0x140cafc63      ; store bx; 0xffff -> end; else jmp 0x140cafad0
    140cafdac  jmp 0x140cafc68      ; (bx is stored already)   same test

rbx is callee-saved, so bx survives the free before 0x140cafdac.  Leagues linked by +0x78
(2 -> 3 -> 4 in England) are walked exactly as before; only the step back to +0x7c after
the end of a chain is gone.  The first step from +0x7c (at the start of a walk) is kept.

Verified 2026-09-30 on the same save: with this module day 30 passed at 3.3 GB with no growth,
and on day 41 both Korean leagues joined their new season (33 and 18 matchdays placed).

Install:
  1. copy this file to <game>\SiderAddons\modules\fl26nullguard10.lua
  2. add to sider.ini, in the lua.module list:   lua.module = "fl26nullguard10.lua"
     -- after fl26nullguard9.lua
  3. start the game and read sider.log

The exe has no ASLR (DllCharacteristics 0x8120), so the addresses are absolute and stable.
The applier checks that anyway by verifying the bytes.
--]]

local m = {}

local patches = {
  {va=0x140cafc35, old="e986feffff", new="e929000000",
   why="league-link walk, plain step: jmp 0x140cafac0 -> jmp 0x140cafc63 (0xffff ends the walk)"},
  {va=0x140cafdac, old="e914fdffff", new="e9b7feffff",
   why="league-link walk, dated step: jmp 0x140cafac5 -> jmp 0x140cafc68 (0xffff ends the walk)"},
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
  log(string.format("fl26caps: set '%s', %d patches, verifying", "nullguard10", n))

  -- pass 1: read only.  Nothing is written until every site has been confirmed.
  local bad = 0
  for i = 1, n do
    local p = patches[i]
    local want = unhex(p.old)
    local cur = memory.read(p.va, #want)
    if cur ~= want then
      log(string.format("fl26caps: MISMATCH at 0x%x: found %s, expected %s  [%s]",
                        p.va, tohex(cur), p.old, p.why))
      bad = bad + 1
    end
  end
  if bad > 0 then
    log(string.format("fl26caps: ABORTED, %d of %d sites did not match. "
                      .. "Nothing was written; the game is unmodified. "
                      .. "If site 1 is the mismatch, another module has taken that padding.",
                      bad, n))
    return
  end

  -- pass 2: write, then read back, because a write into the code section can silently fail
  -- if the page is not writable for us.
  local written, failed = 0, 0
  for i = 1, n do
    local p = patches[i]
    local new = unhex(p.new)
    memory.write(p.va, new)
    local back = memory.read(p.va, #new)
    if back == new then
      written = written + 1
      log(string.format("fl26caps: [%d/%d] 0x%x %s -> %s  %s",
                        i, n, p.va, p.old, p.new, p.why))
    else
      failed = failed + 1
      log(string.format("fl26caps: WRITE FAILED at 0x%x (%s) -- read back %s",
                        p.va, p.why, tohex(back)))
    end
  end

  if failed == 0 then
    log(string.format("fl26caps: applied all %d patches -- %s", written,
                      "nullguard10: league-link walk ends at the last league (0x140cafc35, 0x140cafdac)"))
  else
    log(string.format("fl26caps: PARTIAL: %d written, %d failed. The game is now in an "
                      .. "inconsistent state -- quit and report the addresses above.",
                      written, failed))
  end
end

return m
end)()

-- ==== fl26nullguard11.lua ====
local part8 = (function()
--[[
fl26caps -- runtime patch applier (nullguard11 set).

Defensive fix, NOT a capacity patch.  One block of code is rewritten in place; no
trampoline, no cave, no spare bytes anywhere else.

Set:     nullguard11
Summary: a squad entry whose player no longer exists is skipped when a new Master League
         is set up, instead of crashing the game (1 patch, 67 bytes at 0x140f3258e)

The bug.  0x140f32470 runs once when a new Master League is created.  It walks every team of
the Edit block (count at +0xd0bcec, 1,939 on 14.10.2026), fetches the team's squad list with
0x140f2b670 (32-byte records, the player handle in the first qword, flags in the dword at
+0x1c), looks each player up with 0x1414bc9a0(edit block, handle) and sets captain / kicker
bits in the player record at +0x148 according to the flags:

    140f32589  call   0x1414bc9a0            ; rax = player record, or 0 when he is not there
    140f3258e  movups xmm0, [rsp+0x20]       ; the record's second half
    140f32593  psrldq xmm0, 0xc
    140f32598  movd   ecx, xmm0              ; ecx = flags (record +0x1c)
    140f3259c  test   ecx, 0xd0
    140f325a2  jbe    0x140f325ae
    140f325a4  or     dword [rax+0x148], 0x20000   <-- writes through rax, never checked
    ...        two more "test / jbe / or dword [rax+0x148], 0x40000"
    140f325d1  add    rbx, 0x20              ; next record

The lookup returns 0 when the handle names a player who is not in Player.bin, and the
first "or" then writes to address 0x148: access violation at 0x140f325a4.  Seen four times on
14.10.2026 (four crash dumps): team index 11 (Aston Villa, team 107), player
213663 -- a player the previous world had and the rebuilt one numbers differently.  The
squad list that still names him is not in the world's tables (all seven, scanned) but in
the game's own saved state, so a player who installs a rebuilt world over an older one
hits it the moment he starts a Master League; the crash in #beta-chat (Tysonche35) is the
same.  The other two writes at 0x140f325b3 / 0x140f325c7 go through the same rax.

The fix rewrites the 67 bytes 0x140f3258e..0x140f325d1 so that the flags are read straight
from the stack (the dword at [rsp+0x2c] is exactly what psrldq/movd produced) and a null
rax skips the whole record:

    140f3258e  mov    ecx, [rsp+0x2c]
    140f32592  test   rax, rax
    140f32595  je     0x140f325d1             ; no such player: next record
    140f32597  test   cl, 0xd0  / je          ; the three flag tests as before
    140f3259c  or     dword [rax+0x148], 0x20000
    140f325a6  test   cl, 0x50  / je
    140f325ab  or     dword [rax+0x148], 0x40000
    140f325b5  test   cl, 0x20  / je ; test cl, 8 / je
    140f325bf  or     dword [rax+0x148], 0x40000
    140f325c9  8 x nop

"jbe" after "test" is "je" (test clears CF), and "test ecx, 0xd0" only looks at the low
byte, so "test cl, 0xd0" is the same test.  xmm0 is not used after this block (the loop head
reloads it), ecx and rdx are rewritten at the next iteration.  Nothing else changes: a team
whose players all exist gets exactly the bits it got before.

Install:
  1. copy this file to <game>\SiderAddons\modules\fl26nullguard11.lua
  2. add to sider.ini, in the lua.module list:   lua.module = "fl26nullguard11.lua"
     -- after fl26nullguard10.lua (or let fl26guards.lua carry it)
  3. start the game and read sider.log

The exe has no ASLR (DllCharacteristics 0x8120), so the addresses are absolute and stable.
The applier checks that anyway by verifying the bytes.
--]]

local m = {}

local patches = {
  {va=0x140f3258e,
   old="0f10442420660f73d80c660f7ec1f7c1d0000000760a81884801000000000200f6c150760a81884801000000000400f6c120760ff6c108760a81884801000000000400",
   new="8b4c242c4885c0743af6c1d0740a81884801000000000200f6c150740a81884801000000000400f6c1207417f6c1087412818848010000000004009090909090909090",
   why="new Master League squad flags: skip a record whose player lookup returned 0 (was: or dword [rax+0x148] with rax=0)"},
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
  log(string.format("fl26caps: set '%s', %d patches, verifying", "nullguard11", n))

  -- pass 1: read only.  Nothing is written until every site has been confirmed.
  local bad = 0
  for i = 1, n do
    local p = patches[i]
    local want = unhex(p.old)
    local cur = memory.read(p.va, #want)
    if cur ~= want then
      log(string.format("fl26caps: MISMATCH at 0x%x: found %s, expected %s  [%s]",
                        p.va, tohex(cur), p.old, p.why))
      bad = bad + 1
    end
  end
  if bad > 0 then
    log(string.format("fl26caps: ABORTED, %d of %d sites did not match. "
                      .. "Nothing was written; the game is unmodified.", bad, n))
    return
  end

  -- pass 2: write, then read back, because a write into the code section can silently fail
  -- if the page is not writable for us.
  local written, failed = 0, 0
  for i = 1, n do
    local p = patches[i]
    local new = unhex(p.new)
    memory.write(p.va, new)
    local back = memory.read(p.va, #new)
    if back == new then
      written = written + 1
      log(string.format("fl26caps: [%d/%d] 0x%x %s  %s", i, n, p.va, "rewritten", p.why))
    else
      failed = failed + 1
      log(string.format("fl26caps: WRITE FAILED at 0x%x (%s) -- read back %s",
                        p.va, p.why, tohex(back)))
    end
  end

  if failed == 0 then
    log(string.format("fl26caps: applied all %d patches -- %s", written,
                      "nullguard11: a missing player is skipped at Master League setup (0x140f3258e)"))
  else
    log(string.format("fl26caps: PARTIAL: %d written, %d failed. The game is now in an "
                      .. "inconsistent state -- quit and report the addresses above.",
                      written, failed))
  end
end

return m
end)()

local PARTS = { { "fl26nullguard", part0 }, { "fl26nullguard2", part1 }, { "fl26nullguard4", part2 }, { "fl26nullguard5", part3 }, { "fl26nullguard7", part4 }, { "fl26nullguard8", part5 }, { "fl26nullguard9", part6 }, { "fl26nullguard10", part7 }, { "fl26nullguard11", part8 } }
local m = {}
function m.init(ctx)
  for _, p in ipairs(PARTS) do
    if type(p[2]) == "table" and p[2].init then
      p[2].init(ctx)          -- no pcall in Sider's Lua (run023, 2026-10-07)
    end
  end
end
return m

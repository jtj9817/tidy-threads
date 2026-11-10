# Filters RimWorld's Player.log down to the mod's own lines (tag, e.g. [Quadrunner]) and error blocks (head + trace).
# Run with LC_ALL=C so length() counts bytes, matching the byte offsets qblog.sh stores.
#
# Variables (-v):
#   start        print only lines that end after this byte offset (default 0)
#   baseline     file of known error signatures; matching blocks are hidden
#   errors_only  1 = drop non-error mod lines
#   mode         "signatures" = print one signature per error block instead of the blocks
#   tag          the mod's log prefix to match, e.g. "[Quadrunner]" (passed by qblog.sh)

BEGIN {
  if (baseline != "") while ((getline s < baseline) > 0) known[s] = 1
}

# Stable identity of an error across runs: thing IDs, coordinates, counts and ref hashes removed.
function sig(line) {
  gsub(/\[Ref [0-9A-F]+\]/, "[Ref]", line)
  gsub(/[0-9]+/, "N", line)
  return substr(line, 1, 60)
}

# Lines that continue the current block rather than start a new one.
function continues(line) {
  return line ~ /^\[Ref [0-9A-F]+\]/ || line ~ /^  at / || line ~ /^--- End of / ||
         line ~ /^Rethrow as / || line ~ /^[A-Za-z0-9_.]+(Exception|Error)(: |$)/
}

function emit(line) {
  if (pos > start) { print line; fflush() }
}

{
  pos += length($0) + 1
  line = $0

  if (inblock && continues(line)) {
    if (match(line, /^\[Ref [0-9A-F]+\]/)) {
      ref = substr(line, 6, RLENGTH - 6)
      if (line ~ /Duplicate stacktrace/) {
        # 1.6 prints each stack trace once; swap the original back in.
        if (show) { emit(line); if (ref in refs) emit(refs[ref]) }
        curref = ""
        next
      }
      curref = ref
      refs[ref] = ""
    } else if (curref != "" && line ~ /^  at /) {
      refs[curref] = refs[curref] (refs[curref] == "" ? "" : "\n") line
    }
    if (show) emit(line)
    next
  }

  inblock = 0
  curref = ""
  qb = index(line, tag) > 0
  err = line !~ /GL_/ && line ~ /[Ee]xception|[Ee]rror|ERROR|[Ff]ailed|[Cc]ould not|[Cc]annot |Missing|not found/
  if (!qb && !err) next

  inblock = 1
  if (mode == "signatures") {
    show = 0
    if (err && pos > start) print sig(line)
    next
  }
  show = (qb || !(sig(line) in known)) && (err || !errors_only)
  if (show) emit(line)
}

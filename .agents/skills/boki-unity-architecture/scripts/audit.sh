#!/usr/bin/env bash
# Structural audit for the Boki Unity project. Needs only git-bash/POSIX tools, no Unity.
# Usage: .agents/skills/boki-unity-architecture/scripts/audit.sh [path-to-Game]
# Exit code 1 if any FAIL is reported; WARN lines are advisory.
set -u

game="${1:-Game}"
assets="$game/Assets"
[ -d "$assets" ] || { echo "No Assets directory at $assets (run from repo root or pass the Game path)"; exit 2; }

fails=0
fail() { echo "FAIL  $*"; fails=$((fails + 1)); }
warn() { echo "WARN  $*"; }
ok()   { echo "ok    $*"; }

# 1. Every asset and folder has a .meta, and every .meta has an asset.
missing=0
while IFS= read -r path; do
  [ -e "$path.meta" ] || { fail "missing .meta for $path (import in the pinned Editor; never invent a GUID)"; missing=1; }
done < <(find "$assets" \( -name '*.cs' -o -name '*.asmdef' -o -name '*.unity' -o -name '*.prefab' \
          -o -name '*.mat' -o -name '*.asset' -o -type d \) -not -name '*.meta' -not -path "$assets" 2>/dev/null)
while IFS= read -r meta; do
  [ -e "${meta%.meta}" ] || { fail "orphan $meta (asset deleted or moved without its pair)"; missing=1; }
done < <(find "$assets" -name '*.meta')
[ "$missing" -eq 0 ] && ok ".meta pairing"

# 2. Duplicate GUIDs across .meta files.
dupes=$(grep -rh '^guid:' --include='*.meta' "$assets" "$game/Packages" 2>/dev/null | sort | uniq -d)
[ -z "$dupes" ] && ok "unique GUIDs" || fail "duplicate GUIDs: $dupes"

# 3. Rules assemblies stay engine-free (deterministic, EditMode-testable).
for dir in $(find "$assets/Scripts" -type d -name Rules); do
  if grep -rn --include='*.cs' -E 'using UnityEngine|using UnityEditor' "$dir" >/dev/null; then
    fail "$dir references UnityEngine/UnityEditor:"; grep -rn --include='*.cs' -E 'using UnityEngine|using UnityEditor' "$dir"
  fi
  for asmdef in "$dir"/*.asmdef; do
    [ -e "$asmdef" ] || { fail "$dir has no .asmdef"; continue; }
    grep -q '"noEngineReferences": *true' "$asmdef" || warn "$asmdef lacks \"noEngineReferences\": true (compiler would not stop engine leaks)"
  done
done

# 4. Each Rules assembly has a matching test assembly.
for asmdef in $(find "$assets/Scripts" -path '*/Rules/*.asmdef'); do
  name=$(basename "$asmdef" .asmdef)
  grep -rlq "\"$name\"" "$assets/Tests" --include='*.asmdef' 2>/dev/null || warn "$name is not referenced by any test assembly"
done

# 5. Runtime code must not reference the Editor namespace outside #if UNITY_EDITOR.
while IFS= read -r file; do
  if grep -q 'UnityEditor' "$file" && ! grep -q '#if UNITY_EDITOR' "$file"; then
    fail "$file uses UnityEditor outside #if UNITY_EDITOR (breaks player builds)"
  fi
done < <(find "$assets/Scripts" -name '*.cs' -not -path '*/Rules/*')

# 6. Banned or suspicious calls in runtime code.
runtime=$(find "$assets/Scripts" -name '*.cs')
check() { # pattern, level, message
  local hits
  hits=$(echo "$runtime" | xargs grep -n -E "$1" 2>/dev/null)
  [ -z "$hits" ] && return
  if [ "$2" = fail ]; then fail "$3"; else warn "$3"; fi
  echo "$hits" | sed 's/^/        /'
}
check 'Input\.(GetAxis|GetKey|GetButton|GetMouse)' fail "legacy Input API (use the Input System; see Architecture 7)"
check 'FindObjectOfType|FindObjectsOfType|FindFirstObjectByType|FindAnyObjectByType|GameObject\.Find\(|FindWithTag' warn "scene-wide Find* call (prefer serialized references; never in Update)"
check 'Resources\.Load' warn "Resources.Load (needs agreement; prefer serialized references)"
check 'Time\.timeScale' warn "Time.timeScale write/read (pause must go through the single pause authority)"
check 'using System\.Linq' warn "System.Linq in runtime code (avoid in per-frame paths)"
check 'SendMessage\(|BroadcastMessage\(' fail "SendMessage/BroadcastMessage (use typed calls or C# events)"
check 'Physics\.(RaycastAll|SphereCastAll|CapsuleCastAll|BoxCastAll|OverlapSphere|OverlapBox|OverlapCapsule)\(' warn "array-returning physics query allocates (use the *NonAlloc variant with a reused buffer)"
check 'MeshCollider' warn "MeshCollider (only when primitives cannot represent the shape)"

# 7. Build settings must list every scene a build or transition can load.
settings="$game/ProjectSettings/EditorBuildSettings.asset"
if [ -f "$settings" ]; then
  for scene in $(find "$assets/Scenes" -name '*.unity' -not -name 'SampleScene.unity' -not -name '*_Test.unity'); do
    rel="${scene#$game/}"
    grep -q "$rel" "$settings" || warn "$rel is not in EditorBuildSettings (Architecture 10 requires every gameplay scene; coordinate: this file is shared)"
  done
fi

# 8. Project settings that the architecture pins.
grep -q 'm_SerializationMode: 2' "$game/ProjectSettings/EditorSettings.asset" 2>/dev/null && ok "Force Text serialization" || fail "Asset Serialization is not Force Text"
grep -q 'm_Mode: Visible Meta Files' "$game/ProjectSettings/VersionControlSettings.asset" 2>/dev/null && ok "Visible Meta Files" || warn "Version Control is not Visible Meta Files"

# 9. Hand-written one-per-milestone Editor clones.
for kind in PlayerBuild SceneBuilder SmokeChecks; do
  n=$(find "$assets/Editor" -name "M*$kind.cs" 2>/dev/null | wc -l | tr -d ' ')
  [ "$n" -gt 2 ] && warn "$n milestone-prefixed *$kind.cs files; extend a shared helper instead of cloning another"
done

echo
[ "$fails" -eq 0 ] && echo "audit: no FAILs" || echo "audit: $fails FAIL(s)"
exit $((fails > 0))

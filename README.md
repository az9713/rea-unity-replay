# Guarded Replay Ledger

Repeating an edit is safer when you first check that it is still aimed at the right object in the right starting state. This tool previews a small set of scene edits and refuses to repeat them when the recorded conditions no longer match.

Suppose an editor script moves an object called Target. Later, another scene contains an object with the same name but a different identity or starting position. Repeating the old instruction without checking can change the wrong thing.

**Example:** The browser example changes selected object positions and on/off state. It checks the saved starting scene before allowing the repeated edits. Change that starting state and the example refuses the old edit, showing why a familiar object name is not enough.

**What you install:** An experimental Unity editor package, plus a tested separate Windows checker.

**Current scope:** The separate C# checker and Windows example were executed. Installation in a real Unity Editor, the editor panel and native Undo behavior remain unverified. Complex scenes, physics, animation and arbitrary scripts are outside this release.

[Live guide](https://az9713.github.io/rea-unity-replay/) · [Download packages](https://github.com/az9713/rea-unity-replay/releases/tag/v0.1.0)

## The before/after change

| Before | With the bounded replay ledger |
|---|---|
| A scene diff shows changed serialized properties | An explicit command stream says what is permitted to change and which object state must precede each command |
| Names or hierarchy paths guess a target | Saved-scene GlobalObjectIds and a captured root closure identify targets; duplicates, missing objects and changed roots are refused |
| An old automation runs against a newer baseline | Whole-checkpoint and per-object hashes must match before mutation |
| Unknown scripts or prefab overrides may produce hidden effects | The initial adapter refuses unreviewed components, prefab instances, play mode and compilation |
| A partial failed batch leaves uncertain state | The core requires rollback and verifies captured fields; failed rollback is explicitly reported |

This adds a constrained transaction contract above existing native Undo, semantic
YAML merge and diff/dependency tools. It does not claim generic scene diff is new.

## Run the verified standalone release

Download `GuardedReplay-0.1.0-cli-win-x64.zip` and extract it. It contains a
self-contained .NET runtime; no Unity or installed .NET runtime is required:

```powershell
./UnityReplay.Cli.exe demo ./demo
./UnityReplay.Cli.exe verify ./demo/manifest.json ./demo/baseline.json ./demo/context.json ./demo ./demo/plan.json
```

The demo uses original synthetic IDs and assets, explicitly labeled **NOT UNITY**.
It executes four real C# commands: move a marker to X=2, scale to 1.5, rename it
`VerifiedMarker`, then deactivate it. The verifier independently hashes actual
fixture asset/meta files before replaying the pure policy model. Its synthetic
importer/dependency labels do not prove native Unity importer behavior.

The UPM package is a separate download: `GuardedReplay-0.1.0-upm.zip`.

## Use the experimental Editor package

Target API: Unity 6; reference source pinned to **6000.6.7f1**. Compatibility with
an actual Editor installation remains to be established.

1. Extract the UPM ZIP. In Unity Package Manager, choose **Add package from disk**
   and select its `package.json`.
2. Create a disposable scene with a top-level empty root and empty child marker.
   Save the scene. Start with Transform-only objects; default primitive colliders
   are deliberately outside the reviewed component set.
3. Open **Tools → REA → Guarded Replay Ledger**, choose the root and capture the
   clean saved baseline. The tool never saves the scene automatically.
4. Choose the child and a position/scale/rotation/name/active command. **Record and
   apply** validates the last checkpoint and adds an ordinary native Undo group.
5. **Preview** computes the full command plan and checks current identities.
6. **Restore captured checkpoint** refuses unrecorded drift. Then **Apply full
   replay** requires the exact captured baseline and checks the resulting fields.
7. Export/import a bounded JSON manifest to inspect or repeat the experiment.

Do not save changed scene bytes between capture and replay: the saved scene itself
is a hashed dependency, so doing so intentionally creates an identity conflict.
Inspector edits outside this window also conflict with the recorded checkpoint.

The window does not accept reflection targets, arbitrary expressions, asset-write
commands, object creation/deletion, reparenting or a global event recorder.

## Guard and invariant contract

- Exactly one captured top-level root, saved scene identity and unique object IDs.
- Exact object membership/parent closure and controlled-field baseline hash.
- Per-command before-object hash; only five allowlisted command kinds.
- Finite local transforms, unit quaternion, positive bounded scales and bounded
  displacement from the original baseline, checked after **every** command.
- Unity version, target platform, serialization mode and package/project-version
  file identities; source asset/meta hashes, importer type and dependency hash.
- Caps: 256 objects, 512 commands/assets, 4 MiB manifest, 32 MiB per hashed file
  and 128 MiB aggregate file budget. Symlink/reparse dependencies are refused.

The initial Unity adapter allows exact Transform, MeshFilter and MeshRenderer
components only. It rejects scripts, missing scripts, colliders, animation,
physics and prefab instances. Asset byte hashing covers `Assets/` scene
dependencies, including the saved scene; package manifest/lock files identify
package context. It does not hash every registry-package file or imported artifact.

Scope is **declared local fields**, not complete object serialization, visuals,
physics, build output or arbitrary Editor state. Other extensions/global callbacks
can have effects outside the captured fields; these are not proven reversible.
Hashes detect drift relative to a manifest; they are not publisher signatures.

## What was actually verified

`artifacts/verification.json` separates three evidence levels:

1. **Real C# core execution:** 20 tests for success, wrong baselines/roots,
   duplicate IDs, hierarchy escapes/cycles, unknown commands/components, NaN and
   Infinity, invalid scale/rotation, intermediate invariant violations, identity
   conflicts, caps, culture/JSON roundtrip, rollback, explicit rollback failure
   and real file tampering.
2. **Compile-only API shim check:** EditorWindow plus the actual shared core build
   with zero warnings/errors against original handwritten signatures. This is
   not compilation against Unity assemblies.
3. **Real published EXE:** the self-contained executable runs the demo and file-
   verified replay. No simulated CLI test is substituted for that execution.

Native GlobalObjectId lifetime, Unity JSON behavior, package import, EditorWindow
interaction, importer/dependency identities, scene serialization and native Undo
remain unverified. The memory-backend transaction tests do not prove native Undo.

Before broader Unity use, manually verify in the disposable scene: capture →
record → preview → restore → replay → one native Undo. Then introduce a manual
transform edit, unsupported component and asset/meta change; each must be refused.

## Reproduce the build

Requires .NET SDK **9.0.320**, Python 3.11+ and PowerShell. No npm or Unity is used:

```powershell
./scripts/test.ps1
./scripts/publish_cli.ps1
python scripts/build.py
```

The scripts direct .NET home, NuGet/cache/temp and build outputs into `.runtime`,
disable ASP.NET certificate generation and pass an explicit NuGet configuration.
They can reuse the portfolio's already-downloaded SDK read-only or a normal `dotnet`
command when used independently. The release CLI itself needs no SDK.

`scripts/fetch_reference.py` can retrieve the pinned Unity reference source for
local inspection into an untracked folder. Its implementation is **not redistributed**:
Unity's Reference-Only License prohibits that. The public source record contains
immutable links and hashes. Our code and compile shims are original MIT work.

The build packages exact UPM files and a hash-verified CLI publish manifest; it
does not copy private/runtime/cache directories by wildcard. All HTML is under
`docs/` for GitHub Pages. See [NOTICE.md](NOTICE.md) for runtime attribution.

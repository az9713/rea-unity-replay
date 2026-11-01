# Guarded replay versus existing scene inspection

The implementation uses documented Unity APIs and original C# code. No binary REA
success, native Editor run or Unity assembly compilation is claimed.

## Existing coverage

| Existing mechanism | Already provided | This project's bounded contribution |
|---|---|---|
| [UnityYAMLMerge](https://docs.unity3d.com/6000.0/Documentation/Manual/SmartMerge.html) | Semantic scene/prefab merging | Explicit replay commands with before-state guards, not another merge tool |
| [Scene Diff](https://voxcub.com/tools/scene-diff.html) | Author describes GameObject/component/property comparison | Command preconditions and declared invariants, rather than generic semantic comparison |
| [PrefabLens](https://github.com/hashiiiii/PrefabLens) | Semantic Unity YAML diff/merge tooling | A small transaction policy shared by an Editor adapter and standalone verifier |
| [SceneXRay](https://andriisviatenko.github.io/SceneXRay/) | Dependency graphs, reference checks, scene-link differences and CI guards | Identity-checked checkpoint restoration and refusal of unsupported command effects |
| Native Undo | Records/restores Editor object changes | Explicit preflight/postcondition checks and rollback verification of declared fields |

The ecosystem search is bounded; no absence of other replay products is inferred.
Generic history/diff/dependency features should not receive novelty credit here.

## Version-pinned public references

UnityCsReference `master` was identified as Unity 7000.0 alpha. To avoid treating
that as stable evidence, this investigation pinned the **6000.6.7f1** tag:
`d0e5e3e00a4f454bc2853234708c3d7c67ebca9a`.

The public [pin manifest](public-source/pin.json) records immutable source URLs
and SHA-256 hashes. Downloaded implementation files remain untracked because the
[Reference-Only License](https://unity.com/legal/licenses/unity-reference-only-license)
permits local reference use while restricting redistribution/modification. No
upstream implementation was copied into our package or shims.

Relevant documented contracts:

- [GlobalObjectId](https://docs.unity.com/en-us/engine/6000.6/script-reference/unityeditor/globalobjectid): saved-scene GUID/local identity, with loaded-scene and persistence caveats. The adapter requires saved, loaded scenes and refuses missing/zero identities.
- [Undo](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/undo): grouping and object snapshots; the adapter wires complete-object registration and group rollback, but that native behavior is not tested here.
- [GetAssetDependencyHash](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityeditor/assetdatabase/getassetdependencyhash): incorporates asset/meta/platform/importer context. The adapter records it alongside actual source/meta SHA-256 identities.

## Proof boundaries

The standalone tests genuinely execute the shared C# planner and transaction
policy. Their backend is an original in-memory object model. It deliberately
injects partial failure, divergent output and failed rollback. These establish
policy behavior, not Unity's native Undo or callbacks.

The Editor adapter is compiled against handwritten signature shims. This catches
syntax/type-flow errors against that limited model; it cannot substitute for
genuine Unity references, package import or a native Editor test.

The self-contained CLI genuinely executes published .NET code and verifies actual
synthetic asset/meta bytes. Its IDs/importer/dependency labels explicitly identify
synthetic data. No installed app, private project, Unity license folder or account
was accessed. Native integration remains a specific outstanding validation step.

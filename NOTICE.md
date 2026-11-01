# Attribution and verification boundaries

The policy engine, command-line app, Unity Editor package, tests, API shims,
synthetic assets and website are original MIT-licensed work.

UnityCsReference was read **for reference only**, pinned to Unity **6000.6.7f1**,
commit `d0e5e3e00a4f454bc2853234708c3d7c67ebca9a`. Its Reference-Only License does not
permit redistribution of its implementation. No Unity reference implementation,
native assembly, installed Editor file or licensed runtime is incorporated or
distributed. Public evidence contains immutable URLs and SHA-256 hashes only.
Downloaded reference files remain untracked in `.runtime`.

`tests/UnityApiStubs.cs` is handwritten compile scaffolding, not Unity source.
Compiling against it checks C# syntax and that small signature model. It does not
prove compilation against genuine Unity assemblies or native Editor behavior.

The Windows CLI release includes Microsoft's self-contained .NET runtime. Its
publisher `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` are included in that ZIP and
retain their respective licenses. The Unity UPM package does not contain .NET or
Unity runtime files. Synthetic fixture identity labels explicitly say NOT UNITY.

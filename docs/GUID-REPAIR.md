# Controller Settings metadata repair

Baseline: `codex-cli/dev@27b8d070c5f8890bd4db636f28a51a01be4777fb`.

The previous controller-settings helper metadata declared the 33-character token `7b17694538454915b4f708c18797f6f01`. The repair retains its first 32 hex characters, `7b17694538454915b4f708c18797f6f0`. No other metadata line or existing GUID is changed.

The old token and the proposed prefix had no serialized references in Assets, ProjectSettings or Packages. The file contributes methods to the partial `FrontierRuntime` class; the scene bootstrap uses `AddComponent<FrontierRuntime>()`. The primary runtime script keeps its original GUID. All other 243 baseline metadata files remain byte-identical. No scene, prefab, resource, protagonist, save or Git history is regenerated.

`tools/check_unity_metadata.py` now rejects malformed/nonzero GUID format, case-insensitive duplicates, missing metadata, orphan metadata and missing/multiple top-level GUID declarations. It does not rewrite assets. The baseline check failed specifically on the malformed token; the repaired repository and seven temporary corruption-fixture tests pass. CI runs both checks, and the existing visual checker and new-metadata generator also invoke the guard.

Unity 2022.3's [GUID reference source](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/2022.3/Editor/Mono/GUID.bindings.cs) delegates parsing to a native function. The source and offline guard alone do not establish whether the former 33-character token was rejected or truncated by a native importer. This repair establishes canonical metadata without changing referenced asset identity; native project import/build remain **NATIVE_UNVERIFIED**.

A single isolated Unity 2022.3.53f1 probe on 2026-10-13 exited1 after3.4seconds, before its parser method ran: `No ULF license found` / `No valid Unity Editor license`. It did not import the production project, create its Library, activate a license or change any user account. Temporary logs and the safe summary remain ignored. No native parser/import/build result is claimed.

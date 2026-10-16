# Unity 6.3 LTS — Breaking Changes

**Last verified:** 2026-02-13

This document tracks breaking API changes and behavioral differences between Unity 2022 LTS
(likely in model training) and Unity 6.3 LTS (current version). Organized by risk level.

## HIGH RISK — Will Break Existing Code

### Entities/DOTS API Complete Overhaul
**Versions:** Entities 1.0+ (Unity 6.0+)

```csharp
// ❌ OLD (pre-Unity 6, GameObjectEntity pattern)
public class HealthComponent : ComponentData {
    public float Value;
}

// ✅ NEW (Unity 6+, IComponentData)
public struct HealthComponent : IComponentData {
    public float Value;
}

// ❌ OLD: ComponentSystem
public class DamageSystem : ComponentSystem { }

// ✅ NEW: ISystem (unmanaged, Burst-compatible)
public partial struct DamageSystem : ISystem {
    public void OnCreate(ref SystemState state) { }
    public void OnUpdate(ref SystemState state) { }
}
```

**Migration:** Follow Unity's ECS migration guide. Major architectural changes required.

---

### Input System — Legacy Input Deprecated
**Versions:** Unity 6.0+

```csharp
// ❌ OLD: Input class (deprecated)
if (Input.GetKeyDown(KeyCode.Space)) { }

// ✅ NEW: Input System package
using UnityEngine.InputSystem;
if (Keyboard.current.spaceKey.wasPressedThisFrame) { }
```

**Migration:** Install Input System package, replace all `Input.*` calls with new API.

---

### URP/HDRP Renderer Feature API Changes
**Versions:** Unity 6.0+

```csharp
// ❌ OLD: ScriptableRenderPass.Execute signature
public override void Execute(ScriptableRenderContext context, ref RenderingData data)

// ✅ NEW: Uses RenderGraph API
public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
```

**Migration:** Update custom render passes to use RenderGraph API.

---

## MEDIUM RISK — Behavioral Changes

### Addressables — Asset Loading Returns
**Versions:** Unity 6.2+

Asset loading failures now throw exceptions by default instead of returning null.
Add proper exception handling or use `TryLoad` variants.

```csharp
// ❌ OLD: Silent null on failure
var handle = Addressables.LoadAssetAsync<Sprite>("key");
var sprite = handle.Result; // null if failed

// ✅ NEW: Throws on failure, use try/catch or TryLoad
try {
    var handle = Addressables.LoadAssetAsync<Sprite>("key");
    var sprite = await handle.Task;
} catch (Exception e) {
    Debug.LogError($"Failed to load: {e}");
}
```

---

### Physics — Default Solver Iterations Changed
**Versions:** Unity 6.0+

Default solver iterations increased for better stability.
Check `Physics.defaultSolverIterations` if you rely on old behavior.

---

## LOW RISK — Deprecations (Still Functional)

### UGUI (Legacy UI)
**Status:** Deprecated but supported
**Replacement:** UI Toolkit

UGUI still works but UI Toolkit is recommended for new projects.

---

### Legacy Particle System
**Status:** Deprecated
**Replacement:** Visual Effect Graph (VFX Graph)

---

### Old Animation System
**Status:** Deprecated
**Replacement:** Animator Controller (Mecanim)

---

## Platform-Specific Breaking Changes

### WebGL
- **Unity 6.0+**: WebGPU is now the default (WebGL 2.0 fallback available)
- Update shaders for WebGPU compatibility

### Android
- **Unity 6.0+**: Minimum API level raised to 24 (Android 7.0)

### iOS
- **Unity 6.0+**: Minimum deployment target raised to iOS 13

---

## Migration Checklist

When upgrading from 2022 LTS to Unity 6.3 LTS:

- [ ] Audit all DOTS/ECS code (complete rewrite likely needed)
- [ ] Replace `Input` class with Input System package
- [ ] Update custom render passes to RenderGraph API
- [ ] Add exception handling to Addressables calls
- [ ] Test physics behavior (solver iterations changed)
- [ ] Consider migrating UGUI to UI Toolkit for new UI
- [ ] Update WebGL shaders for WebGPU
- [ ] Verify minimum platform versions (Android/iOS)

---

**Sources:**
- https://docs.unity3d.com/6000.0/Documentation/Manual/upgrade-guides.html
- https://docs.unity3d.com/Packages/com.unity.entities@1.3/manual/upgrade-guide.html

---

## 6.3 → 6.6

**Last verified:** 2026-10-15

Sourced only from the official upgrade guides — one per step:
- 6.3 → 6.4: https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity64
- 6.4 → 6.5: https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity65
- 6.5 → 6.6: https://docs.unity.com/en-us/engine/6000.6/manual/upgrade-guides/upgrade-guide-unity66

Platform-only items (Android, iOS, XR) are listed briefly; this project targets macOS.

### 6.3 → 6.4

- **`Object.Destroy` now invokes `OnDisable` on all descendant components**, matching
  `DestroyImmediate`. Remove any manual `OnDisable` propagation — it now double-fires.
- **Asset Database artifact dependencies** reimport only when the dependency's import
  *result* changes. Importers reading another asset's source must call
  `AssetImportContext.DependsOnSourceAsset`; post-processors using indirect artifacts
  must call `DependsOnArtifact` on that asset.
- **URP Compatibility Mode removed** — custom render passes must use the render graph;
  `URP_COMPATIBILITY_MODE` define is gone.
- **SRP Core physics dependency is soft** — packages using physics declare
  `com.unity.modules.physics`. `IVolume` removed (use `VolumeGizmoDrawer`).
- **`LightShadowCasterMode` renames**: `NonLightmappedOnly` → `ShadowMask`,
  `Everything` → `DistanceShadowMask` (auto-updated).
- **Project Auditor built into the Editor**; `CodeModuleInstructionAnalyzer` and
  `InstructionAnalysisContext` are now internal.
- iOS: runtime libraries are now `Frameworks/UnityRuntime.framework`. XR: HoloLens 2 deprecated.

### 6.4 → 6.5

- **`InstanceID` removed → `EntityId`.** `go.GetInstanceID()` → `go.GetEntityId()`.
  `EntityId` is now **8 bytes**: never store it in an `int`, never use
  `EntityId.GetHashCode()` as a dictionary key.
- **Obsolete GameObject/Component shortcuts removed** (compile errors):
  `.rigidbody`, `.rigidbody2D`, `.camera`, `.light`, `.animation`, `.constantForce`,
  `.renderer`, `.audio`, `.networkView`, `.collider`, `.collider2d`, `.hingeJoint`,
  `.particleSystem` → `GetComponent<T>()`. `GameObject.AddComponent(string)` →
  `AddComponent<T>()`. `GameObject.SampleAnimation` → `AnimationClip.SampleAnimation`.
- **Entities**: `Entities.ForEach` → `IJobEntity` / `SystemAPI.Query`; `Job.WithCode` →
  `IJob`; `IAspect` → component and `EntityQuery` APIs.
- **2D low-level physics renamed**: `LowLevelPhysics2D` → `PhysicsCore2D`, namespace
  `UnityEngine.LowLevelPhysics2D` → `Unity.U2D.Physics`. Version gate: `UNITY_6000_5_OR_NEWER`.
- **Editor.log is project-local** (`<Project>/Logs/Editor.log`). Use
  `Application.consoleLogPath`; external tools pass `-logFile`.
- **Built-In Render Pipeline deprecated** (supported through 6.7 LTS) — use URP.
- **Render Pipeline Core no longer depends on uGUI** — add `com.unity.ugui` explicitly if used.
- **Unity ships `System.Text.Json`, `System.Collections.Immutable`,
  `System.Reflection.Metadata`, `System.Runtime.CompilerServices.Unsafe`** — your own
  copies are dropped from the build.
- **ModelImporter renames** (compile errors): `isFileScaleUsed` → `useFileScale`,
  `normalImportMode` → `importNormals`, `optimizeMesh` → `optimizeMeshPolygons`/`optimizeMeshVertices`,
  `resampleRotations` → `resampleCurves`, `splitTangentsAcrossSeams` → `tangentImportMode`,
  `tangentImportMode` → `importTangents`.
- Graphics: custom `RequestLightsDelegate` needs π intensity correction; Reflection Probe
  Shader Graph node deprecated; legacy Render Graph compiler removed (custom SRPs only).
- Android: min API 26, x86-64 removed, Gradle 9.1.0 / AGP 9.0.0, fullscreen APIs replaced
  by window-insets APIs. iOS: `ReplayKit` obsolete. XR: VR Module and Magic Leap removed.

### 6.5 → 6.6

- **`DEVELOPMENT_BUILD` and `UNITY_64` deprecated** (warnings `UAC0009` / `UAC0008`;
  hard errors in 6.8). Use `UNITY_ENABLE_CHECKS` for safety checks,
  `UNITY_INCLUDE_INSTRUMENTATION` for profiling/logging, `Debug.isDebugBuild` at runtime,
  `IntPtr.Size` instead of `UNITY_64`. New **Managed Code Variant** player setting
  (`PlayerSettings.SetManagedCodeVariant`). `CoreBuildData.developmentBuild` always
  returns `false`; `BuildOptions.ForceEnableAssertions` obsolete.
- **`[SerializeField] Dictionary<TKey, TValue>` fields are now serialized.** Types deriving
  from or wrapping `Dictionary` are unaffected. When migrating a custom solution, copy data
  to the new field before deleting the old one.
- **UI Toolkit: UXML Factory/Traits removed** → `[UxmlElement]` / `[UxmlAttribute]`.
  `UIToolkitInputConfiguration.SetRuntimeInputBackend` is now a compile error — delete calls.
- **Cinemachine 3 is a core package**; Cinemachine 2 projects auto-upgrade (different API).
- **YAML serialization**: word wrapping disabled, "Reduce Version Control Noise" removed —
  expect diffs on unrelated assets after upgrading.
- **Dynamic batching obsolete.** Render Pipeline Core no longer depends on Terrain
  (add `com.unity.modules.terrain` if used). Progressive CPU Lightmapper deprecated.
  Rendering Debugger `DebugState*` types no longer compile (use `ISerializedDebugDisplaySettings`).
- **`com.unity.test-framework.performance` is a core package.**
- Android: Legacy/Round icons removed, min OpenGL ES 3.1, `openGLRequireES31` deprecated.

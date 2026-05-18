# RVP (Randomation Vehicle Physics) — Unity 6.4 Migration Notes

## About This File
Analysis of the RVP codebase for Unity 6.4 upgrade. The PDF manual uses a custom font encoding
and cannot be machine-read directly; notes below are derived from full codebase analysis and
knowledge of the RVP package.

---

## RVP Manual — Key Extracted Points

### Project Settings Required
- **Tags**: `Pop Tire` (pops tires on contact), `Underside` (excludes underside from crash detection)
- **Layers**: `Ignore Wheel Cast`, `Vehicles`, `Detachable Part`
- **Script Execution Order**: GlobalControl and TimeMaster must execute before vehicles
- **Physics**: "Queries Hit Triggers" must be **false**
- Detachable parts must NOT collide with other detachable parts or `Ignore Wheel Cast` layer objects

### Vehicle Setup Rules (from manual)
- Rigidbody mass: acceptable range **0.5 – 10**; outside this causes unpredictable behavior
- Collision detection mode must be **Discrete**
- Vehicle scale must be **1 on all axes** — scale meshes on import instead
- Mesh orientation: +Z = forward (grille direction), +Y = up (roof direction)
- Suspension objects: +Z = outward from the side they're on, +Y = toward roof
- Wheel: +Z = outward from rim face

### Top Speed Formula
```
top speed (m/s) = maxRPM / lastGearRatio / (PI * 100) * wheelCircumference
* 2.23694 → mph    * 3.6 → km/h
```
After changing the torque curve at runtime: call `GasMotor.GetMaxRPM()`
After changing gear ratios: call `GearboxTransmission.CalculateRpmRanges()` + `GetFirstGear()`

### Wheel Groups (Performance)
- Each wheel group fires its raycast on a separate FixedUpdate
- 4 groups = 4 FixedUpdates to get all 4 wheel contacts
- **Intended for AI vehicles only** — reduces precision for player vehicles

### Transmission Ratios
- `> 1` = more torque, lower RPM (low gear)
- `< 1` = less torque, higher RPM (high gear)
- Gear array must contain at least one gear with ratio = 0 (neutral), required by `GetFirstGear()`
- Automatic oscillation issue: if tires slip, auto-trans upshifts → fix by increasing `feedbackRpmBias` on driven wheels

### Tire Shader (Built-in RP, custom)
- Vertex shader deforms tire mesh based on a **Deform Map** (grayscale texture) and **Deform Normal** (Vector4)
- White = full deform, black = no deform; wrap texture so rim connection doesn't deform
- Tire geometry should be a plain cylinder — grooves go in normal map only (not modeled in mesh)
- `_DeformNormal` is set by code (`Wheel.cs:398-399`) — **this shader must be rewritten for URP**
- "Tires Bumped" variant adds occlusion, normal mapping, smoothness slider

### Damage System Notes
- `VehicleDamage`: meshes in `deformMeshes` should NOT also be in `displaceParts` (double displacement)
- `maxCollisionPoints` should be **2** in most cases — has large performance impact
- `DetachablePart`: must be on the `Detachable Part` layer; must have ≥1 collider
- Vehicles **cannot be teleported** while loose parts are attached by joints
- `looseForce = -1` → part falls straight off with no hinge

### VehicleBalance (Motorcycle)
- The manual explicitly notes this script is **"not completely stable"** — experimental only

---

## RVP Architecture Overview

RVP is a **raycasting-based** vehicle physics system — it does NOT use Unity's WheelCollider.
All wheel contact detection uses `Physics.RaycastAll` per wheel per FixedUpdate.

### Core Component Hierarchy
```
VehicleParent         — root: holds input, wheel arrays, Rigidbody access, crash logic
  Suspension          — spring/damper per wheel; generates hard colliders at runtime
    Wheel             — raycasting, friction simulation, RPM, tire deformation
      DriveForce      — data container passing RPM/torque down the drivetrain
  GasMotor : Motor    — engine torque curve, RPM, ignition, boost
    GearboxTransmission / ContinuousTransmission — gear ratios, shift logic
  VehicleAssist       — drift assist, downforce, rollover correction
  VehicleDamage       — runtime mesh deformation, part displacement/detachment
  BasicInput          — maps old Input Manager axes to VehicleParent setters
GlobalControl         — scene singleton: layer masks, physic materials, tire mark config (static)
TimeMaster            — time scaling, audio mixer pitch
TireMarkCreate        — runtime mesh generation for skid marks per wheel
GroundSurfaceMaster   — surface type registry (friction, sparks, always-scrape)
```

### Key Design Patterns
- Input flows into VehicleParent via SetAccel/SetBrake/SetSteer etc. (decoupled from input source)
- DriveForce objects form a chain: Motor → Transmission → Suspension → Wheel (feedbackRPM goes back up)
- GlobalControl uses static fields set in Start() — a scene singleton pattern
- WheelCheckGroup: wheels split into groups, only one group raycasts per FixedUpdate (performance)
- TimeMaster sets Time.fixedDeltaTime every FixedUpdate to keep physics in sync with time scale

---

## RVP Manual Key Points (from codebase inference)

### Vehicle Setup
- VehicleParent requires Rigidbody; Suspension objects are children of the vehicle root
- Each Suspension holds a Wheel reference (sibling or child)
- Wheel expects: first child = rim Transform, first child of rim = tire Transform
- Hard colliders auto-generated at runtime on Suspension (CapsuleCollider) and Wheel (SphereCollider)
- Center of mass offset + suspension average height adjustment via `suspensionCenterOfMass`

### Drivetrain
- outputDrives array on GasMotor distributes torque; driveDividePower controls how torque splits
- feedbackRPM flows back from wheels through DriveForce chain to inform engine load
- Transmission uses gear ratios to scale RPM/torque between engine and wheels

### Damage System
- VehicleDamage.DamageApplication: deforms mesh vertices, displaces parts, damages motor/transmission health
- DetachablePart: parts that can hinge or fully break off
- ShatterPart: mesh replacement on break (broken material swap or renderer disable)
- Wheel.Detach(): disconnects wheel, spawns detached rigidbody with MeshCollider

### Physics Notes
- Suspension force applied at ground contact point or wheel position (configurable)
- Wheel friction force: forward + sideways slip curves, slip dependence modes, compression factor
- Burnout: accel+brake simultaneously above threshold spins drive wheels
- Hover mode: uses HoverWheel instead of Wheel (force-based levitation)

---

## Unity 6.4 Migration Issues


### CRITICAL — API Breakage

- Points 1 through 4 have been fixed and are therefore omitted.

### HIGH — Input System

#### 5. Legacy Input Manager — entire BasicInput.cs
- `Input.GetAxis`, `Input.GetButtonDown`, `Input.GetButton` are legacy API
- Still functional in Unity 6 if "Active Input Handling" = "Both" or "Input Manager (Old)"
- **Recommended**: Migrate to Unity Input System package (`InputAction`-based)
- VehicleParent's Set* methods are already perfectly decoupled — only BasicInput.cs needs changing
- Also affects: `GlobalControl.cs`, `BasicCameraInput.cs`, `VehicleDebug.cs`, `PropertyToggleSetter.cs`

#### 6. Input read in FixedUpdate (wrong frame)
- `BasicInput.cs` reads `Input.GetAxis` inside `FixedUpdate`
- Legacy `Input.GetAxis` accumulates between FixedUpdates so this works but can be inaccurate
- With new Input System this becomes a real bug — must separate frame input (Update) from physics input (FixedUpdate)

---

### HIGH — Performance

#### 7. `Physics.RaycastAll` allocates every FixedUpdate per wheel
- **File**: `Wheel.cs:418` — `Physics.RaycastAll(...)` called in `GetWheelContact()`
- This allocates a new `RaycastHit[]` array every physics frame per wheel
- **Fix**: Replace with `Physics.RaycastNonAlloc` + a pre-allocated `RaycastHit[]` buffer (static or field)

#### 8. Mesh vertex array allocation in VehicleDamage
- `VehicleDamage.cs:92`: `(Mesh)Instantiate(deformColliders[i].sharedMesh)` — fine
- `tempMeshes[i].vertices = meshVertices[i].verts` on every damage event — allocates
- Unity 6: use `Mesh.SetVertices(List<Vector3>)` or `Mesh.SetVertices(NativeArray<Vector3>)` for zero-alloc

#### 9. `GetComponent` calls in collision handlers
- `VehicleDamage.OnCollisionEnter` calls `GetComponent<Motor>()`, `GetComponent<Transmission>()`,
  `GetComponent<DetachablePart>()`, `GetComponent<Suspension>()`, `GetComponent<HoverWheel>()`,
  `GetComponent<SuspensionPart>()` inside loops over all damage/displace parts
- These should be cached in `Start()` via dictionaries or pre-resolved arrays

#### 10. Coroutine allocation: `new WaitForFixedUpdate()`
- `VehicleParent.cs:472`: `yield return new WaitForFixedUpdate()` in `WheelCheckLoop`
- Allocates every loop iteration. Cache a static `WaitForFixedUpdate` instance.
- Unity 6 alternative: use `Awaitable.FixedUpdateAsync()` (new in Unity 6)

#### 11. TireMarkCreate: per-frame mesh updates
- `TireMarkCreate.UpdateMark()` sets `mesh.vertices`, `mesh.triangles`, `mesh.uv`, `mesh.colors`
  every Update while creating a mark — 4 managed array assignments per frame per tire
- Unity 6: use `Mesh.SetVertices(NativeArray<>)` / `Mesh.SetColors(NativeArray<>)` etc.
- Also: `mesh.RecalculateTangents()` is expensive; only call when mark segment advances

#### 12. Static variables in GlobalControl not reset with Domain Reload disabled
- Unity 6's "Enter Play Mode Options" can skip domain reload — static fields won't reset
- `GlobalControl.wheelCastMaskStatic`, `frictionlessMatStatic`, etc. may hold stale values
- **Fix**: Add `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]`
  static method to null/zero all statics, or use `[field: SerializeField]` instance properties

---

### MEDIUM — Rendering Pipeline

#### 13. Built-in RP shaders need URP/HDRP conversion
- Project built for Built-in Render Pipeline (Unity 2019.4)
- Unity 6 defaults to URP
- Tire deformation shader (reads `_DeformNormal` property in `Wheel.cs:398`) — custom shader, needs port
- Rim glow shader (reads `_EmissionColor` in `Wheel.cs:404`) — likely Standard shader, auto-upgradable
  via Edit > Rendering > Render Pipeline Converter, but deform shader needs manual rewrite in Shader Graph

#### 14. `ShadowCastingMode.Off` — still valid, no change needed
- `TireMarkCreate.cs:173` — fine in all pipelines

---

### MEDIUM — Physics Behavior (PhysX 5)

#### 15. Unity 6 uses PhysX 5 (vs PhysX 4 in 2019.4)
- Collision detection and contact generation changed — vehicle may feel different
- Suspension spring forces may need retuning
- `rb.inertiaTensor = rb.inertiaTensor` workaround in `VehicleParent.cs:371` — this was a
  Unity 5.3 bug fix. PhysX 5 likely handles this correctly; the line is harmless but can be removed.
- Hard collider (CapsuleCollider on Suspension, SphereCollider on Wheel) interaction with PhysX 5
  contact welding may change how the vehicle sits — test carefully

#### 16. `ForceMode.Acceleration` usage
- Used for both wheel friction and suspension forces — still valid in Unity 6, no change

---

### LOW — Code Quality / Unity 6 Best Practices

#### 17. `F.GetTopmostParentComponent<T>()` calls GetComponent twice per parent
- `F.cs:29-30`: calls `GetComponent<T>()` twice on each parent node (check + assign)
- Cache the result: `T comp = tr.parent.GetComponent<T>(); if (comp != null) getting = comp;`

#### 18. TimeMaster sets Time.fixedDeltaTime in FixedUpdate
- `TimeMaster.cs:38`: `Time.fixedDeltaTime = Time.timeScale * initialFixedTime`
- Setting fixedDeltaTime during FixedUpdate is unusual; in Unity 6 this can interact poorly
  with the new fixed update scheduling. Move to Update.

#### 19. `Mathf.Infinity` as default serialized field values
- `Wheel.cs:180`: `public float detachForce = Mathf.Infinity;`
- `Suspension.cs:152`: `public float jamForce = Mathf.Infinity;`
- These serialize as infinity in the Inspector. Unity 6 handles this fine but it reads oddly.
  Use a sentinel like -1 meaning "never" instead.

#### 20. Coroutine vs Awaitable
- Unity 6 introduces `Awaitable` for async patterns. `WheelCheckLoop` IEnumerator could become
  an `async Awaitable` method, reducing overhead for the wheel group staggering system.

#### 21. Unnecessary `using System.Collections` imports
- Several files import `System.Collections` only for IEnumerator (coroutines).
  In Unity 6 these can be replaced with `Awaitable` where appropriate.

---

## Summary Priority Table

| # | Issue | Impact | Effort | Unity 6 Must-Fix? |
|---|-------|--------|--------|-------------------|
| 1 | `rb.velocity` in CameraControl | Compile error | Trivial | YES |
| 2 | `[ExecuteInEditMode]` | Warning | Trivial | Recommended |
| 5 | Legacy Input System | Functional but deprecated | Medium | Recommended |
| 7 | RaycastAll per wheel per frame | GC pressure / perf | Medium | Recommended |
| 8 | Mesh vertex alloc in damage | GC spikes on collision | Medium | Optional |
| 9 | GetComponent in collision loops | CPU perf | Low-Medium | Optional |
| 12 | Static vars + domain reload skip | Bugs on Play | Low-Medium | Recommended |
| 13 | Built-in RP shaders | Visual breakage | High effort | YES (if using URP) |
| 15 | PhysX 5 behavior change | Feel/tuning | Retest only | YES (retest) |
| 18 | fixedDeltaTime in FixedUpdate | Subtle physics bugs | Low | Recommended |

---

## Immediate Steps for Unity 6.4

1. Open project in Unity 6.4 — note all compile errors and warnings
2. Fix `CameraControl.cs:74` velocity → linearVelocity (likely the only compile error)
3. Replace `[ExecuteInEditMode]` with `[ExecuteAlways]` in Wheel.cs and Suspension.cs
4. Set "Active Input Handling" to "Both" in Project Settings to keep legacy input working
5. Run the Render Pipeline Converter (Edit > Rendering) if switching to URP
   — manually port the deform shader afterward
6. Playtest and retune suspension/friction values for PhysX 5 differences
7. Add static reset method to GlobalControl for domain-reload-skip compatibility
8. Replace Physics.RaycastAll with RaycastNonAlloc in Wheel.GetWheelContact()
9. Cache WaitForFixedUpdate in VehicleParent
10. Migrate BasicInput to Unity Input System package when ready

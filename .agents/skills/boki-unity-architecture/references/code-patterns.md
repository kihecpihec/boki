# Code patterns

Templates distilled from the existing code (`HealthState`, `PersistentUnlocks`, `RunSession`, `RouteGate`, `ScooterController`, `EnemyHealth`). Copy the shape, not the names. Numbers below are placeholders; real values come from the canonical docs (Story §4, Gameplay §5) and are passed in, never hard-coded in rules.

## 1. Rules class: pure, injected tuning, time as an argument

```csharp
using System;

namespace Boki.Battery
{
    // Tuning arrives from the adapter's Inspector fields; tests construct their own.
    public readonly struct BatteryTuning
    {
        public readonly float Capacity;
        public readonly float DrainPerSecond;
        public BatteryTuning(float capacity, float drainPerSecond)
        {
            if (capacity <= 0f) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (drainPerSecond < 0f) throw new ArgumentOutOfRangeException(nameof(drainPerSecond));
            Capacity = capacity;
            DrainPerSecond = drainPerSecond;
        }
    }

    public sealed class BatteryState
    {
        private readonly BatteryTuning tuning;
        public float Charge { get; private set; }
        public bool IsEmpty => Charge <= 0f;
        public event Action Changed;

        public BatteryState(BatteryTuning tuning) { this.tuning = tuning; Charge = tuning.Capacity; }

        public void Tick(float deltaSeconds)               // caller passes scaled delta; paused = 0
        {
            if (deltaSeconds <= 0f || IsEmpty) return;     // reject invalid deltas, stay frame-rate independent
            Charge = Math.Max(0f, Charge - tuning.DrainPerSecond * deltaSeconds);
            Changed?.Invoke();
        }

        public bool TryAdd(float amount)
        {
            if (amount <= 0f || Charge >= tuning.Capacity) return false;
            Charge = Math.Min(tuning.Capacity, Charge + amount);
            Changed?.Invoke();
            return true;
        }
    }
}
```

Why: no `UnityEngine`, no static state, no clock reads, no hidden randomness. Commands that can be refused return `bool`. `Changed` events are plain C#.

Randomness: take a `System.Random` (or `Func<int,int>`) in the constructor so a test can seed it.

## 2. I/O behind an interface

```csharp
public interface IUnlockStore { bool Read(RouteLevel level); void Write(RouteLevel level); void Save(); }
```

Rules take the interface; `Scripts/Core/` has the `PlayerPrefs` implementation; tests use a fake that counts `Save()` calls (see `PersistentUnlocksTests`). Write immediately when earned, call `Save()`, read once per New Run.

## 3. EditMode test shape

```csharp
using NUnit.Framework;
using Boki.Battery;

namespace Boki.Tests
{
    public sealed class BatteryStateTests
    {
        private static BatteryState Make() => new BatteryState(new BatteryTuning(100f, 1.5f));

        [Test]
        public void DrainIsFrameRateIndependent()
        {
            var a = Make(); var b = Make();
            for (int i = 0; i < 60; i++) a.Tick(1f / 60f);
            b.Tick(1f);
            Assert.That(a.Charge, Is.EqualTo(b.Charge).Within(1e-3f));
        }

        [Test] public void NegativeOrZeroDeltaIsIgnored() { var s = Make(); s.Tick(-1f); s.Tick(0f); Assert.That(s.Charge, Is.EqualTo(100f)); }
        [Test] public void AddClampsToCapacityAndRejectsWhenFull() { var s = Make(); Assert.That(s.TryAdd(10f), Is.False); }
    }
}
```

Name tests for behavior. Cover boundaries (zero, exactly at threshold, over cap), duplicate invocation, ordering (lock priority), and New Run cleanup. Do not assert private structure.

## 4. Adapter: validate, bind, unsubscribe

```csharp
[DisallowMultipleComponent]
public sealed class LightBattery : MonoBehaviour
{
    [SerializeField] private Light spotLight;
    [SerializeField, Min(0f)] private float drainPerSecond = 1.5f;   // Story §4 initial value
    private RunSession session;

    private void OnEnable()
    {
        if (spotLight == null)
        {
            Debug.LogError("Assign the spot light on LightBattery.", this);
            enabled = false;
            return;
        }
        session = RunSession.Ensure();
        session.State.Changed += Refresh;      // paired with OnDisable
        Refresh();
    }

    private void OnDisable() { if (session != null) session.State.Changed -= Refresh; }

    private void Update() => session.State.Battery.Tick(Time.deltaTime);   // adapter owns time, rules own math

    private void Refresh() => spotLight.enabled = !session.State.Battery.IsEmpty;
}
```

Adapters hold references and apply results to the scene. They hold no authoritative copy of run state. (`session.State.Battery.Tick` is the target API: today `RunState.Battery` is a plain `int`, so introducing this means moving battery into a rules object owned by `RunState`; see `project-structure.md`.)

## 5. One-shot latch for triggers and milestones

```csharp
private bool fired;
private void OnTriggerEnter(Collider other)
{
    if (fired || !isActiveAndEnabled || !manager.IsPlayer(other)) return;
    fired = true;                                  // set before side effects: re-entry cannot double-fire
    if (!session.EnterBossArena()) fired = false;  // rules may refuse; allow a legitimate retry
}
```

Also guard in the rules layer (`RunSession.EnterBossArena` keeps `hasEnteredBossArena`) so a second adapter cannot repeat the effect. Reset the latch on New Run.

## 6. Static registries and singletons

```csharp
private static readonly List<EnemyHealth> active = new List<EnemyHealth>();
public static IReadOnlyList<EnemyHealth> Active => active;

[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetRegistry() => active.Clear();

private void OnEnable() { if (IsAlive && !active.Contains(this)) active.Add(this); }
private void OnDisable() => active.Remove(this);
```

Every static gets a reset. Singletons destroy duplicates in `Awake` (`RunSession`). Do not create more global access points; pass references through serialized fields or constructor-like `Init` calls.

## 7. Input actions

Serialize `InputActionReference`s. Enable only what you find disabled and remember that you own it; disable only what you enabled; clear held state on `OnDisable` and `OnApplicationFocus` (see `ScooterController.EnableAction`). Keyboard and mouse only for the jam; do not read devices directly.

## 8. Queries and allocation in hot paths

- Cache components in `Awake`/`OnEnable`. No `GetComponent`, `Find*`, LINQ, string concatenation, or collection allocation in `Update`.
- Use `Physics.*NonAlloc` with a reused buffer when a query returns multiple hits. Single-hit `Raycast`/`Linecast`/`SphereCast` do not allocate.
- Update HUD text only when the value changes (event-driven), not every frame.
- Projectile travel uses a swept query so fast frames do not tunnel (`docs/plans/M6.md` §2). Bounded lifetime or range on every spawned object. Pool only after profiling.

## 9. Scene loading

All transitions go through `RunSession` (`Advance`, `Retry`/New Run). It validates the destination, guards `IsLoading`, saves progress before the load, and handles Editor versus player loading. A new feature that needs a scene change calls it rather than `SceneManager.LoadScene` directly.

## 10. Smells that earlier agents introduced, avoid repeating

- Copy-pasting a milestone Editor script and renaming it.
- Public setters on shared state (`RunState.Battery`) instead of rule methods.
- Magic layer numbers and string identifiers (upgrades as `string`).
- Treating a smoke-check or scene builder passing as proof the game is playable. They are not (Architecture §9).
- Putting game rules into prose inside code comments or `docs/plans`. Rules go in the canonical doc; handoffs only record what was done.

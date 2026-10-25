using Boki.Routes;
using Boki.Battery;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Boki.Core
{
    [DefaultExecutionOrder(-1000)]
    public sealed class RunSession : MonoBehaviour
    {
        public const string MainMenuScene = "Assets/Scenes/MainMenu.unity";
        public const string V1PohorjeScene = "Assets/Scenes/V1_Pohorje.unity";
        public const string V1MariborScene = "Assets/Scenes/V1_Maribor.unity";
        public const string V1FeriScene = "Assets/Scenes/V1_FERI.unity";
        public const string ForestScene = "Assets/Scenes/Routes_Forest_Test.unity";
        public const string CityScene = "Assets/Scenes/Routes_City_Test.unity";
        public const string PohorjeScene = "Assets/Scenes/Level1_Pohorje.unity";
        public const string MariborScene = "Assets/Scenes/Level2_Maribor.unity";
        public const string FeriScene = "Assets/Scenes/Level3_FERI.unity";

        [SerializeField] private string forestDestination = ForestScene;
        [SerializeField] private string cityDestination = CityScene;
        [SerializeField] private string feriDestination = "";
        [SerializeField] private bool persistentProgress;
        [Tooltip("Pull-in time between a portal accepting the rider and the next scene loading.")]
        [SerializeField, Min(0.1f)] private float portalTransitSeconds = 0.8f;
        public bool HasEnteredBossArena => hasEnteredBossArena;
        public float PortalTransitSeconds => portalTransitSeconds;
        private PersistentUnlocks unlocks;
        private LightBattery lightDriver;
        private PortalTransit transit;
        private bool hasEnteredBossArena;
        private bool arrivingThroughPortal;
        public string EntryScene => forestDestination;
        public static RunSession Instance { get; private set; }
        public RunState State { get; private set; }
        public bool IsLoading { get; private set; }
        public bool IsGameplayActive => State != null && State.Health.IsAlive
            && !IsLoading && !State.Pause.IsPaused && !State.Guilt.GameplaySuspended;
        // In-memory input retained for the standalone M3 demonstration.
        // Production scenes read permanent flags from PlayerPrefs on every new run.
        public UnlockSnapshot NextRunUnlocks { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => Instance = null;

        public static RunSession Ensure()
        {
            if (Instance == null) new GameObject("Run Session").AddComponent<RunSession>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            unlocks = new PersistentUnlocks(new PlayerPrefsUnlockStore());
            State = new RunState(ReadNewRunUnlocks(), Time.realtimeSinceStartupAsDouble);
            State.Pause.Changed += ApplyPause;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        public LightBattery Light => lightDriver;
        public bool BindLight(LightBattery driver)
        {
            if (lightDriver != null && lightDriver != driver)
            {
                Debug.LogError("Only one active player may drive the run light battery.", driver);
                return false;
            }
            lightDriver = driver;
            return true;
        }

        public void UnbindLight(LightBattery driver)
        {
            if (lightDriver == driver) lightDriver = null;
        }

        private float previousTimeScale = 1f;
        private bool appliedPause;
        public void SetPaused(PauseReason reason, bool paused) => State.Pause.Set(reason, paused);
        private void ApplyPause()
        {
            if (State.Pause.IsPaused == appliedPause) return;
            if (State.Pause.IsPaused)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                appliedPause = true;
            }
            else
            {
                Time.timeScale = previousTimeScale;
                appliedPause = false;
            }
        }
        private void Update() => State.LightBattery.Tick(Time.deltaTime,
            IsGameplayActive && lightDriver != null && lightDriver.IsGameplayActive);

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            IsLoading = false;
            State.Guilt.IsLoading = false;
            SetPaused(PauseReason.Loading, false);
            if (transit == null) return;
            if (arrivingThroughPortal) transit.PlayArrive(); else transit.Cancel();
            arrivingThroughPortal = false;
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (State != null) State.Pause.Changed -= ApplyPause;
            if (Instance == this)
            {
                if (appliedPause) Time.timeScale = previousTimeScale;
                Instance = null;
            }
        }

        // Production routing owns persistence. A scene cached before M5 must not
        // silently disable saving because it lacks a newly serialized checkbox.
        private bool UsesPersistentUnlocks => persistentProgress || forestDestination == PohorjeScene;
        private UnlockSnapshot ReadNewRunUnlocks() => UsesPersistentUnlocks ? unlocks.Read() : NextRunUnlocks;

        public bool EnterBossArena()
        {
            if (hasEnteredBossArena || IsLoading || State.ActiveLevel != RouteLevel.Feri || !State.Health.IsAlive
                || (State.Choice(RouteLevel.Feri) == RouteChoice.None && !State.BossShortcutTaken)) return false;
            if (UsesPersistentUnlocks) unlocks.Earn(State, RouteLevel.Feri);
            hasEnteredBossArena = true;
            State.Guilt.EndForBoss();
            return true;
        }

        public bool StartV1Run()
        {
            if (IsLoading) return false;
            forestDestination = V1PohorjeScene;
            cityDestination = V1MariborScene;
            feriDestination = V1FeriScene;
            persistentProgress = true;
            return Retry();
        }

        public bool ReturnToMainMenu()
        {
            if (IsLoading || !CanLoad(MainMenuScene)) return false;
            IsLoading = true;
            State.Pause.Reset();
            SetPaused(PauseReason.Menu, true);
            State.Guilt.IsLoading = true;
            SetPaused(PauseReason.Loading, true);
            LoadScene(MainMenuScene);
            return true;
        }

        public bool Retry() => Load(forestDestination, true, RouteLevel.Forest);

        public bool TrySkipToV1Boss(RouteLevel from, float horizontalSpeed, float minimumSpeed)
        {
            string source = from == RouteLevel.Forest ? V1PohorjeScene : V1MariborScene;
            if (!IsGameplayActive || forestDestination != V1PohorjeScene || cityDestination != V1MariborScene
                || feriDestination != V1FeriScene || SceneManager.GetActiveScene().path != source
                || !State.CanSkipToBoss(from, horizontalSpeed, minimumSpeed) || !CanLoad(V1FeriScene)) return false;
            // Save only the exit actually reached, before moving the active level past it.
            if (UsesPersistentUnlocks) unlocks.Earn(State, from);
            if (!State.TrySkipToBoss(from, horizontalSpeed, minimumSpeed)) return false;
            IsLoading = true;
            State.Guilt.IsLoading = true;
            SetPaused(PauseReason.Loading, true);
            LoadThroughPortal(V1FeriScene);
            return true;
        }

        public bool Advance(RouteLevel level, bool throughPortal = false)
        {
            if (!State.CanAdvance(level)) return false;
            string destination = level == RouteLevel.Forest ? cityDestination : feriDestination;
            if (string.IsNullOrWhiteSpace(destination)) return false;
            return Load(destination, false, level, throughPortal);
        }

        private bool Load(string path, bool newRun, RouteLevel from, bool throughPortal = false)
        {
            if (IsLoading || !CanLoad(path)) return false;
            IsLoading = true;
            if (newRun)
            {
                hasEnteredBossArena = false;
                State.NewRun(ReadNewRunUnlocks(), Time.realtimeSinceStartupAsDouble);
            }
            else
            {
                // The destination and prerequisites are valid before progress is saved.
                if (!State.CanAdvance(from)) { IsLoading = false; return false; }
                if (UsesPersistentUnlocks) unlocks.Earn(State, from);
                State.TryAdvance(from);
            }
            State.Guilt.IsLoading = true;
            SetPaused(PauseReason.Loading, true);
            if (throughPortal) LoadThroughPortal(path); else LoadScene(path);
            return true;
        }

        // Progress is already committed and the world is frozen by PauseReason.Loading,
        // so the pull-in plays on unscaled time before the scene is replaced.
        private void LoadThroughPortal(string path)
        {
            arrivingThroughPortal = true;
            if (transit == null) transit = gameObject.AddComponent<PortalTransit>();
            transit.PlayEnter(portalTransitSeconds, () => LoadScene(path));
        }

        private bool CanLoad(string path)
        {
#if UNITY_EDITOR
            bool available = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path) != null;
#else
            bool available = Application.CanStreamedLevelBeLoaded(path);
#endif
            if (!available) Debug.LogError("Missing run destination in the build scene list: " + path, this);
            return available;
        }

        private static void LoadScene(string path)
        {
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(path);
#endif
        }
    }
}

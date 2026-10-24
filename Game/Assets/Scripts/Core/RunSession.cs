using Boki.Routes;
using Boki.Battery;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Boki.Core
{
    [DefaultExecutionOrder(-1000)]
    public sealed class RunSession : MonoBehaviour
    {
        public const string ForestScene = "Assets/Scenes/Routes_Forest_Test.unity";
        public const string CityScene = "Assets/Scenes/Routes_City_Test.unity";
        public const string PohorjeScene = "Assets/Scenes/Level1_Pohorje.unity";
        public const string MariborScene = "Assets/Scenes/Level2_Maribor.unity";
        public const string FeriScene = "Assets/Scenes/Level3_FERI.unity";

        [SerializeField] private string forestDestination = ForestScene;
        [SerializeField] private string cityDestination = CityScene;
        [SerializeField] private string feriDestination = "";
        [SerializeField] private bool persistentProgress;
        public bool HasEnteredBossArena => hasEnteredBossArena;
        private PersistentUnlocks unlocks;
        private LightBattery lightDriver;
        private bool hasEnteredBossArena;
        public string EntryScene => forestDestination;
        public static RunSession Instance { get; private set; }
        public RunState State { get; private set; }
        public bool IsLoading { get; private set; }
        public bool IsGameplayActive => State != null && State.Health.IsAlive
            && !IsLoading && !State.Guilt.GameplaySuspended;
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
            SceneManager.sceneLoaded += SceneLoaded;
        }

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

        private void Update() => State.LightBattery.Tick(Time.deltaTime,
            lightDriver != null && lightDriver.IsGameplayActive);

        private void SceneLoaded(Scene scene, LoadSceneMode mode)
        {
            IsLoading = false;
            State.Guilt.IsLoading = false;
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (Instance == this) { Time.timeScale = 1f; Instance = null; }
        }

        // Production routing owns persistence. A scene cached before M5 must not
        // silently disable saving because it lacks a newly serialized checkbox.
        private bool UsesPersistentUnlocks => persistentProgress || forestDestination == PohorjeScene;

        public void SetPaused(bool paused)
        {
            State.Guilt.GameplaySuspended = paused;
            Time.timeScale = paused ? 0f : 1f;
        }
        private UnlockSnapshot ReadNewRunUnlocks() => UsesPersistentUnlocks ? unlocks.Read() : NextRunUnlocks;

        public bool EnterBossArena()
        {
            if (hasEnteredBossArena || IsLoading || State.ActiveLevel != RouteLevel.Feri || !State.Health.IsAlive
                || State.Choice(RouteLevel.Feri) == RouteChoice.None) return false;
            if (UsesPersistentUnlocks) unlocks.Earn(State, RouteLevel.Feri);
            hasEnteredBossArena = true;
            State.Guilt.EndForBoss();
            return true;
        }

        public bool Retry() => Load(forestDestination, true, RouteLevel.Forest);
        public bool Advance(RouteLevel level)
        {
            if (!State.CanAdvance(level)) return false;
            string destination = level == RouteLevel.Forest ? cityDestination : feriDestination;
            if (string.IsNullOrWhiteSpace(destination)) return false;
            return Load(destination, false, level);
        }

        private bool Load(string path, bool newRun, RouteLevel from)
        {
            if (IsLoading) return false;
#if UNITY_EDITOR
            if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path) == null)
#else
            if (!Application.CanStreamedLevelBeLoaded(path))
#endif
            {
                Debug.LogError("Missing run destination in the build scene list: " + path, this);
                return false;
            }
            IsLoading = true;
            SetPaused(false);
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
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            SceneManager.LoadScene(path);
#endif
            return true;
        }
    }
}

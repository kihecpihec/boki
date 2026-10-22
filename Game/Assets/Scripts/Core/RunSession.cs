using Boki.Routes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Boki.Core
{
    public sealed class RunSession : MonoBehaviour
    {
        public const string ForestScene = "Assets/Scenes/Routes_Forest_Test.unity";
        public const string CityScene = "Assets/Scenes/Routes_City_Test.unity";
        public static RunSession Instance { get; private set; }
        public RunState State { get; private set; }
        public bool IsLoading { get; private set; }
        // M5 supplies saved flags here before starting the next run.
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
            State = new RunState(NextRunUnlocks, Time.realtimeSinceStartupAsDouble);
            SceneManager.sceneLoaded += SceneLoaded;
        }

        private void SceneLoaded(Scene scene, LoadSceneMode mode) => IsLoading = false;
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (Instance == this) Instance = null;
        }

        public bool Retry() => Load(ForestScene, true, RouteLevel.Forest);
        public bool Advance(RouteLevel level)
        {
            if (level != RouteLevel.Forest || !State.CanAdvance(level)) return false;
            return Load(CityScene, false, level);
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
            if (newRun) State.NewRun(NextRunUnlocks, Time.realtimeSinceStartupAsDouble);
            else if (!State.TryAdvance(from)) { IsLoading = false; return false; }
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

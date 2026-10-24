using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Boki.Editor
{
    /// <summary>
    /// Walks a CharacterController with the scene player's own capsule from the start through each route into the
    /// classroom floor (stopping short of the lectern at the Exit_FERI marker) in FERI_Map_Test. Proves the guide colliders and Route B's ramps are walkable end to end with
    /// the seals open; says nothing about jump, boost, camera or gameplay.
    /// </summary>
    public static class FeriMapChecks
    {
        private const float Step = 0.1f;
        private const float Reached = 0.5f;
        private const int GuardSteps = 2000;

        // Blender x/y of each route's door line (README: A left, B centre, C right).
        private static readonly (string name, float x)[] Routes = { ("A", -24f), ("B", 0f), ("C", 24f) };

        [MenuItem("Boki/FERI/Run Walk Check")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(FeriMapTestBuilder.Target, OpenSceneMode.Single);
            var template = Object.FindFirstObjectByType<Boki.Player.ScooterController>().GetComponent<CharacterController>();
            int failures = 0;
            foreach (var (name, x) in Routes)
            {
                var path = new[] { new Vector2(0f, 4f), new Vector2(x, 6f), new Vector2(x, 56f), new Vector2(0f, 58f), new Vector2(0f, 75f) };
                var walker = new GameObject("walker_" + name).AddComponent<CharacterController>();
                walker.height = template.height; walker.radius = template.radius; walker.center = template.center;
                walker.stepOffset = template.stepOffset; walker.slopeLimit = template.slopeLimit; walker.skinWidth = template.skinWidth;
                walker.transform.position = new Vector3(path[0].x, 0.1f, path[0].y);
                Physics.SyncTransforms();

                float maxY = 0f, minY = 0f;
                bool stuck = false;
                for (int i = 1; i < path.Length && !stuck; i++)
                {
                    var target = new Vector3(path[i].x, 0f, path[i].y);
                    for (int guard = 0; ; guard++)
                    {
                        var flat = target - new Vector3(walker.transform.position.x, 0f, walker.transform.position.z);
                        if (flat.magnitude < Reached) break;
                        if (guard > GuardSteps) { stuck = true; Report(walker, flat.normalized, name, i); break; }
                        walker.Move(flat.normalized * Step + Vector3.down * 0.3f);
                        Physics.SyncTransforms();
                        maxY = Mathf.Max(maxY, walker.transform.position.y);
                        minY = Mathf.Min(minY, walker.transform.position.y);
                    }
                }
                var end = walker.transform.position;
                bool ok = !stuck && Mathf.Abs(end.z - path[path.Length - 1].y) < Reached + 0.1f;
                Debug.Log($"FERI_WALK route={name} reached={ok} end=({end.x:F1},{end.y:F2},{end.z:F1}) yRange=[{minY:F2},{maxY:F2}]");
                if (!ok) failures++;
                Object.DestroyImmediate(walker.gameObject);
            }
            Debug.Log($"FERI_WALK_DONE failures={failures}");
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        private static void Report(CharacterController walker, Vector3 direction, string route, int segment)
        {
            var origin = walker.transform.position;
            Debug.LogError($"FERI_WALK stuck route={route} segment={segment} at {origin}");
            var hits = Physics.CapsuleCastAll(origin + Vector3.up * 0.45f, origin + Vector3.up * 1.35f, walker.radius, direction, 1f);
            var seen = new HashSet<string>();
            foreach (var hit in hits)
                if (hit.collider != walker && seen.Add(hit.collider.name))
                    Debug.LogError($"FERI_WALK   blocker {hit.collider.name} {hit.collider.GetType().Name} dist={hit.distance:F2} bounds={hit.collider.bounds}");
        }
    }
}

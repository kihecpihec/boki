using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Boki.Editor
{
    /// <summary>
    /// Walks a CharacterController with the scene player's own capsule from the start through each route into the
    /// classroom floor (stopping short of the lectern at the Exit_FERI marker) in FERI_Map_Test. Proves the guide colliders and Route B's ramps are walkable end to end with
    /// the seals open, and that every indoor stretch of the walk is roofed (only the campus arrival and Route B's
    /// courtyard pocket may be open to the sky); says nothing about jump, boost, camera or gameplay.
    /// </summary>
    public static class FeriMapChecks
    {
        private const float Step = 0.1f;
        private const float Reached = 0.5f;
        private const int GuardSteps = 2000;

        private const float RoofReach = 3f;

        [System.Serializable] private class Marker { public string name; public float[] position; public float[] size; }
        [System.Serializable] private class MarkerList { public Marker[] markers; }

        private static Marker Find(Marker[] list, string name)
        {
            foreach (var marker in list) if (marker.name == name) return marker;
            throw new System.InvalidOperationException("markers.json has no " + name);
        }

        [MenuItem("Boki/FERI/Run Walk Check")]
        public static void Run()
        {
            EditorSceneManager.OpenScene(FeriMapTestBuilder.Target, OpenSceneMode.Single);
            var template = Object.FindFirstObjectByType<Boki.Player.ScooterController>().GetComponent<CharacterController>();
            int failures = 0;
            // Door lines, rear seals, start and exit come from markers.json (Blender x/y), so a re-export needs no edit here.
            var markers = JsonUtility.FromJson<MarkerList>(File.ReadAllText("Assets/Art/FERI/markers.json")).markers;
            var start = Find(markers, "Start_Player").position;
            float rearY = Find(markers, "Seal_A_Rear").position[1], exitY = Find(markers, "Exit_FERI").position[1];
            var arrivalEnd = Find(markers, "Gate_A").position[1];
            var courtyard = Find(markers, "Occupancy_B");
            float courtyardY0 = courtyard.position[1] - courtyard.size[1] / 2f, courtyardY1 = courtyard.position[1] + courtyard.size[1] / 2f;
            foreach (var route in new[] { "A", "B", "C" })
            {
                string name = route;
                float x = Find(markers, "Gate_" + route).position[0];
                var path = new[] { new Vector2(start[0], start[1]), new Vector2(x, arrivalEnd - 2f), new Vector2(x, rearY + 4f), new Vector2(0f, rearY + 6f), new Vector2(0f, exitY - 4f) };
                var walker = new GameObject("walker_" + name).AddComponent<CharacterController>();
                walker.height = template.height; walker.radius = template.radius; walker.center = template.center;
                walker.stepOffset = template.stepOffset; walker.slopeLimit = template.slopeLimit; walker.skinWidth = template.skinWidth;
                walker.transform.position = new Vector3(path[0].x, 0.1f, path[0].y);
                Physics.SyncTransforms();

                float maxY = 0f, minY = 0f;
                bool stuck = false;
                int roofed = 0, openIndoors = 0;
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
                        CountRoof(walker.transform.position, route == "B", arrivalEnd, courtyardY0, courtyardY1, ref roofed, ref openIndoors);
                    }
                }
                var end = walker.transform.position;
                bool ok = !stuck && Mathf.Abs(end.z - path[path.Length - 1].y) < Reached + 0.1f && openIndoors == 0;
                Debug.Log($"FERI_WALK route={name} reached={ok} end=({end.x:F1},{end.y:F2},{end.z:F1}) yRange=[{minY:F2},{maxY:F2}] roofedSteps={roofed} openIndoorSteps={openIndoors}");
                if (!ok) failures++;
                Object.DestroyImmediate(walker.gameObject);
            }
            Debug.Log($"FERI_WALK_DONE failures={failures}");
            if (Application.isBatchMode) EditorApplication.Exit(failures == 0 ? 0 : 1);
        }

        // The arrival strip (z below the gates) and Route B's courtyard pocket are meant to be open; anywhere else
        // a ray straight up must hit the ceiling slab.
        private static void CountRoof(Vector3 feet, bool courtyardRoute, float arrivalEnd, float courtyardY0, float courtyardY1, ref int roofed, ref int openIndoors)
        {
            bool hit = Physics.Raycast(feet + Vector3.up * 2.2f, Vector3.up, RoofReach, ~0, QueryTriggerInteraction.Ignore);
            bool meantOpen = feet.z < arrivalEnd || (courtyardRoute && feet.z > courtyardY0 && feet.z < courtyardY1);
            if (hit) roofed++;
            else if (!meantOpen) openIndoors++;
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

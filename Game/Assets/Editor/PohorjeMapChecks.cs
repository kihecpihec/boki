using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Boki.Editor
{
    /// <summary>
    /// Traversal checks for Pohorje_Map_Test against the v2 layout. Opens the scene without saving and reports:
    /// floors whose collision is missing or at the wrong height, controller drop-through, blocked or falling lane rides,
    /// and free rides that end under a floor or out of the world.
    /// Batch: Unity -batchmode -nographics -quit -executeMethod Boki.Editor.PohorjeMapChecks.Run
    /// </summary>
    public static class PohorjeMapChecks
    {
        private const string Scene = "Assets/Scenes/Pohorje_Map_Test.unity";
        private const string Layout = "../output/world-modeling-v2/maps/pohorje/layout.json";
        private const float Dt = 1f / 60f;

        [Serializable] private class LaneData { public string name; public float width; public float[] pts; }
        [Serializable] private class PadData { public string name; public float cx, cz, hx, hz, h; }
        [Serializable] private class UnityLayout { public LaneData[] lanes; public PadData[] pads; }
        [Serializable] private class LayoutFile { public UnityLayout unity; }

        [MenuItem("Boki/Pohorje/Run Map Collision Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("POHORJE_CHECK refuses Play Mode"); return; }
            var layout = LoadLayout();
            if (layout == null) return;
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            var cc = Object.FindFirstObjectByType<CharacterController>();
            if (cc == null) { Debug.LogError("POHORJE_CHECK_FAIL no player controller"); return; }
            int mask = Physics.DefaultRaycastLayers & ~(1 << cc.gameObject.layer);
            Physics.SyncTransforms();

            var report = new StringBuilder();
            int floorSamples = 0, floorBad = 0;
            foreach (var lane in layout.lanes)
            {
                var pts = Points(lane);
                for (int i = 0; i + 1 < pts.Count; i++)
                {
                    Vector3 a = pts[i], b = pts[i + 1];
                    Vector3 right = Vector3.Cross(Vector3.up, (b - a).normalized);
                    int steps = Mathf.CeilToInt(Vector3.Distance(a, b));
                    for (int s = 0; s <= steps; s++)
                    foreach (float side in new[] { 0f, -1f, 1f })
                    {
                        Vector3 p = Vector3.Lerp(a, b, s / (float)steps) + right * side * (lane.width / 2f - 0.6f);
                        floorSamples++;
                        // Ramps and bumps (UCX_) are intentional floor; terrain must match the design height.
                        if (Floor(p.x, p.z, mask, out var hit, p.y + 1.5f) && (Mathf.Abs(hit.point.y - p.y) < 0.3f || hit.collider.name.StartsWith("UCX_"))) continue;
                        floorBad++;
                        if (floorBad <= 20)
                            report.AppendLine($"FLOOR lane {lane.name} expected y={p.y:F2} at ({p.x:F1}, {p.z:F1}) got " +
                                              (hit.collider != null ? $"{hit.point.y:F2} on {hit.collider.name}" : "nothing"));
                    }
                }
            }
            foreach (var pad in layout.pads)
                for (float x = pad.cx - pad.hx + 1.5f; x <= pad.cx + pad.hx - 1.5f; x += 1.5f)
                for (float z = pad.cz - pad.hz + 1.5f; z <= pad.cz + pad.hz - 1.5f; z += 1.5f)
                {
                    floorSamples++;
                    // Arenas carry mounds, pits and props, so only require walkable ground near the pad height.
                    if (Floor(x, z, mask, out var hit, pad.h + 3f) && hit.point.y > pad.h - 1.6f) continue;
                    floorBad++;
                    if (floorBad <= 20) report.AppendLine($"FLOOR pad {pad.name} hole at ({x:F1}, {z:F1})");
                }

            int drops = 0;
            for (float x = -95f; x <= 91f; x += 3f)
            for (float z = -19f; z <= 193f; z += 3f)
            {
                if (!Floor(x, z, mask, out var hit, 70f) || hit.normal.y < 0.7f) continue;
                Place(cc, hit.point + Vector3.up * 1.2f);
                float vy = 0f;
                for (int i = 0; i < 60; i++)
                {
                    vy -= 25f * Dt;
                    if ((cc.Move(new Vector3(0f, vy * Dt, 0f)) & CollisionFlags.Below) != 0) vy = -2f;
                }
                if (cc.transform.position.y >= hit.point.y - 0.5f) continue;
                drops++;
                if (drops <= 10) report.AppendLine($"DROP controller fell through {hit.collider.name} at {hit.point:F1}");
            }

            int falls = 0, blocked = 0;
            foreach (var lane in layout.lanes)
                Ride(cc, "lane " + lane.name, Points(lane), mask, report, ref falls, ref blocked);
            int crossingsBlocked = 0, crossingFalls = 0;
            foreach (var (from, to) in new[] { ("A", "ExitA"), ("B", "ExitB"), ("C", "ExitC") })
            {
                var a = Points(Array.Find(layout.lanes, l => l.name == from));
                var b = Points(Array.Find(layout.lanes, l => l.name == to));
                Ride(cc, $"arena {from}", new List<Vector3> { a[a.Count - 1], b[0] }, mask, report, ref crossingFalls, ref crossingsBlocked);
            }
            int wander = Wander(cc, mask, report);

            Debug.Log($"POHORJE_CHECK floorSamples={floorSamples} floorBad={floorBad} drops={drops} laneFalls={falls} laneBlocked={blocked} " +
                      $"arenaCrossingsBlocked={crossingsBlocked} arenaFalls={crossingFalls} wanderFailures={wander}\n{report}");
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        }

        private static UnityLayout LoadLayout()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Layout));
            if (!File.Exists(path)) { Debug.LogError("POHORJE_CHECK_FAIL no layout at " + path); return null; }
            return JsonUtility.FromJson<LayoutFile>(File.ReadAllText(path)).unity;
        }

        private static List<Vector3> Points(LaneData lane)
        {
            var list = new List<Vector3>();
            for (int i = 0; i + 2 < lane.pts.Length; i += 3) list.Add(new Vector3(lane.pts[i], lane.pts[i + 2], lane.pts[i + 1]));
            return list;
        }

        /// <summary>Highest walkable collision below maxY: ignores roofs, beams and the tunnel ceiling.</summary>
        private static bool Floor(float x, float z, int mask, out RaycastHit hit, float maxY)
        {
            var hits = Physics.RaycastAll(new Vector3(x, 120f, z), Vector3.down, 200f, mask, QueryTriggerInteraction.Ignore);
            hit = default;
            float best = float.NegativeInfinity;
            foreach (var h in hits)
                if (h.normal.y > 0.6f && h.point.y < maxY && h.point.y > best) { best = h.point.y; hit = h; }
            return best > float.NegativeInfinity;
        }

        /// <summary>Feet height of the controller capsule.</summary>
        private static float Feet(CharacterController cc) =>
            cc.transform.TransformPoint(cc.center).y - cc.height * 0.5f * cc.transform.lossyScale.y;

        // Under a walkable surface less than 2 m above the feet (tunnel clearance is 4.6 m, so roofs never count).
        private static bool UnderFloor(CharacterController cc, int mask, out RaycastHit floor)
        {
            Vector3 p = cc.transform.position; float feet = Feet(cc);
            return Floor(p.x, p.z, mask, out floor, feet + 2f) && feet < floor.point.y - 0.8f;
        }

        private static void Place(CharacterController cc, Vector3 p)
        {
            cc.enabled = false;
            cc.transform.position = p;
            cc.enabled = true;
            Physics.SyncTransforms();
        }

        // Rides a polyline at boost speed with gravity, like ScooterController.
        private static void Ride(CharacterController cc, string name, List<Vector3> pts, int mask, StringBuilder report,
            ref int falls, ref int blocks)
        {
            Place(cc, pts[0] + Vector3.up * 0.3f);
            float vy = 0f;
            for (int w = 1; w < pts.Count; w++)
            {
                float stuck = 0f;
                for (int step = 0; step < 3000; step++)
                {
                    Vector3 pos = cc.transform.position;
                    Vector3 to = pts[w] - pos; to.y = 0f;
                    if (to.magnitude < 1f) break;
                    vy -= 25f * Dt;
                    if ((cc.Move(to.normalized * 10.5f * Dt + Vector3.up * vy * Dt) & CollisionFlags.Below) != 0) vy = -2f;
                    Vector3 now = cc.transform.position;
                    stuck = new Vector2(now.x - pos.x, now.z - pos.z).magnitude < 10.5f * Dt * 0.2f ? stuck + Dt : 0f;
                    if (stuck > 0.5f)
                    {
                        blocks++;
                        report.AppendLine($"BLOCKED {name} near {now:F1} heading to {pts[w]:F1}");
                        Place(cc, pts[w] + Vector3.up * 0.5f); vy = 0f; break;
                    }
                    if (UnderFloor(cc, mask, out _) || now.y < -20f)
                    {
                        falls++;
                        report.AppendLine($"FALL {name} at {now:F1}");
                        Place(cc, pts[w] + Vector3.up * 0.5f); vy = 0f; break;
                    }
                }
            }
        }

        // Free riding like a player: random headings at bunny-hop speeds with jumps, everywhere in the play area.
        private static int Wander(CharacterController cc, int mask, StringBuilder report)
        {
            var rng = new System.Random(7);
            int failures = 0;
            for (int run = 0; run < 500; run++)
            {
                float x = (float)(rng.NextDouble() * 186 - 95), z = (float)(rng.NextDouble() * 212 - 19);
                if (!Floor(x, z, mask, out var start, 70f) || start.normal.y < 0.8f) continue;
                Place(cc, start.point + Vector3.up * 0.2f);
                float yaw = (float)(rng.NextDouble() * 360), speed = 6f + (float)rng.NextDouble() * 12f, vy = 0f;
                for (int step = 0; step < 300; step++)
                {
                    if (step % 40 == 0) yaw += (float)(rng.NextDouble() * 90 - 45);
                    Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                    vy -= 25f * Dt;
                    var flags = cc.Move(dir * speed * Dt + Vector3.up * vy * Dt);
                    if ((flags & CollisionFlags.Below) != 0) vy = rng.NextDouble() < 0.08 ? 7.75f : -2f;
                    Vector3 now = cc.transform.position;
                    bool under = UnderFloor(cc, mask, out var floor);
                    if (!under && now.y > -5f) continue;
                    failures++;
                    if (failures <= 20)
                        report.AppendLine(under ? $"WANDER under {floor.collider.name} at {now:F1}" : $"WANDER out of world at {now:F1}");
                    break;
                }
            }
            return failures;
        }

        /// <summary>Renders review screenshots to BOKI_CAPTURE_DIR (default /tmp). Needs a graphics device.</summary>
        public static void Capture()
        {
            EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
            string dir = Environment.GetEnvironmentVariable("BOKI_CAPTURE_DIR") ?? "/tmp";
            var cam = new GameObject("CaptureCamera").AddComponent<Camera>();
            var main = Camera.main;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = main != null ? main.backgroundColor : Color.black;
            cam.farClipPlane = 420f; cam.fieldOfView = 70f;
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            var shots = new (string, Vector3, Vector3)[]
            {
                ("01-bellevue", new Vector3(0f, 43.2f, -9f), new Vector3(0f, 39f, 20f)),
                ("02-fork", new Vector3(0f, 41f, 30f), new Vector3(0f, 37f, 52f)),
                ("03-route-a", new Vector3(-24f, 38.5f, 54f), new Vector3(-34f, 33f, 72f)),
                ("04-route-b", new Vector3(0f, 40f, 54f), new Vector3(0f, 24f, 100f)),
                ("05-route-c-tunnel", new Vector3(32f, 38f, 58f), new Vector3(42f, 32f, 76f)),
                ("06-arena-a", new Vector3(-48f, 30f, 104f), new Vector3(-68f, 22f, 128f)),
                ("07-arena-b", new Vector3(-16f, 28f, 104f), new Vector3(4f, 20f, 132f)),
                ("08-arena-c", new Vector3(44f, 31f, 108f), new Vector3(66f, 24f, 128f)),
                ("09-overlook", new Vector3(0f, 17f, 180f), new Vector3(0f, 0f, 320f)),
                ("10-overview", new Vector3(-120f, 110f, -10f), new Vector3(0f, 20f, 110f)),
            };
            cam.transform.position = shots[0].Item2; cam.Render(); // warm-up: the first render after load lacks textures/lights
            foreach (var (name, from, at) in shots)
            {
                cam.transform.position = from;
                cam.transform.LookAt(at);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                File.WriteAllBytes(Path.Combine(dir, "pohorje-" + name + ".png"), tex.EncodeToPNG());
            }
            RenderTexture.active = null;
            Debug.Log("POHORJE_CAPTURE " + dir);
        }
    }
}

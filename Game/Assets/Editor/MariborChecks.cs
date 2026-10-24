using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Boki.Editor {
public static class MariborChecks {
  static readonly Dictionary<string, Vector2[]> Paths = new Dictionary<string, Vector2[]>();
  public static void Run() {
    var scene = EditorSceneManager.OpenScene("Assets/Scenes/Maribor_Map_Test.unity", OpenSceneMode.Single);
    var json = System.IO.File.ReadAllText("Assets/Art/Maribor/paths.json");
    var temp = new List<GameObject>();
    var map = GameObject.Find("Maribor_Map");
    foreach (var mf in map.GetComponentsInChildren<MeshFilter>()) {
      if (mf.transform.IsChildOf(map.transform.Find("Maribor_Collision"))) continue;
      if (mf.sharedMesh == null || mf.GetComponentInParent<MeshCollider>() != null) continue;
      if (mf.GetComponent<Collider>() != null) continue;
      var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mc.enabled = false; temp.Add(mf.gameObject);
    }
    Physics.SyncTransforms();
    // paths.json: name|x,y;x,y;...
    int bad = 0, total = 0, stuck = 0;
    foreach (var line in json.Split('\n')) {
      if (string.IsNullOrWhiteSpace(line)) continue;
      var parts = line.Trim().Split('|'); var pts = new List<Vector3>();
      foreach (var pt in parts[1].Split(';')) { var xy = pt.Split(','); pts.Add(new Vector3(float.Parse(xy[0]), 0, float.Parse(xy[1]))); }
      var go = new GameObject("walker"); var cc = go.AddComponent<CharacterController>();
      cc.height = 1.8f; cc.radius = 0.4f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.3f; cc.slopeLimit = 50f; cc.skinWidth = 0.05f;
      go.transform.position = pts[0] + Vector3.up * 0.3f; Physics.SyncTransforms();
      int lastBad = -99; var visited = new List<(int, Vector3)>();
      for (int i = 1; i < pts.Count; i++) {
        var target = new Vector3(pts[i].x, 0, pts[i].z); int guard = 0;
        while (guard++ < 400) {
          var d = target - new Vector3(go.transform.position.x, 0, go.transform.position.z);
          if (d.magnitude < 0.3f) break;
          var before = go.transform.position;
          cc.Move(d.normalized * 0.2f + Vector3.down * 0.3f);
          Physics.SyncTransforms();
          if (guard > 380) { stuck++; Debug.Log($"CHECK STUCK {parts[0]} seg {i} at {go.transform.position} target {target}");
            var o = go.transform.position; var dir = (target - new Vector3(o.x, 0, o.z)).normalized;
            foreach (var h in Physics.CapsuleCastAll(o + Vector3.up * 0.45f, o + Vector3.up * 1.35f, 0.4f, dir, 1.0f)) if (h.collider != cc) Debug.Log($"CHECK   blocker {h.collider.name} {h.collider.GetType().Name} dist={h.distance:F2} bounds={h.collider.bounds}");
            break; }
        }
        visited.Add((i, go.transform.position));
      }
      cc.enabled = false; foreach (var tg in temp) tg.GetComponent<MeshCollider>().enabled = true; Physics.SyncTransforms();
      foreach (var (i, p) in visited) {
        // compare collision vs render ground
        var hits = Physics.RaycastAll(new Vector3(p.x, 3f, p.z), Vector3.down, 6f);
        float col = float.NaN, ren = float.NaN;
        foreach (var h in hits) { bool isCol = h.collider is BoxCollider; if (isCol) { if (float.IsNaN(col) || h.point.y > col) col = h.point.y; } else { if (float.IsNaN(ren) || h.point.y > ren) ren = h.point.y; } }
        total++;
        if (Mathf.Abs(p.y) > 1.2f || (!float.IsNaN(ren) && !float.IsNaN(col) && Mathf.Abs(ren - col) > 0.2f)) { bad++; if (i - lastBad > 0) Debug.Log($"CHECK MISMATCH {parts[0]} seg {i} pos={p} colY={col} renY={ren}"); lastBad = i; }
      }
      foreach (var tg in temp) tg.GetComponent<MeshCollider>().enabled = false;
      Debug.Log($"CHECK PATH {parts[0]} end={go.transform.position} wanted={pts[pts.Count-1]}");
      Object.DestroyImmediate(go);
    }
    Debug.Log($"CHECK DONE samples={total} mismatches={bad} stuck={stuck}");
    foreach (var t in temp) Object.DestroyImmediate(t.GetComponent<MeshCollider>());
  }
}}

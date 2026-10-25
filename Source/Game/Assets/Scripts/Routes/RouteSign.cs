using Boki.Core;
using UnityEngine;

namespace Boki.Routes
{
    // Trail-gate sign for one route: a lit wooden board on posts, a reflective route badge, the route name and
    // the gate message (Gameplay §2 names, §4 messages). Lettering and trim only light up within Boki's
    // headlight reach and fade in the scene fog, so signs obey the same darkness as the world.
    // Built at runtime on the gate's existing TextMesh object, so authored scenes need no rewiring.
    [DisallowMultipleComponent]
    public sealed class RouteSign : MonoBehaviour
    {
        private const float Width = 3.4f;
        private const float Height = 1.6f;
        private const float BadgeSize = 1.2f;
        // Board center above ground; the scene builders place the source text AuthoredHeight over the gate floor.
        private const float BoardCenter = 2.05f;
        private const float AuthoredHeight = 3.2f;
        // Overhead fallback keeps the board bottom above the follow camera's resting height.
        private const float OverheadCenter = 3.3f + Height * 0.5f;
        private const float TextLeft = -Width * 0.5f + BadgeSize + 0.45f;
        private const float TextWidth = Width * 0.5f - 0.15f - TextLeft;
        private const float MinimumReach = 3f;
        private static readonly Color Paper = new Color(0.93f, 0.89f, 0.78f);
        private static readonly Color Ink = new Color(0.04f, 0.04f, 0.04f);
        private static readonly Color Wood = new Color(0.24f, 0.17f, 0.11f);
        private static readonly Color Iron = new Color(0.16f, 0.16f, 0.17f);
        private static readonly Color Open = new Color(0.45f, 1f, 0.62f);
        private static readonly Color Chosen = new Color(0.8f, 1f, 0.88f);
        private static readonly Color Discover = new Color(1f, 0.68f, 0.2f);
        private static readonly Color Sealed = new Color(0.9f, 0.18f, 0.12f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int RevealId = Shader.PropertyToID("_Reveal");
        private static readonly string[] ForestNames = { "Strmina", "Gondola", "Rave tunel" };
        private static readonly string[] CityNames = { "Tabor", "Lent", "Podzemni prehod" };
        private static readonly string[] FeriNames = { "Glavni vhod", "Dvorišče", "Servisni vhod" };
        private static Mesh cube, quad;

        private Transform root;
        private Font font;
        private Material textMaterial, trimMaterial;
        private Renderer[] planks;
        private TextMesh letter, routeName, status;
        private GameObject padlock;
        private MaterialPropertyBlock block;
        private float reveal = -1f;
        private bool built;

        public static string RouteName(RouteLevel level, RouteChoice choice)
        {
            int index = (int)choice - 1;
            if (index < 0 || index > 2) return string.Empty;
            return (level == RouteLevel.Forest ? ForestNames : level == RouteLevel.City ? CityNames : FeriNames)[index];
        }

        // Returns false when the shared assets are missing, so the gate can keep its plain text sign.
        public bool Show(TextMesh source, BoxCollider opening, RouteLevel level, RouteChoice choice, GateState state)
        {
            if (!built && !Build(source, opening, level, choice)) return false;
            bool persistent = state == GateState.PersistentLock;
            bool sealedRoute = state == GateState.RouteLock;
            Color accent = sealedRoute ? Sealed : persistent ? Discover : state == GateState.Selected ? Chosen : Open;
            trimMaterial.SetColor(ColorId, accent);
            status.color = accent;
            status.text = sealedRoute ? "SEALED BY YOUR\nPREVIOUS PATH" : persistent ? "DISCOVER ON\nANOTHER RUN"
                : state.ToString().ToUpperInvariant();
            Fit(status, TextWidth, 0.52f);
            // ROUTE_LOCK reads as a shadowed gate; the hanging padlock marks PERSISTENT_LOCK.
            block.SetColor(BaseColorId, sealedRoute ? Wood * 0.4f : Wood);
            foreach (Renderer plank in planks) plank.SetPropertyBlock(block);
            padlock.SetActive(persistent);
            return true;
        }

        private void LateUpdate()
        {
            var lamp = RunSession.Instance != null ? RunSession.Instance.Light : null;
            if (built && lamp != null) Reveal(lamp.LampPosition, lamp.LampReach);
        }

        // Public for the Editor capture, which has no running light battery.
        public void Reveal(Vector3 lampPosition, float reach)
        {
            reach = Mathf.Max(reach, MinimumReach);
            float distance = Vector3.Distance(lampPosition, root.position);
            float value = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(reach, reach * 0.7f, distance));
            if (Mathf.Abs(value - reveal) < 0.002f) return;
            reveal = value;
            textMaterial.SetFloat(RevealId, value);
            trimMaterial.SetFloat(RevealId, value);
        }

        private bool Build(TextMesh source, BoxCollider opening, RouteLevel level, RouteChoice choice)
        {
            Shader shader = Resources.Load<Shader>("RouteSign");
            Material board = Resources.Load<Material>("RouteSignBoard");
            if (shader == null || board == null) return false;
            font = source.font != null ? source.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (cube == null) cube = PrimitiveMesh(PrimitiveType.Cube);
            if (quad == null) quad = PrimitiveMesh(PrimitiveType.Quad);
            block = new MaterialPropertyBlock();
            textMaterial = new Material(shader) { name = "Route Sign Text", mainTexture = font.material.mainTexture };
            trimMaterial = new Material(shader) { name = "Route Sign Trim", mainTexture = Texture2D.whiteTexture };
            Font.textureRebuilt += FontRebuilt;
            source.GetComponent<MeshRenderer>().enabled = false;

            root = new GameObject("Route Sign Board").transform;
            root.SetParent(transform, false);
            float halfOpening = opening != null ? opening.size.x * 0.5f * Mathf.Abs(opening.transform.lossyScale.x) : 0f;
            if (Place(halfOpening))
            {
                for (int side = -1; side <= 1; side += 2)
                    Part(board, "Post", Wood * 0.75f, new Vector3(side * (Width * 0.5f - 0.3f), (Height * 0.5f + 0.12f - BoardCenter) * 0.5f, 0.1f),
                        new Vector3(0.16f, BoardCenter + Height * 0.5f + 0.12f, 0.16f));
            }
            else
            {
                // No room beside the entrance: hang the board from a gate-wide beam, above the follow camera.
                float spread = Mathf.Max(halfOpening - 0.2f, Width * 0.5f + 0.2f);
                float beam = Height * 0.5f + 0.35f;
                for (int side = -1; side <= 1; side += 2)
                    Part(board, "Post", Wood * 0.75f, new Vector3(side * spread, (beam - OverheadCenter) * 0.5f, 0.1f),
                        new Vector3(0.18f, beam + OverheadCenter, 0.18f));
                Part(board, "Beam", Wood * 0.75f, new Vector3(0f, beam - 0.09f, 0.1f), new Vector3(spread * 2f + 0.3f, 0.18f, 0.2f));
                for (int side = -1; side <= 1; side += 2)
                    Part(board, "Hanger", Iron, new Vector3(side * Width * 0.35f, Height * 0.5f + 0.1f, 0.06f), new Vector3(0.04f, 0.2f, 0.04f));
            }
            Part(board, "Cap", Wood * 0.75f, new Vector3(0f, Height * 0.5f + 0.06f, 0.06f), new Vector3(Width + 0.2f, 0.12f, 0.3f));

            planks = new Renderer[3];
            float plankHeight = Height / 3f;
            for (int i = 0; i < 3; i++)
                planks[i] = Part(board, "Plank", Wood, new Vector3((i - 1) * 0.03f, Height * 0.5f - plankHeight * (i + 0.5f), 0.06f),
                    new Vector3(Width - (i == 1 ? 0f : 0.08f), plankHeight - 0.035f, 0.1f));

            Trim("Top Trim", new Vector3(0f, Height * 0.5f - 0.09f, -0.012f), new Vector2(Width - 0.18f, 0.07f));
            Trim("Bottom Trim", new Vector3(0f, -Height * 0.5f + 0.09f, -0.012f), new Vector2(Width - 0.18f, 0.07f));
            float badgeX = -Width * 0.5f + 0.2f + BadgeSize * 0.5f;
            Trim("Badge", new Vector3(badgeX, 0f, -0.012f), new Vector2(BadgeSize, BadgeSize));

            letter = Text("Letter", new Vector3(badgeX, 0.02f, -0.02f), TextAnchor.MiddleCenter, Ink, FontStyle.Bold);
            letter.text = choice.ToString();
            Fit(letter, BadgeSize * 0.8f, BadgeSize * 0.72f);
            routeName = Text("Route Name", new Vector3(TextLeft, 0.07f, -0.02f), TextAnchor.LowerLeft, Paper, FontStyle.Bold);
            routeName.text = RouteName(level, choice);
            Fit(routeName, TextWidth, 0.46f);
            status = Text("Status", new Vector3(TextLeft, -0.05f, -0.02f), TextAnchor.UpperLeft, Open, FontStyle.Normal);

            padlock = new GameObject("Padlock");
            padlock.transform.SetParent(root, false);
            padlock.transform.localPosition = new Vector3(Width * 0.5f - 0.55f, -Height * 0.5f - 0.32f, -0.05f);
            Part(board, "Body", Iron, new Vector3(0f, 0f, 0f), new Vector3(0.36f, 0.3f, 0.12f), padlock.transform);
            Part(board, "Shackle", Iron, new Vector3(-0.12f, 0.22f, 0f), new Vector3(0.05f, 0.18f, 0.05f), padlock.transform);
            Part(board, "Shackle", Iron, new Vector3(0.12f, 0.22f, 0f), new Vector3(0.05f, 0.18f, 0.05f), padlock.transform);
            Part(board, "Shackle", Iron, new Vector3(0f, 0.29f, 0f), new Vector3(0.29f, 0.05f, 0.05f), padlock.transform);
            Part(board, "Chain", Iron, new Vector3(0f, 0.39f, 0f), new Vector3(0.03f, 0.18f, 0.03f), padlock.transform);
            built = true;
            Reveal(root.position, MinimumReach);
            return true;
        }

        // Trailhead placement: beside the entrance at eye level, so the board never hangs over the riding line or
        // covers authored gate art. Tries both sides, then further back, then inside a wide opening; returns false
        // after placing the board overhead when nothing fits.
        private bool Place(float halfOpening)
        {
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 back = -Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Quaternion facing = Quaternion.LookRotation(-back, Vector3.up);
            float floor = transform.position.y - AuthoredHeight;
            float outside = halfOpening + 0.4f + Width * 0.5f;
            float inside = halfOpening - 0.3f - Width * 0.5f;
            if (halfOpening > 0f)
            {
                for (int step = 0; step < 3; step++)
                    for (int side = 1; side >= -1; side -= 2)
                    {
                        float offset = step == 2 ? side * inside : side * outside;
                        if (step == 2 && inside < 2.5f) continue;
                        Vector3 spot = transform.position + right * offset + back * (step == 1 ? 1.5f : 0f);
                        // Turned slightly toward the riding line, like a trail sign facing the approach.
                        Quaternion rotation = facing * Quaternion.Euler(0f, (step == 2 ? -side : side) * 14f, 0f);
                        if (!Fits(spot, rotation, floor, out float ground)) continue;
                        root.SetPositionAndRotation(new Vector3(spot.x, ground + BoardCenter, spot.z), rotation);
                        return true;
                    }
            }
            root.SetPositionAndRotation(new Vector3(transform.position.x, floor + OverheadCenter, transform.position.z), facing);
            return false;
        }

        // Standing room: level ground near the gate floor and no collider where the board and posts would stand.
        private static bool Fits(Vector3 spot, Quaternion rotation, float floor, out float ground)
        {
            ground = floor;
            if (!Physics.Raycast(spot, Vector3.down, out RaycastHit hit, AuthoredHeight + 2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                || Mathf.Abs(hit.point.y - floor) > 1f) return false;
            ground = hit.point.y;
            float top = BoardCenter + Height * 0.5f + 0.15f;
            Vector3 center = new Vector3(spot.x, ground + (top + 0.3f) * 0.5f, spot.z);
            Vector3 extents = new Vector3(Width * 0.5f + 0.15f, (top - 0.3f) * 0.5f, 0.3f);
            return !Physics.CheckBox(center, extents, rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }

        private Renderer Part(Material material, string name, Color color, Vector3 position, Vector3 size, Transform parent = null)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(parent != null ? parent : root, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            part.GetComponent<MeshFilter>().sharedMesh = cube;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
            return renderer;
        }

        private void Trim(string name, Vector3 position, Vector2 size)
        {
            var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            part.transform.localScale = new Vector3(size.x, size.y, 1f);
            part.GetComponent<MeshFilter>().sharedMesh = quad;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = trimMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private TextMesh Text(string name, Vector3 position, TextAnchor anchor, Color color, FontStyle style)
        {
            var part = new GameObject(name);
            part.transform.SetParent(root, false);
            part.transform.localPosition = position;
            var text = part.AddComponent<TextMesh>();
            text.font = font;
            text.fontSize = 96;
            text.fontStyle = style;
            text.anchor = anchor;
            text.alignment = TextAlignment.Left;
            text.color = color;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = textMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return text;
        }

        // Scales a line to its box, so long names such as "Podzemni prehod" stay on the board.
        private static void Fit(TextMesh text, float width, float height)
        {
            text.characterSize = 1f;
            Vector3 size = text.GetComponent<MeshRenderer>().localBounds.size;
            if (size.x <= 0f || size.y <= 0f) return;
            text.characterSize = Mathf.Min(width / size.x, height / size.y);
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var temporary = GameObject.CreatePrimitive(type);
            Mesh mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
            // Immediate, so its collider cannot catch the post ground rays in the same frame.
            DestroyImmediate(temporary);
            return mesh;
        }

        private void FontRebuilt(Font rebuilt)
        {
            if (rebuilt == font && textMaterial != null) textMaterial.mainTexture = font.material.mainTexture;
        }

        private void OnDestroy()
        {
            Font.textureRebuilt -= FontRebuilt;
            if (textMaterial != null) Destroy(textMaterial);
            if (trimMaterial != null) Destroy(trimMaterial);
        }
    }
}

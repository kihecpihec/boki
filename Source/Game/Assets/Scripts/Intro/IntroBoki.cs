using UnityEngine;

namespace Boki.Intro
{
    // The intro-only Boki: the neutral-pose rig plus separate face parts, so the face can act (Story §3 PROLOGUE).
    // Presentation only: IntroActing decides the pose, IntroCutscene decides when. Pose in, transforms out.
    [DisallowMultipleComponent]
    public sealed class IntroBoki : MonoBehaviour
    {
        // Every field is an absolute value, so any moment of the intro can be shown directly (skip, capture).
        public struct Pose
        {
            // Rig: Euler degrees about character-frame axes at the bind pose (+X right, +Y up, +Z forward).
            // Torso is split over spine, chest and neck. Positive X bends forward; arms and thighs raise forward with negative X.
            public Vector3 Torso;
            public Vector3 Head;
            public Vector3 UpperArmL, LowerArmL, UpperArmR, LowerArmR;
            public Vector3 ThighL, ShinL, ThighR, ShinR;

            // Face. Lids: 0 closed, ~0.35 tired slit, 1 wide. Pop: 0 flat painted-style eyes, 1 eyes on stalks, above 1 overshoot.
            public float Lids;
            public float Pop;
            public Vector2 Gaze;
            public float Brows;      // -1 low .. 1 raised
            public float BrowTilt;   // degrees, positive = worried (inner ends up)
            public float Mouth;      // 0 closed .. 1 gaping
            public float Shake;      // eye and brow jitter strength
            public float Clock;      // seconds, only feeds the jitter

            // Cues above the head, drawn facing the camera.
            public float Alert;      // scale of the "!" mark
            public float SweatTime;  // seconds since the sweat burst began; negative = none
        }

        // Face space: origin on the head axis at eye level, +Z forward, +Y up, +X the character's right. Meters.
        private const float FacePlane = 0.079f;
        private const float EyeX = 0.031f;
        private const float EyeY = 0.034f;
        private const float BrowY = 0.058f;
        private const float MouthTop = -0.02f;
        private const float MouthPlane = 0.0725f;
        private const float SweatLife = 0.9f;
        private const float Gravity = 3.2f;

        [Header("Rig")]
        [SerializeField] private Transform spine;
        [SerializeField] private Transform chest;
        [SerializeField] private Transform neck;
        [SerializeField] private Transform head;
        [SerializeField] private Transform upperArmL;
        [SerializeField] private Transform lowerArmL;
        [SerializeField] private Transform handL;
        [SerializeField] private Transform upperArmR;
        [SerializeField] private Transform lowerArmR;
        [SerializeField] private Transform thighL;
        [SerializeField] private Transform shinL;
        [SerializeField] private Transform thighR;
        [SerializeField] private Transform shinR;

        [Header("Face")]
        [Tooltip("Child of the head bone, axes aligned with the character at the bind pose.")]
        [SerializeField] private Transform face;
        [SerializeField] private Transform eyeL;
        [SerializeField] private Transform eyeR;
        [SerializeField] private Transform pupilL;
        [SerializeField] private Transform pupilR;
        [SerializeField] private Transform stalkL;
        [SerializeField] private Transform stalkR;
        [SerializeField] private Transform lashL;
        [SerializeField] private Transform lashR;
        [SerializeField] private Transform browL;
        [SerializeField] private Transform browR;
        [SerializeField] private Transform mouth;

        [Header("Cues")]
        [SerializeField] private Transform cues;
        [SerializeField] private Transform alertMark;
        [SerializeField] private Transform[] sweatDrops;

        private sealed class Joint
        {
            private readonly Transform bone;
            private readonly Quaternion restLocal;
            private readonly Quaternion restFrame;

            public Joint(Transform bone, Transform root)
            {
                this.bone = bone;
                restLocal = bone.localRotation;
                restFrame = Quaternion.Inverse(root.rotation) * bone.rotation;
            }

            // Rotates about character-frame axes however the imported bone happens to be oriented.
            public void Rotate(Vector3 euler)
                => bone.localRotation = restLocal * Quaternion.Inverse(restFrame) * Quaternion.Euler(euler) * restFrame;
        }

        private Joint jointSpine, jointChest, jointNeck, jointHead;
        private Joint jointUpperL, jointLowerL, jointUpperR, jointLowerR;
        private Joint jointThighL, jointShinL, jointThighR, jointShinR;
        private bool captured;

        public bool IsWired => spine != null && chest != null && neck != null && head != null
            && upperArmL != null && lowerArmL != null && handL != null && upperArmR != null && lowerArmR != null
            && thighL != null && shinL != null && thighR != null && shinR != null
            && face != null && eyeL != null && eyeR != null && pupilL != null && pupilR != null
            && stalkL != null && stalkR != null && lashL != null && lashR != null
            && browL != null && browR != null && mouth != null
            && cues != null && alertMark != null && sweatDrops != null && System.Array.IndexOf(sweatDrops, null) < 0;

        public Transform Head => head;
        public Transform Face => face;
        public Transform Hand => handL;

        // Body and face first; the shot camera frames them; then ShowCues orients the cues toward that camera.
        public void Apply(in Pose pose)
        {
            Capture();
            ApplyBody(pose);
            ApplyFace(pose);
        }

        // Rest rotations are read once, while the rig still holds the bind pose.
        private void Capture()
        {
            if (captured) return;
            captured = true;
            Transform root = transform;
            jointSpine = new Joint(spine, root);
            jointChest = new Joint(chest, root);
            jointNeck = new Joint(neck, root);
            jointHead = new Joint(head, root);
            jointUpperL = new Joint(upperArmL, root);
            jointLowerL = new Joint(lowerArmL, root);
            jointUpperR = new Joint(upperArmR, root);
            jointLowerR = new Joint(lowerArmR, root);
            jointThighL = new Joint(thighL, root);
            jointShinL = new Joint(shinL, root);
            jointThighR = new Joint(thighR, root);
            jointShinR = new Joint(shinR, root);
        }

        private void ApplyBody(in Pose pose)
        {
            jointSpine.Rotate(pose.Torso * 0.4f);
            jointChest.Rotate(pose.Torso * 0.45f);
            jointNeck.Rotate(pose.Torso * 0.15f);
            jointHead.Rotate(pose.Head);
            jointUpperL.Rotate(pose.UpperArmL);
            jointLowerL.Rotate(pose.LowerArmL);
            jointUpperR.Rotate(pose.UpperArmR);
            jointLowerR.Rotate(pose.LowerArmR);
            jointThighL.Rotate(pose.ThighL);
            jointShinL.Rotate(pose.ShinL);
            jointThighR.Rotate(pose.ThighR);
            jointShinR.Rotate(pose.ShinR);
        }

        private void ApplyFace(in Pose pose)
        {
            ApplyEye(eyeL, pupilL, stalkL, lashL, -1f, 0f, pose);
            ApplyEye(eyeR, pupilR, stalkR, lashR, 1f, 1.7f, pose);
            ApplyBrow(browL, -1f, pose);
            ApplyBrow(browR, 1f, pose);
            ApplyMouth(pose);
        }

        // Side is -1 for the character's left eye, +1 for the right. Rest is a flat, tired slit like the painted original.
        private void ApplyEye(Transform eye, Transform pupil, Transform stalk, Transform lash, float side, float phase, in Pose pose)
        {
            float pop = pose.Pop;
            float lids = Mathf.Clamp01(pose.Lids);
            float slit = Mathf.Clamp01(lids / 0.35f);
            float wide = Mathf.Clamp01((lids - 0.35f) / 0.65f);
            float restHeight = Mathf.Lerp(Mathf.Lerp(0f, 0.0075f, slit), 0.021f, wide);

            float rx = Mathf.LerpUnclamped(0.024f, 0.037f, pop);
            float ry = Mathf.LerpUnclamped(restHeight, 0.044f, pop);
            float rz = Mathf.LerpUnclamped(0.003f, 0.042f, pop);
            float jitter = pose.Shake * 0.0035f;
            float x = side * Mathf.LerpUnclamped(EyeX, 0.05f, pop) + jitter * Mathf.Sin(pose.Clock * 53f + phase);
            float y = EyeY + 0.008f * pop + jitter * Mathf.Sin(pose.Clock * 61f + phase * 2f);
            float z = FacePlane + 0.002f + Mathf.LerpUnclamped(0f, 0.072f, pop);

            bool closed = ry < 0.0012f;
            eye.gameObject.SetActive(!closed);
            pupil.gameObject.SetActive(!closed);
            lash.gameObject.SetActive(closed);
            lash.localPosition = new Vector3(side * EyeX, EyeY - 0.004f, FacePlane + 0.0025f);
            lash.localScale = new Vector3(0.05f, 0.0035f, 0.003f);

            eye.localPosition = new Vector3(x, y, z);
            eye.localScale = new Vector3(2f * rx, 2f * Mathf.Max(ry, 0.0012f), 2f * rz);

            // The pupil rides the front of the ball and shrinks relative to it as the eye swells.
            float gazeX = Mathf.Clamp(pose.Gaze.x, -1f, 1f) * rx * 0.45f;
            float gazeY = Mathf.Clamp(pose.Gaze.y, -1f, 1f) * ry * 0.45f;
            float surface = rz * Mathf.Sqrt(Mathf.Max(0f, 1f - Square(gazeX / rx) - Square(gazeY / Mathf.Max(ry, 0.0012f))));
            float pupilHeight = Mathf.Min(Mathf.LerpUnclamped(0.0072f, 0.0165f, pop), 1.7f * ry);
            float pupilWidth = Mathf.LerpUnclamped(0.0125f, 0.0165f, pop);
            pupil.localPosition = new Vector3(x + gazeX, y + gazeY, z + surface + 0.0005f);
            pupil.localScale = new Vector3(pupilWidth, pupilHeight, 0.004f);

            // Stalk: skin-coloured cylinder from inside the head to the eye, so the eye looks pushed out of the face.
            float start = FacePlane - 0.02f;
            float length = z - start;
            bool stalked = pop > 0.03f && length > 0.001f;
            stalk.gameObject.SetActive(stalked);
            if (stalked)
            {
                float diameter = 0.03f + 0.026f * Mathf.Clamp01(pop);
                stalk.localPosition = new Vector3(x, y, start + length * 0.5f);
                // Cylinder mesh runs along its Y; the child is turned onto Z, so the anchor scales Z as the length.
                stalk.localScale = new Vector3(diameter, diameter, length * 0.5f);
            }
        }

        private void ApplyBrow(Transform brow, float side, in Pose pose)
        {
            float lift = Mathf.LerpUnclamped(0f, 0.026f, pose.Brows) + 0.03f * pose.Pop;
            float jitter = pose.Shake * 0.003f * Mathf.Sin(pose.Clock * 47f + side);
            brow.localPosition = new Vector3(side * Mathf.LerpUnclamped(EyeX, 0.05f, pose.Pop), BrowY + lift + jitter,
                FacePlane + 0.0025f + 0.075f * pose.Pop);
            brow.localRotation = Quaternion.Euler(0f, 0f, -side * pose.BrowTilt);
            brow.localScale = new Vector3(0.052f, 0.009f, 0.008f);
        }

        private void ApplyMouth(in Pose pose)
        {
            float open = Mathf.LerpUnclamped(0.004f, 0.052f, pose.Mouth);
            float width = Mathf.LerpUnclamped(0.062f, 0.046f, pose.Mouth);
            mouth.localPosition = new Vector3(0f, MouthTop - open * 0.5f, MouthPlane + 0.0015f);
            mouth.localScale = new Vector3(width, Mathf.Max(open, 0.004f), 0.006f);
        }

        // Cues sit above the head as the camera sees it and face the camera, whichever way Boki is lying.
        public void ShowCues(in Pose pose, Quaternion billboard)
        {
            cues.SetPositionAndRotation(face.position + billboard * Vector3.up * 0.3f, billboard);
            float scale = Mathf.Max(0f, pose.Alert);
            alertMark.gameObject.SetActive(scale > 0.01f);
            alertMark.localScale = Vector3.one * scale;

            for (int i = 0; i < sweatDrops.Length; i++)
            {
                // Four drops fly off alternating temples, a beat apart, then fall under world gravity.
                float age = pose.SweatTime - i * 0.12f;
                Transform drop = sweatDrops[i];
                bool live = age > 0f && age < SweatLife;
                drop.gameObject.SetActive(live);
                if (!live) continue;
                float side = i % 2 == 0 ? -1f : 1f;
                Vector3 outward = face.right * (side * (0.3f + 0.08f * i)) + face.forward * 0.1f;
                Vector3 origin = face.position + face.right * (side * 0.085f) + face.up * 0.05f;
                drop.position = origin + (outward + Vector3.up * 0.7f) * age - Vector3.up * (0.5f * Gravity * age * age);
                float shrink = 1f - Mathf.Clamp01((age - 0.6f) / (SweatLife - 0.6f));
                drop.localScale = new Vector3(0.035f, 0.05f, 0.035f) * shrink;
                drop.rotation = billboard;
            }
        }

        private static float Square(float value) => value * value;
    }
}

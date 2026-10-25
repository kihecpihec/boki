using UnityEngine;

namespace Boki.Ending
{
    // The ending-only professor: the enemy model's rig plus a cat's ears, whiskers, nose and tongue (Story §3 victory).
    // Presentation only: EndingActing decides the pose, EndingCutscene decides when. Pose in, transforms out.
    [DisallowMultipleComponent]
    public sealed class EndingProfessor : MonoBehaviour
    {
        // Every field is an absolute value, so any moment of the ending can be shown directly (skip, capture).
        public struct Pose
        {
            // Rig: Euler degrees about character-frame axes (+X right, +Y up, +Z forward). Torso is split over spine,
            // chest and neck. Positive X bends forward; arms raise forward with negative X; arms swing outward with
            // negative Z on the left arm and positive Z on the right.
            public Vector3 Torso;
            public Vector3 Head;
            public Vector3 UpperArmL, LowerArmL, UpperArmR, LowerArmR;
            public float Scale;     // whole body, 1 = normal size

            public float Mouth;     // 0 closed (the painted mouth shows) .. 1 wide open
            public float Tongue;    // 0 hidden .. 1 reaching the forearm point named by LickAlong
            public float LickAlong; // 0 wrist .. 1 elbow of the left forearm
            public float Cat;       // 0 plain .. 1 ears and whiskers; may overshoot
            public float Twitch;    // ear and whisker flutter strength
            public float Clock;     // seconds, only feeds the flutter
        }

        private const float MaxTongue = 0.2f;
        // Face space: just under the painted mouth, which sits low on the chin.
        private static readonly Vector3 TongueStart = new Vector3(0f, -0.123f, 0.06f);

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

        [Header("Cat")]
        [Tooltip("Child of the head bone, axes aligned with the character at the bind pose.")]
        [SerializeField] private Transform face;
        [SerializeField] private Transform earL;
        [SerializeField] private Transform earR;
        [Tooltip("Three per side, left first. Anchors whose Z axis is the whisker's length.")]
        [SerializeField] private Transform[] whiskers;
        [SerializeField] private Transform nose;
        [SerializeField] private Transform mouth;
        [Tooltip("Anchor at the mouth; its Z scale is the tongue's length.")]
        [SerializeField] private Transform tongue;

        private RigJoint jointSpine, jointChest, jointNeck, jointHead;
        private RigJoint jointUpperL, jointLowerL, jointUpperR, jointLowerR;
        private Quaternion earRestL, earRestR;
        private Vector3 earScaleL, earScaleR;
        private Quaternion[] whiskerRest;
        private Vector3[] whiskerScale;
        private Vector3 mouthRestPosition, mouthRestScale;
        private bool captured;

        public bool IsWired => spine != null && chest != null && neck != null && head != null
            && upperArmL != null && lowerArmL != null && handL != null && upperArmR != null && lowerArmR != null
            && face != null && earL != null && earR != null && nose != null && mouth != null && tongue != null
            && whiskers != null && whiskers.Length == 6 && System.Array.IndexOf(whiskers, null) < 0;

        public Transform Face => face;
        public Transform Hand => handL;

        // Body first, then the cat parts, because the tongue aims at where the forearm ended up.
        public void Apply(in Pose pose)
        {
            Capture();
            transform.localScale = Vector3.one * Mathf.Max(0.01f, pose.Scale);
            ApplyBody(pose);
            ApplyEars(pose);
            ApplyWhiskers(pose);
            ApplyMouth(pose);
            ApplyTongue(pose);
        }

        // Rest values are read once, while the rig and the cat parts still hold their authored placement.
        private void Capture()
        {
            if (captured) return;
            captured = true;
            Transform root = transform;
            jointSpine = new RigJoint(spine, root);
            jointChest = new RigJoint(chest, root);
            jointNeck = new RigJoint(neck, root);
            jointHead = new RigJoint(head, root);
            jointUpperL = new RigJoint(upperArmL, root);
            jointLowerL = new RigJoint(lowerArmL, root);
            jointUpperR = new RigJoint(upperArmR, root);
            jointLowerR = new RigJoint(lowerArmR, root);
            earRestL = earL.localRotation;
            earRestR = earR.localRotation;
            earScaleL = earL.localScale;
            earScaleR = earR.localScale;
            whiskerRest = new Quaternion[whiskers.Length];
            whiskerScale = new Vector3[whiskers.Length];
            for (int i = 0; i < whiskers.Length; i++)
            {
                whiskerRest[i] = whiskers[i].localRotation;
                whiskerScale[i] = whiskers[i].localScale;
            }
            mouthRestPosition = mouth.localPosition;
            mouthRestScale = mouth.localScale;
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
        }

        // Ears grow out of the bald crown and flick when the cat is on edge.
        private void ApplyEars(in Pose pose)
        {
            float cat = Mathf.Max(0f, pose.Cat);
            bool visible = cat > 0.01f;
            earL.gameObject.SetActive(visible);
            earR.gameObject.SetActive(visible);
            if (!visible) return;
            float flickL = pose.Twitch * 16f * Mathf.Max(0f, Mathf.Sin(pose.Clock * 23f));
            float flickR = pose.Twitch * 16f * Mathf.Max(0f, Mathf.Sin(pose.Clock * 19f + 1.7f));
            earL.localRotation = earRestL * Quaternion.Euler(0f, 0f, flickL);
            earR.localRotation = earRestR * Quaternion.Euler(0f, 0f, -flickR);
            earL.localScale = earScaleL * cat;
            earR.localScale = earScaleR * cat;
        }

        private void ApplyWhiskers(in Pose pose)
        {
            float cat = Mathf.Max(0f, pose.Cat);
            bool visible = cat > 0.01f;
            nose.gameObject.SetActive(visible);
            for (int i = 0; i < whiskers.Length; i++)
            {
                whiskers[i].gameObject.SetActive(visible);
                if (!visible) continue;
                float flutter = pose.Twitch * 5f * Mathf.Sin(pose.Clock * 31f + i * 1.3f);
                whiskers[i].localRotation = whiskerRest[i] * Quaternion.Euler(flutter, 0f, 0f);
                whiskers[i].localScale = new Vector3(whiskerScale[i].x, whiskerScale[i].y, whiskerScale[i].z * cat);
            }
        }

        private void ApplyMouth(in Pose pose)
        {
            float open = Mathf.Clamp01(pose.Mouth);
            bool visible = open > 0.02f;
            mouth.gameObject.SetActive(visible);
            if (!visible) return;
            mouth.localScale = new Vector3(Mathf.Lerp(mouthRestScale.x, mouthRestScale.x * 1.35f, open),
                Mathf.Lerp(mouthRestScale.y, 0.05f, open), mouthRestScale.z);
            mouth.localPosition = mouthRestPosition + Vector3.down * (0.5f * (mouth.localScale.y - mouthRestScale.y));
        }

        // The tongue leaves the mouth and ends on the forearm point the lick is at.
        private void ApplyTongue(in Pose pose)
        {
            float out01 = Mathf.Clamp01(pose.Tongue);
            bool visible = out01 > 0.02f;
            tongue.gameObject.SetActive(visible);
            if (!visible) return;
            Vector3 start = face.TransformPoint(TongueStart);
            Vector3 target = Vector3.Lerp(handL.position, lowerArmL.position, Mathf.Lerp(0.05f, 0.55f, Mathf.Clamp01(pose.LickAlong)));
            Vector3 toward = target - start;
            float length = Mathf.Min(toward.magnitude, MaxTongue * transform.localScale.x) * out01;
            tongue.position = start;
            tongue.rotation = toward.sqrMagnitude > 1e-8f ? Quaternion.LookRotation(toward, face.up) : face.rotation;
            tongue.localScale = new Vector3(tongue.localScale.x, tongue.localScale.y, length / Mathf.Max(0.0001f, transform.localScale.x));
        }
    }
}

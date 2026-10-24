using UnityEngine;

namespace Boki.Ending
{
    // Rotates a bone about character-frame axes (+X right, +Y up, +Z forward) however the imported bone is oriented.
    // Rest rotations are read when the joint is created, so create it while the rig still holds its bind pose.
    public sealed class RigJoint
    {
        private readonly Transform bone;
        private readonly Quaternion restLocal;
        private readonly Quaternion restFrame;

        public RigJoint(Transform bone, Transform root)
        {
            this.bone = bone;
            restLocal = bone.localRotation;
            restFrame = Quaternion.Inverse(root.rotation) * bone.rotation;
        }

        public void Rotate(Vector3 euler)
            => bone.localRotation = restLocal * Quaternion.Inverse(restFrame) * Quaternion.Euler(euler) * restFrame;
    }
}

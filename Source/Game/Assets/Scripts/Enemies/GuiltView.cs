using UnityEngine;

namespace Boki.Enemies
{
    // Presentation only: Guilt_Warning while it looms, Guilt_Chase while it runs.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GuiltChaser))]
    public sealed class GuiltView : MonoBehaviour
    {
        private static readonly int Warning = Animator.StringToHash("Guilt_Warning");
        private static readonly int Chase = Animator.StringToHash("Guilt_Chase");
        [SerializeField] private Animator animator;
        private GuiltChaser guilt;

        private void OnEnable()
        {
            guilt = GetComponent<GuiltChaser>();
            if (animator == null)
            {
                Debug.LogError("Assign the Animator on GuiltView.", this);
                enabled = false;
                return;
            }
            guilt.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable() => guilt.PhaseChanged -= OnPhaseChanged;

        private void OnPhaseChanged(GuiltPhase phase)
        {
            if (phase == GuiltPhase.Warning) animator.Play(Warning, 0, 0f);
            else if (phase == GuiltPhase.Chase) animator.CrossFadeInFixedTime(Chase, 0.2f);
        }
    }
}

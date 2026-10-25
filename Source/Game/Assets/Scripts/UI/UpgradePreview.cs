using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    [DisallowMultipleComponent]
    public sealed class UpgradePreview : MonoBehaviour
    {
        [SerializeField] private UpgradeScreen screen;
        [SerializeField] private Text notice;
        [SerializeField] private Button reopen;
        private CursorLockMode previousLock;
        private bool previousVisible;

        private void OnEnable()
        {
            if (screen == null || notice == null || reopen == null)
            {
                Debug.LogError("Assign UpgradePreview screen, notice and reopen button.", this);
                enabled = false;
                return;
            }
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            screen.ChoiceRequested.AddListener(Chosen);
            reopen.onClick.AddListener(Show);
        }

        private void Start() => Show();

        private void OnDisable()
        {
            if (screen != null) screen.ChoiceRequested.RemoveListener(Chosen);
            if (reopen != null) reopen.onClick.RemoveListener(Show);
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }

        private void Show()
        {
            screen.SetCard(0, "RAVE OVERDRIVE", "+30% laser damage  (now +30% → +60%)", "UPGRADE · 1/3", false);
            screen.SetCard(1, "TURBO SKIRO", "+20% movement speed", "UPGRADE · NEW", false);
            screen.SetCard(2, "EMERGENCY SNACK", "Restore 30 HP  (80 → 100)", "RECOVERY", true);
            notice.text = "UPGRADE PREVIEW / SAMPLE OFFER";
            reopen.gameObject.SetActive(false);
            screen.ShowOffer();
        }

        private void Chosen(int index)
        {
            screen.Hide();
            notice.text = "UPGRADE PREVIEW / CHOSEN CARD " + (index + 1) + "\nPresentation only; no run stats changed.";
            reopen.gameObject.SetActive(true);
            reopen.Select();
        }
    }
}

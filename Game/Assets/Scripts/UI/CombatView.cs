using Boki.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    public sealed class CombatView : MonoBehaviour
    {
        [SerializeField] private Text healthText;
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private Button retryButton;
        public Button RetryButton => retryButton;
        public bool DeathVisible => deathPanel != null && deathPanel.activeSelf;
        public void ShowHealth(PlayerHealth health) => healthText.text = $"HP {health.Current} / {health.Maximum}";
        public void ShowDeath(bool visible) => deathPanel.SetActive(visible);
    }
}

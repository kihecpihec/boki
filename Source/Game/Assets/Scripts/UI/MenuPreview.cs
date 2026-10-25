using UnityEngine;
using UnityEngine.UI;

namespace Boki.UI
{
    // Presentation sandbox only. The gameplay integration owns pause and scene loading.
    [DisallowMultipleComponent]
    public sealed class MenuPreview : MonoBehaviour
    {
        [SerializeField] private MenuScreen screen;
        [SerializeField] private Text notice;
        private CursorLockMode previousLock;
        private bool previousVisible;

        private void OnEnable()
        {
            if (screen == null || notice == null)
            {
                Debug.LogError("Assign MenuPreview screen and notice.", this);
                enabled = false;
                return;
            }
            previousLock = Cursor.lockState;
            previousVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            screen.NewRunRequested.AddListener(PreviewRun);
            screen.RestartRequested.AddListener(PreviewRun);
            screen.ResumeRequested.AddListener(PreviewRun);
            screen.PauseRequested.AddListener(screen.ShowPauseMenu);
            screen.MainMenuRequested.AddListener(screen.ShowMainMenu);
            screen.QuitRequested.AddListener(PreviewQuit);
        }

        private void OnDisable()
        {
            if (screen != null)
            {
                screen.NewRunRequested.RemoveListener(PreviewRun);
                screen.RestartRequested.RemoveListener(PreviewRun);
                screen.ResumeRequested.RemoveListener(PreviewRun);
                screen.PauseRequested.RemoveListener(screen.ShowPauseMenu);
                screen.MainMenuRequested.RemoveListener(screen.ShowMainMenu);
                screen.QuitRequested.RemoveListener(PreviewQuit);
            }
            Cursor.lockState = previousLock;
            Cursor.visible = previousVisible;
        }

        private void PreviewRun()
        {
            screen.Hide();
            notice.text = "MENU PREVIEW / NO GAMEPLAY LOADED\nPress ESC to preview the pause menu.";
        }

        private void PreviewQuit() => notice.text = "MENU PREVIEW / QUIT REQUESTED\nExit Play Mode to close this preview.";
    }
}

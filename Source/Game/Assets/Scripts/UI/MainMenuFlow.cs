using Boki.Core;
using UnityEngine;

namespace Boki.UI
{
    [DisallowMultipleComponent]
    public sealed class MainMenuFlow : MonoBehaviour
    {
        [SerializeField] private MenuScreen menu;
        private RunSession session;

        private void OnEnable()
        {
            if (menu == null)
            {
                Debug.LogError("Assign the main MenuScreen.", this);
                enabled = false;
                return;
            }
            session = RunSession.Ensure();
            session.SetPaused(PauseReason.Menu, true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            menu.NewRunRequested.AddListener(NewRun);
            menu.QuitRequested.AddListener(Quit);
        }

        private void OnDisable()
        {
            if (menu == null) return;
            menu.NewRunRequested.RemoveListener(NewRun);
            menu.QuitRequested.RemoveListener(Quit);
        }

        private void NewRun()
        {
            session.StartV1Run();
        }

        private static void Quit() => Application.Quit();
    }
}

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Boki.UI
{
    [DisallowMultipleComponent]
    public sealed class MenuScreen : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject effects;
        [SerializeField] private Text location;
        [SerializeField] private Text subtitle;
        [SerializeField] private Text details;
        [SerializeField] private Text hint;
        [SerializeField] private Button[] buttons;
        [SerializeField] private InputActionReference navigate;
        [SerializeField] private InputActionReference submit;
        [SerializeField] private InputActionReference back;
        [SerializeField] private UnityEvent newRunRequested = new UnityEvent();
        [SerializeField] private UnityEvent resumeRequested = new UnityEvent();
        [SerializeField] private UnityEvent restartRequested = new UnityEvent();
        [SerializeField] private UnityEvent mainMenuRequested = new UnityEvent();
        [SerializeField] private UnityEvent quitRequested = new UnityEvent();
        [SerializeField] private UnityEvent pauseRequested = new UnityEvent();
        private readonly string[] mainLabels = { "NEW RUN", "UNLOCKS", "SETTINGS", "QUIT" };
        private readonly string[] pauseLabels = { "RESUME", "SETTINGS", "RESTART RUN", "QUIT TO MENU" };
        private bool paused;
        private bool visible;
        private bool settings;
        private bool unlocks;
        private int selected;
        private string runSummary = "HP  --\nBATTERY  --\nXP  --\n\nUPGRADES\n--";

        public bool IsMainMenuVisible => visible && !paused;
        public UnityEvent NewRunRequested => newRunRequested;
        public UnityEvent ResumeRequested => resumeRequested;
        public UnityEvent RestartRequested => restartRequested;
        public UnityEvent MainMenuRequested => mainMenuRequested;
        public UnityEvent QuitRequested => quitRequested;
        public UnityEvent PauseRequested => pauseRequested;

        private void OnEnable()
        {
            if (panel == null || effects == null || location == null || subtitle == null || details == null
                || hint == null || buttons == null || buttons.Length != 4
                || navigate == null || submit == null || back == null)
            {
                Debug.LogError("Assign all MenuScreen display and dedicated input references.", this);
                enabled = false;
                return;
            }
            foreach (Button button in buttons)
                if (button == null || button.GetComponentInChildren<Text>() == null)
                {
                    Debug.LogError("MenuScreen needs four buttons with Text labels.", this);
                    enabled = false;
                    return;
                }
            navigate.action.performed += Navigate;
            submit.action.performed += Submit;
            back.action.performed += Back;
            navigate.action.Enable();
            submit.action.Enable();
            back.action.Enable();
            ShowMainMenu();
        }

        private void OnDisable()
        {
            if (navigate != null) { navigate.action.performed -= Navigate; navigate.action.Disable(); }
            if (submit != null) { submit.action.performed -= Submit; submit.action.Disable(); }
            if (back != null) { back.action.performed -= Back; back.action.Disable(); }
        }

        public void ShowMainMenu() { paused = false; Show(); }
        public void ShowPauseMenu() { paused = true; Show(); }
        public void Hide() { visible = false; panel.SetActive(false); }
        public void SetRunSummary(string summary)
        {
            runSummary = summary;
            if (visible && paused && !settings && !unlocks) details.text = "RUN\n\n" + summary;
        }

        private void Show()
        {
            visible = true;
            settings = unlocks = false;
            panel.SetActive(true);
            location.text = paused ? "PAUSED" : "BELLEVUE";
            subtitle.text = paused ? "THE WORLD WAITS. <color=#c4202a>IT WON'T FOR LONG.</color>"
                : "YOU ARE LATE. <color=#c4202a>DON'T LOOK BACK.</color>";
            details.gameObject.SetActive(paused);
            details.text = "RUN\n\n" + runSummary;
            hint.text = "ENTER: SELECT\n<size=17>W/S SELECT    ESC " + (paused ? "RESUME" : "BACK") + "</size>";
            string[] labels = paused ? pauseLabels : mainLabels;
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].gameObject.SetActive(true);
                buttons[i].GetComponentInChildren<Text>().text = labels[i];
            }
            Select(0);
        }

        public void Activate(int index)
        {
            if (!visible) return;
            if (settings || unlocks)
            {
                if (settings && index == 0)
                {
                    effects.SetActive(!effects.activeSelf);
                    buttons[0].GetComponentInChildren<Text>().text = "SCREEN EFFECTS: " + (effects.activeSelf ? "ON" : "OFF");
                }
                else Show();
                return;
            }
            if (index == 0) { if (paused) resumeRequested.Invoke(); else newRunRequested.Invoke(); }
            else if (index == 1 && !paused) ShowSubpage(false);
            else if (index == 1 || (index == 2 && !paused)) ShowSubpage(true);
            else if (index == 2) restartRequested.Invoke();
            else if (index == 3) { if (paused) mainMenuRequested.Invoke(); else quitRequested.Invoke(); }
        }

        private void ShowSubpage(bool showSettings)
        {
            settings = showSettings;
            unlocks = !showSettings;
            location.text = settings ? "SETTINGS" : "UNLOCKS";
            details.gameObject.SetActive(true);
            details.text = settings ? "PRESENTATION\n\nToggle the menu screen effects.\nGameplay settings connect later."
                : "PERMANENT UNLOCKS\n\nConnect saved route unlocks here\nduring gameplay integration.";
            for (int i = 0; i < buttons.Length; i++) buttons[i].gameObject.SetActive(i < (settings ? 2 : 1));
            buttons[0].GetComponentInChildren<Text>().text = settings
                ? "SCREEN EFFECTS: " + (effects.activeSelf ? "ON" : "OFF") : "BACK";
            buttons[1].GetComponentInChildren<Text>().text = "BACK";
            hint.text = "ENTER: SELECT\n<size=17>W/S SELECT    ESC BACK</size>";
            Select(0);
        }

        private void Select(int index)
        {
            selected = index;
            buttons[index].Select();
        }

        private void Navigate(InputAction.CallbackContext context)
        {
            if (!visible) return;
            int count = settings ? 2 : unlocks ? 1 : 4;
            Select((selected + (context.ReadValue<float>() > 0f ? -1 : 1) + count) % count);
        }

        private void Submit(InputAction.CallbackContext context)
        {
            if (!visible) return;
            GameObject current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i].gameObject == current) { Activate(i); return; }
            Activate(selected);
        }

        private void Back(InputAction.CallbackContext context)
        {
            if (!visible) pauseRequested.Invoke();
            else if (settings || unlocks) Show();
            else if (paused) resumeRequested.Invoke();
        }
    }
}

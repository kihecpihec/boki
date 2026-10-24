using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Boki.UI
{
    [DisallowMultipleComponent]
    public sealed class UpgradeScreen : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private GameObject effects;
        [SerializeField] private Button[] buttons;
        [SerializeField] private Text[] titles;
        [SerializeField] private Text[] descriptions;
        [SerializeField] private Text[] tags;
        [SerializeField] private RectTransform selectionArrow;
        [SerializeField] private InputActionReference navigate;
        [SerializeField] private InputActionReference submit;
        [SerializeField] private InputActionReference[] quickSelect;
        [SerializeField] private UnityEvent<int> choiceRequested = new UnityEvent<int>();
        private int selected;
        private bool ready;
        private bool submitting;
        private int offerCount = 3;
        private int submittedFrame = -1;

        public bool IsVisible => ready && panel.activeSelf;
        public UnityEvent<int> ChoiceRequested => choiceRequested;
        public bool ScreenEffectsEnabled { get => effects.activeSelf; set => effects.SetActive(value); }

        private void OnEnable()
        {
            ready = panel != null && effects != null && selectionArrow != null
                && navigate != null && submit != null && HasThree(buttons) && HasThree(titles)
                && HasThree(descriptions) && HasThree(tags) && HasThree(quickSelect);
            if (!ready)
            {
                Debug.LogError("Assign all UpgradeScreen display and dedicated input references.", this);
                enabled = false;
                return;
            }
            navigate.action.performed += Navigate;
            submit.action.performed += Submit;
            for (int i = 0; i < quickSelect.Length; i++)
            {
                quickSelect[i].action.performed += QuickSelect;
                quickSelect[i].action.Enable();
            }
            navigate.action.Enable();
            submit.action.Enable();
            Hide();
        }

        private static bool HasThree<T>(T[] items) where T : Object
            => items != null && items.Length == 3 && items[0] != null && items[1] != null && items[2] != null;

        private void OnDisable()
        {
            if (navigate != null) { navigate.action.performed -= Navigate; navigate.action.Disable(); }
            if (submit != null) { submit.action.performed -= Submit; submit.action.Disable(); }
            if (quickSelect != null)
                foreach (var reference in quickSelect)
                    if (reference != null) { reference.action.performed -= QuickSelect; reference.action.Disable(); }
            if (panel != null) panel.SetActive(false);
            ready = false;
        }

        // The progression adapter supplies eligible offers and accepts or rejects the choice.
        public void SetCard(int index, string title, string description, string tag, bool recovery)
        {
            if (index < 0 || index >= 3) throw new System.ArgumentOutOfRangeException(nameof(index));
            titles[index].text = title;
            titles[index].color = recovery ? new Color32(185, 194, 122, 255) : new Color32(216, 208, 192, 255);
            descriptions[index].text = description;
            tags[index].text = tag;
        }

        public void ShowOffer()
        {
            if (!ready) return;
            panel.SetActive(true);
            Select(0);
        }

        public void SetOfferSize(int count)
        {
            if (count < 1 || count > 3) throw new System.ArgumentOutOfRangeException(nameof(count));
            offerCount = count;
            for (int i = 0; i < buttons.Length; i++) buttons[i].gameObject.SetActive(i < count);
        }

        public void Hide()
        {
            panel.SetActive(false);
            if (EventSystem.current != null)
                for (int i = 0; i < buttons.Length; i++)
                    if (EventSystem.current.currentSelectedGameObject == buttons[i].gameObject)
                        EventSystem.current.SetSelectedGameObject(null);
        }

        public void RequestChoice(int index)
        {
            if (!IsVisible || submitting || submittedFrame == Time.frameCount || index < 0 || index >= offerCount) return;
            Select(index);
            submittedFrame = Time.frameCount;
            submitting = true;
            try { choiceRequested.Invoke(index); }
            finally { submitting = false; }
        }

        private void Select(int index)
        {
            selected = index;
            selectionArrow.anchoredPosition = new Vector2(896, -284 - index * 126);
            buttons[index].Select();
        }

        private void Navigate(InputAction.CallbackContext context)
        {
            if (IsVisible) Select((selected + (context.ReadValue<float>() > 0 ? 1 : offerCount - 1)) % offerCount);
        }

        private void Submit(InputAction.CallbackContext context)
        {
            if (!IsVisible) return;
            for (int i = 0; i < buttons.Length; i++)
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == buttons[i].gameObject)
                { RequestChoice(i); return; }
            RequestChoice(selected);
        }

        private void QuickSelect(InputAction.CallbackContext context)
        {
            for (int i = 0; i < quickSelect.Length; i++)
                if (context.action == quickSelect[i].action) { RequestChoice(i); return; }
        }
    }
}

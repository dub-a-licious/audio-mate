using System.Collections.Generic;
using AudioMate.Extension;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AudioMate.UI
{
    public class HorizontalButtonGroup : MonoBehaviour
    {

        private readonly List<UIDynamicButton> _buttons;
        private GameObject _container;
        private HorizontalLayoutGroup gridLayout;

        public RectOffset Padding
        {
            set { gridLayout.padding = value; }
        }

        public HorizontalButtonGroup()
        {
            _buttons = new List<UIDynamicButton>();
            Init();
        }

        public void Init()
        {
            _container = new GameObject($"HorizontalButtonGroup{Tools.GenerateID()}");
            _container.transform.SetParent(transform, false);

            gridLayout = _container.AddComponent<HorizontalLayoutGroup>();
            gridLayout.spacing = 10f;
            gridLayout.childForceExpandWidth = false;
            gridLayout.childForceExpandHeight = true;
            gridLayout.childControlWidth = true;
            gridLayout.childControlHeight = false;
            gridLayout.childAlignment = TextAnchor.MiddleCenter;
        }

        public UIDynamicButton CreateButton(string label, string style = Styles.Default, UnityAction call = null, bool addOutline = false, float flexibleWidth = 1f)
        {
            var instance = Instantiate(VamPrefabFactory.ButtonPrefab, transform, false);
            instance.transform.SetParent(_container.transform, false);
            var btn = instance.GetComponent<UIDynamicButton>();
            btn.label = label;
            btn.textColor = Styles.Text(style);
            btn.buttonColor = Styles.Bg(style);
            if (call != null) btn.button.onClick.AddListener(call);
            if (addOutline)
            {
                var outline = btn.button.gameObject.AddComponent<Outline>().GetComponent<Outline>();
                outline.effectColor = Color.white;
                outline.effectDistance = new Vector2(3f, 3f);
            }

            var layout = instance.GetComponent<LayoutElement>();
            layout.preferredWidth = 0f;
            layout.flexibleWidth = flexibleWidth;
            _buttons.Add(btn);
            return btn;
        }

        public Text CreateLabel(string text, string style = Styles.Default, bool invertStyle = true, int size = 32, bool bold = true)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(_container.transform, false);
            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 0f;
            layout.flexibleWidth = 1f;
            layout.flexibleHeight = 1f;

            layout.minWidth = 20f;

            var label = go.AddComponent<Text>();
            label.text = text;
            label.color = invertStyle ? Styles.Bg(style) : Styles.Text(style);
            label.font = VamPrefabFactory.BaseFont;
            label.fontSize = size;
            label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            return label;
        }

        public UIDynamicButton GetButtonAt(int index)
        {
            return _buttons[index];
        }
    }
}

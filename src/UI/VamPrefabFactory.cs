using System;
using System.Collections;
using System.Collections.Generic;
using AssetBundles;
using AudioMate.Extension;
using UnityEngine;
using UnityEngine.UI;

namespace AudioMate.UI
{
    /// <summary>
    /// Base code shamelessly ripped from the incredible Timline plugin (https://github.com/acidbubbles/vam-timeline)
    /// Shout outs to Acidbubbles!
    /// </summary>
    public class VamPrefabFactory : MonoBehaviour
    {
        public static RectTransform ScrollbarPrefab;
        public static RectTransform ButtonPrefab;

        public static readonly Font BaseFont = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");

        public static IEnumerator LoadUIAssets()
        {
            foreach (var x in LoadUIAsset("z_ui2", "DynamicTextField", prefab => ScrollbarPrefab = prefab.GetComponentInChildren<ScrollRect>().verticalScrollbar.gameObject.GetComponent<RectTransform>())) yield return x;
            foreach (var x in LoadUIAsset("z_ui2", "DynamicButton", prefab => ButtonPrefab = prefab)) yield return x;
        }

        private static IEnumerable LoadUIAsset(string assetBundleName, string assetName, Action<RectTransform> assign)
        {
            var request = AssetBundleManager.LoadAssetAsync(assetBundleName, assetName, typeof(GameObject));
            if (request == null) throw new NullReferenceException($"Request for {assetName} in {assetBundleName} assetbundle failed: Null request.");
            yield return request;
            var go = request.GetAsset<GameObject>();
            if ((UnityEngine.Object) go == (UnityEngine.Object) null) throw new NullReferenceException($"Request for {assetName} in {assetBundleName} assetbundle failed: Null GameObject.");
            var prefab = go.GetComponent<RectTransform>();
            if ((UnityEngine.Object) prefab == (UnityEngine.Object) null) throw new NullReferenceException($"Request for {assetName} in {assetBundleName} assetbundle failed: Null RectTansform.");
            assign(prefab);
        }

        public static RectTransform CreateScrollRect(GameObject gameObject)
        {
            var scrollView = CreateScrollView(gameObject);
            var viewport = CreateViewport(scrollView);
            var content = CreateContent(viewport);
            var scrollbar = CreateScrollbar(scrollView);
            var scrollRect = scrollView.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = content.GetComponent<RectTransform>();
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            return content.GetComponent<RectTransform>();
        }

        private static GameObject CreateScrollView(GameObject parent)
        {
            var go = new GameObject("Scroll View");
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            return go;
        }

        private static Scrollbar CreateScrollbar(GameObject scrollView)
        {
            var vs = Instantiate(ScrollbarPrefab, scrollView.transform, false);
            return vs.GetComponent<Scrollbar>();
        }

        private static GameObject CreateViewport(GameObject scrollView)
        {
            var go = new GameObject("Viewport");
            go.transform.SetParent(scrollView.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();
            rect.pivot = new Vector2(0, 1);

            var image = go.AddComponent<Image>();
            image.raycastTarget = true;

            var mask = go.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            return go;
        }

        private static GameObject CreateContent(GameObject viewport)
        {
            var go = new GameObject("Content");
            go.transform.SetParent(viewport.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchTop();
            rect.pivot = new Vector2(0, 1);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.UpperLeft;

            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return go;
        }

        public AudioMateController controller;
        private readonly List<JSONStorableParam> _storables = new List<JSONStorableParam>();

        public VamPrefabFactory()
        {
        }

        private static string PrepareLabelStyle(string label)
        {
            //return label.ToUpper();
            return label;
        }

        public UIDynamic CreateSpacer()
        {
            var ui = Instantiate(controller.Manager.configurableSpacerPrefab).GetComponent<UIDynamic>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.height = 20f;
            return ui;
        }

        public Text CreateHeader(string headline, int level, string subHeadline = null, float height = 60f)
        {
            var headerUI = CreateSpacer();
            headerUI.height = subHeadline == null ? height : height - 40f;

            var text = headerUI.gameObject.AddComponent<Text>();
            text.text = PrepareLabelStyle(headline);
            text.font = BaseFont;
            switch (level)
            {
                case 1:
                    text.fontSize = 32;
                    text.fontStyle = FontStyle.Bold;
                    text.color = Color.white;
                    break;
                case 2:
                    text.fontSize = 24;
                    text.fontStyle = FontStyle.Bold;
                    text.color = Styles.DefaultBg;
                    break;
            }

            if (subHeadline != null) CreateHeader(subHeadline, 2, null, 40f);

            return text;
        }

        public UIDynamicSlider CreateSlider(JSONStorableFloat jsf, float scale = 1.0f, bool showQuickButtons = false)
        {
            RegisterStorable(jsf);
            var prefab = controller.Manager.configurableSliderPrefab;
            prefab.localScale = new Vector3(scale, scale);
            var ui = Instantiate(controller.Manager.configurableSliderPrefab).GetComponent<UIDynamicSlider>();
            ui.gameObject.transform.SetParent(transform, false);
            var rectTransform = ui.gameObject.GetComponent<RectTransform>();
            rectTransform.pivot = new Vector2(0, 0.5f);
            var image = rectTransform.GetChild(0).gameObject.GetComponent<Image>();
            image.color = Styles.DefaultBg;
            ui.labelText.color = Styles.DefaultText;
            //controller.DebugObject(ui.gameObject);
            ui.Configure(jsf.name, jsf.min, jsf.max, jsf.val, jsf.constrained, "F2", showQuickButtons, !jsf.constrained);
            jsf.slider = ui.slider;
            return ui;
        }

        public UIDynamicButton CreateButton(string label, string style = Styles.Default, bool addOutline = false)
        {
            var ui = Instantiate(controller.Manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
            ui.buttonColor = Styles.Bg(style);
            ui.textColor = Styles.Text(style);
            ui.buttonText.fontSize = 22;
            var outline = ui.button.gameObject.AddComponent<Outline>().GetComponent<Outline>();
            outline.effectColor = addOutline ? new Color(0f,0f,0f,0.5f) : new Color(0f,0f,0f,0f);
            outline.effectDistance = new Vector2(-2f,-2f);
            outline.enabled = addOutline;
            ui.gameObject.transform.SetParent(transform, false);
            ui.label = PrepareLabelStyle(label.ToUpper());
            return ui;
        }

        public UIDynamicToggle CreateToggle(JSONStorableBool jsb, string style = Styles.Default)
        {
            RegisterStorable(jsb);
            var ui = Instantiate(controller.Manager.configurableTogglePrefab).GetComponent<UIDynamicToggle>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.backgroundColor = Styles.Bg(style);
            ui.textColor = Styles.Text(style);
            ui.label = jsb.name;
            ui.toggle.graphic.color = Styles.Bg(style);
            jsb.toggle = ui.toggle;
            return ui;
        }

        public UIDynamicTextField CreateTextField(JSONStorableString jss)
        {
            RegisterStorable(jss);
            var ui = Instantiate(controller.Manager.configurableTextFieldPrefab).GetComponent<UIDynamicTextField>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.UItext.fontSize = 28;
            ui.backgroundColor = Styles.EmbeddedBg;
            ui.textColor = Styles.EmbeddedText;
            ui.backgroundImage.fillMethod = Image.FillMethod.Radial180;
            jss.dynamicText = ui;
            return ui;
        }

        public UIDynamicPopup CreatePopup(JSONStorableStringChooser jsc, bool filterable)
        {
            RegisterStorable(jsc);
            Transform prefab;
            #if(VAM_GT_1_20)
            if(filterable)
                prefab = controller.manager.configurableFilterablePopupPrefab;
            else
            #endif
                prefab = controller.Manager.configurableScrollablePopupPrefab;
            var ui = Instantiate(prefab).GetComponent<UIDynamicPopup>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.labelTextColor = Styles.DefaultText;
            ui.popup.backgroundImage.color = Styles.DefaultBg;
            ui.label = jsc.name;
            ui.popup.label = jsc.val;
            jsc.popup = ui.popup;
            return ui;
        }

        public UIDynamicTextField CreateTextInput(
            JSONStorableString jss,
            bool hasLabel = false,
            InputField.ContentType contentType = InputField.ContentType.Alphanumeric)
        {
            RegisterStorable(jss);

            var container = new GameObject();
            container.transform.SetParent(transform, false);
            {
                var rect = container.AddComponent<RectTransform>();
                rect.pivot = new Vector2(0, 1);

                var layout = container.AddComponent<LayoutElement>();
                layout.preferredHeight = 22f;
                layout.flexibleHeight = 1f;
                layout.flexibleWidth = 1f;
            }

            var textfield = Instantiate(controller.Manager.configurableTextFieldPrefab).GetComponent<UIDynamicTextField>();
            textfield.gameObject.transform.SetParent(container.transform, false);
            {
                jss.dynamicText = textfield;

                textfield.backgroundColor = Styles.InputBg;
                textfield.textColor = Styles.InputText;
                var input = textfield.gameObject.AddComponent<InputField>();
                textfield.UItext.resizeTextForBestFit = true;
                textfield.UItext.fontSize = 42;
                textfield.UItext.alignment = TextAnchor.LowerLeft;
                textfield.UItext.alignByGeometry = true;
                input.textComponent = textfield.UItext;
                input.contentType = contentType;
                jss.inputField = input;
                input.lineType = InputField.LineType.SingleLine;
                //input.textComponent.alignment = TextAnchor.LowerLeft;
                var rect = textfield.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0, 0);
                rect.anchoredPosition = new Vector2(0, 0);
                rect.sizeDelta = new Vector2(0, 22f);
                var layout = textfield.GetComponent<LayoutElement>();
                Destroy(layout);
            }
            if (!hasLabel) return textfield;
            var title = new GameObject();
            title.transform.SetParent(container.transform, false);
            {
                var rect = title.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(0, 1f);
                rect.sizeDelta = new Vector2(0, 30f);

                var text = title.AddComponent<Text>();
                text.font = textfield.UItext.font;
                text.text = jss.name;
                text.fontSize = 24;
                text.color = Styles.DefaultBg;
            }
            return textfield;
        }

        public void OnDestroy()
        {
            var clone = new JSONStorableParam[_storables.Count];
            _storables.CopyTo(clone);
            _storables.Clear();
            foreach (var component in clone)
            {
                if (component == null) continue;
                if (component is JSONStorableStringChooser)
                    RemovePopup((JSONStorableStringChooser)component);
                else if (component is JSONStorableFloat)
                    RemoveSlider((JSONStorableFloat)component);
                else if (component is JSONStorableString)
                    RemoveTextField((JSONStorableString)component);
                else if (component is JSONStorableBool)
                    RemoveToggle((JSONStorableBool)component);
                else
                    SuperController.LogError($"AudioMate: Cannot remove component {component}");
            }
        }

        public void RemovePopup(JSONStorableStringChooser jsc, UIDynamicPopup component = null)
        {
            if (jsc.popup != null) { jsc.popup = null; _storables.Remove(jsc); }
            if (component != null) Destroy(component.gameObject);
        }

        public void RemoveSlider(JSONStorableFloat jsf, UIDynamicSlider component = null)
        {
            if (jsf.slider != null) { jsf.slider = null; _storables.Remove(jsf); }
            if (component != null) Destroy(component.gameObject);
        }

        public void RemoveTextField(JSONStorableString jss, UIDynamicTextField component = null)
        {
            if (jss.dynamicText != null) { jss.dynamicText = null; _storables.Remove(jss); }
            if (component != null) Destroy(component.gameObject);
        }

        public void RemoveToggle(JSONStorableBool jsb, UIDynamicToggle component = null)
        {
            if (jsb.toggle != null) { jsb.toggle = null; _storables.Remove(jsb); }
            if (component != null) Destroy(component.gameObject);
        }

        private T RegisterStorable<T>(T v)
            where T : JSONStorableParam
        {
            _storables.Add(v);
            ValidateStorableFreeToBind(v);
            return v;
        }

        private void ValidateStorableFreeToBind(JSONStorableParam v)
        {
            if (v is JSONStorableStringChooser)
            {
                if (((JSONStorableStringChooser)v).popup != null)
                    SuperController.LogError($"Storable {v.name} of atom {controller.containingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableFloat)
            {
                if (((JSONStorableFloat)v).slider != null)
                    SuperController.LogError($"Storable {v.name} of atom {controller.containingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableString)
            {
                if (((JSONStorableString)v).inputField != null)
                    SuperController.LogError($"Storable {v.name} of atom {controller.containingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableBool)
            {
                if (((JSONStorableBool)v).toggle != null)
                    SuperController.LogError($"Storable {v.name} of atom {controller.containingAtom.name} was not correctly unregistered.");
            }
        }
    }
}

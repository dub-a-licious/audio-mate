using System;
using System.Collections.Generic;
using System.Linq;
using AudioMate.Extension;
using uFileBrowser;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AudioMate.UI
{
    [RequireComponent(typeof(UIDynamicButton))]
    public class UIManager : MonoBehaviour
    {
        public static UIManager AddTo(Transform transform)
        {
            var go = new GameObject();
                go.transform.SetParent(transform, false);

                var rect = go.AddComponent<RectTransform>();
                rect.StretchTop();
                rect.sizeDelta = new Vector2(0, 750);
                rect.pivot = new Vector2(0, 1);

                var rows = go.AddComponent<VerticalLayoutGroup>();

                var panels = new GameObject();
                panels.transform.SetParent(go.transform, false);

                var panelsGroup = panels.AddComponent<HorizontalLayoutGroup>();
                panelsGroup.spacing = 10f;
                panelsGroup.childControlWidth = true;
                panelsGroup.childForceExpandWidth = false;
                panelsGroup.childControlHeight = true;
                panelsGroup.childForceExpandHeight = false;

                var leftPanel = CreatePanel(panels.transform, 0f, 1f);
                var rightPanel = CreatePanel(panels.transform, 425f, 0f);

                var ui = go.AddComponent<UIManager>();
                ui.leftPanel = leftPanel;
                ui.rightPanel = rightPanel;
                return ui;
        }

        private static GameObject CreatePanel(Transform transform, float preferredWidth, float flexibleWidth)
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1f);

            var layout = go.AddComponent<LayoutElement>();
            layout.minWidth = 0;
            layout.preferredWidth = preferredWidth;
            layout.flexibleWidth = flexibleWidth;
            layout.minHeight = 100;

            var verticalLayoutGroup = go.AddComponent<VerticalLayoutGroup>();
            verticalLayoutGroup.childControlWidth = true;
            verticalLayoutGroup.childForceExpandWidth = true;
            verticalLayoutGroup.childControlHeight = true;
            verticalLayoutGroup.childForceExpandHeight = false;
            verticalLayoutGroup.spacing = 10f;


            return go;
        }

        public AudioMateController controller;
        public AudioMateCollectionManager Collections;
        public JSONStorableStringChooser ReceiverAtomJSON;
        public JSONStorableStringChooser ReceiverNodeJSON;

        private JSONStorableString _collectionNameJSON;
        private JSONStorableString _collectionContentJSON;

        private UIDynamicPopup _popupCollectionList;

        private UIDynamicTextField _activeCollectionClipListText;

        private AudioMateClipCollection _activeCollection;
        private JSONStorableBool _toggleOnlyIfClearJSON;
        private JSONStorableBool _toggleAlwaysQueueJSON;
        private JSONStorableBool _toggleShuffleJSON;
        private JSONStorableFloat _sliderPlayChanceJSON;

        public GameObject leftPanel;
        public GameObject rightPanel;

        private VamPrefabFactory _leftUI;
        private VamPrefabFactory _rightUI;
        public ClipLibrary clipLibrary;

        private HorizontalButtonGroup _btnGroupAddClips;
        private UIDynamicButton _btnClear;
        private UIDynamicButton _btnPlayRandomClip;
        private UIDynamicButton _btnQueueRandomClip;

        public void Init(AudioMateController mainController)
        {
            if ((UnityEngine.Object) mainController == (UnityEngine.Object) null)
            {
                SuperController.LogError("AudioMate: Error during UI initialization. Controller or Collection Manager are set to null.");
                return;
            }
            controller = mainController;
            Log("### Init ###");
            _leftUI = leftPanel.AddComponent<VamPrefabFactory>();
            _leftUI.controller = controller;

            _rightUI = rightPanel.AddComponent<VamPrefabFactory>();
            _rightUI.controller = controller;
        }

        public void Bind(AudioMateCollectionManager collectionManager)
        {
            if ((UnityEngine.Object) collectionManager == (UnityEngine.Object) null)
            {
                SuperController.LogError("AudioMate: Error during bind. collectionManager is null.");
                return;
            }
            Log("### Bind ###");
            Collections = collectionManager;
            _activeCollection = Collections.ActiveCollection;
            clipLibrary.Bind();
            Collections.OnActiveCollectionSelected.AddListener(OnActiveCollectionSelected);
            Collections.OnActiveCollectionUpdated.AddListener(OnActiveCollectionUpdated);
            Collections.OnActiveCollectionSelected.AddListener(clipLibrary.OnActiveCollectionSelected);
            Collections.OnActiveCollectionUpdated.AddListener(clipLibrary.OnActiveCollectionUpdated);
            _popupCollectionList.popup.onOpenPopupHandlers += Collections.SyncCollectionNames;
            _popupCollectionList.popup.enabled = true;
            _btnGroupAddClips.GetButtonAt(0)?.button.onClick.AddListener(Collections.Add10ClipsToActiveCollection);
            _btnGroupAddClips.GetButtonAt(1)?.button.onClick.AddListener(Collections.Add20ClipsToActiveCollection);
            _btnGroupAddClips.GetButtonAt(2)?.button.onClick.AddListener(Collections.AddAllClipsToActiveCollection);
            _btnClear.button.onClick.AddListener(Collections.ClearActiveCollection);
            _btnPlayRandomClip.button.onClick.AddListener(() =>
            {
                Collections.PlayRandomClipAction();
            });
            _btnQueueRandomClip.button.onClick.AddListener(() => Collections.QueueRandomClipAction());
            Collections.SyncCollectionNames();
            clipLibrary.RefreshUI();
            Collections.SelectActiveCollection();
        }

        #region UI
        public void InitUI()
        {
            if ((UnityEngine.Object) controller == (UnityEngine.Object) null) return;

            //InitHeaderUI(); // TODO Costs too much UI space. Maybe I could add a splash screen or something like that.
            InitImportUI();
            InitAudioReceiverUI();
            InitActiveCollectionUI();
            InitTriggerUI();
            InitPlaybackOptionsUI();
            InitAddToCollectionControlsUI();
            InitClipLibraryUI();
            //InitDebugging();
            controller.OnUIInitialized();
        }

        private void InitHeaderUI()
        {
            _leftUI.CreateHeader("audiomate", 1, null, 40f);

        }

        private void InitAudioReceiverUI()
        {
            if (ReceiverAtomJSON == null || ReceiverNodeJSON == null) return;

            var receiverAtomUI = _leftUI.CreatePopup(ReceiverAtomJSON, true);
            receiverAtomUI.popup.onOpenPopupHandlers += SyncReceiverAtomChoices;
            SyncReceiverAtomChoices();

            var receiverNodeUI = _leftUI.CreatePopup(ReceiverNodeJSON, true);
            receiverNodeUI.popup.onOpenPopupHandlers += SyncReceiverNodeChoices;
            SyncReceiverNodeChoices();
        }
        private void SyncReceiverAtomChoices()
        {
            var choices = new List<string> {"None"};

            choices.AddRange(from atom in SuperController.singleton.GetAtoms()
                where atom != null
                where atom.category == "People" || atom.category == "Sound"
                select atom.name);

            ReceiverAtomJSON.choices = choices;
        }

        private void SyncReceiverNodeChoices()
        {
            var choices = new List<string> {"None"};

            if (ReceiverAtomJSON == null || string.IsNullOrEmpty(ReceiverAtomJSON?.val) ||
                ReceiverAtomJSON?.val == "None") return;

            // Get ReceiverAtom receivers
            var receivers = SuperController.singleton.GetAtomByUid(ReceiverAtomJSON.val)?.GetStorableIDs();

            if (receivers == null) return;

            choices.AddRange(receivers);

            ReceiverNodeJSON.choices = choices;
        }

        private void InitActiveCollectionUI()
        {
            var btnGroup = rightPanel.AddComponent<HorizontalButtonGroup>();
            btnGroup.CreateButton("New", Styles.Success,
                () => Collections.AddCollection());
            _btnClear = btnGroup.CreateButton("Clear", Styles.Danger);
            btnGroup.CreateButton("Delete", Styles.Danger,
                () => Collections.RemoveActiveCollection());

            _popupCollectionList = _rightUI.CreatePopup(controller.CollectionsJSON, true);
            _rightUI.CreateSpacer().height = 35f;
            _collectionNameJSON = new JSONStorableString("Collection Name", "", UpdateCollectionName);
            _rightUI.CreateTextInput(_collectionNameJSON);
            _collectionContentJSON = new JSONStorableString("Collection Content", "", UpdateCollectionContent);
            _activeCollectionClipListText = _rightUI.CreateTextField(_collectionContentJSON);
            _activeCollectionClipListText.height = 325f;
            var btnGroupPreview = rightPanel.AddComponent<HorizontalButtonGroup>();
            _btnPlayRandomClip = btnGroupPreview.CreateButton("\u25B6", Styles.Success, null, true);
            _btnQueueRandomClip = btnGroupPreview.CreateButton("Queue", Styles.Success, null, true);
        }

        private void UpdateCollectionName(string val)
        {
            if (string.IsNullOrEmpty(val))
            {
                _collectionNameJSON.valNoCallback = _activeCollection.Name;
                return;
            }

            Collections.SetActiveCollectionName(val);
        }

        private void UpdateCollectionContent(string val)
        {
            _activeCollectionClipListText.text = _activeCollection.ToString();
        }

        private void InitAddToCollectionControlsUI()
        {
            _btnGroupAddClips = leftPanel.AddComponent<HorizontalButtonGroup>();
            _btnGroupAddClips.CreateLabel("Add");
            _btnGroupAddClips.CreateButton("10");
            _btnGroupAddClips.CreateButton("20");
            _btnGroupAddClips.CreateButton("All");
            _btnGroupAddClips.CreateLabel("\u02C3");
        }

        private void InitImportUI()
        {
            var btnGroup = leftPanel.AddComponent<HorizontalButtonGroup>();
            //btnGroup.Padding = new RectOffset(0, 0, 0, -15);
            btnGroup.CreateButton("Import File", Styles.Default, controller.fileManager.OpenImportFileDialog);
            btnGroup.CreateButton("Import Folder", Styles.Default, controller.fileManager.OpenImportFolderDialog);
        }

        private void InitPlaybackOptionsUI()
        {
            _sliderPlayChanceJSON = new JSONStorableFloat("Play Chance", 1f, (float val) =>
            {
                _activeCollection.PlayChance = val;
            }, 0f, 1f)
            {
                isStorable = false
            };
            _rightUI.CreateSlider(_sliderPlayChanceJSON, 0.945f);
            _toggleOnlyIfClearJSON = new JSONStorableBool("Play Only If Clear", false, (bool val) =>
            {
                _activeCollection.OnlyIfClear = val;
            })
            {
                isStorable = false
            };
            _rightUI.CreateToggle(_toggleOnlyIfClearJSON);

            _toggleAlwaysQueueJSON = new JSONStorableBool("Always Queue", false, (bool val) =>
            {
                _activeCollection.AlwaysQueue = val;
            })
            {
                isStorable = false
            };
            _rightUI.CreateToggle(_toggleAlwaysQueueJSON);

            _toggleShuffleJSON = new JSONStorableBool("Shuffle Mode", true, (bool val) =>
            {
                _activeCollection.ShuffleMode = val;
            })
            {
                isStorable = false
            };
            _rightUI.CreateToggle(_toggleShuffleJSON);
        }

        private void InitTriggerUI()
        {
            _rightUI.CreateSpacer().height = 10f;
            _rightUI.CreatePopup(controller.TriggerColliderChooserJSON, true);
            var btnGroup = rightPanel.AddComponent<HorizontalButtonGroup>();
            btnGroup.Padding = new RectOffset(0,0,-25,0);
            btnGroup.CreateLabel("Add", Styles.Default, true);
            btnGroup.CreateButton("Start", Styles.Default, () =>
            {
                controller.OnAddTriggerButtonClicked();
            });
            btnGroup.CreateButton("End", Styles.Default, () =>
            {
                controller.OnAddTriggerButtonClicked(TriggerManager.EndTriggerAction);
            });
        }

        private void InitClipLibraryUI()
        {
            clipLibrary = ClipLibrary.AddTo(leftPanel.transform);
            clipLibrary.Init(controller);
        }

        private void InitDebugging()
        {
            _leftUI.CreateToggle(controller.DebugToggleJSON);
        }

        #endregion

        #region Updates
        /**
		 * Create/refresh the list of available custom scene clips.
		 */
        public void RefreshClipLibrary()
        {
            if ((UnityEngine.Object) clipLibrary == (UnityEngine.Object) null) return;
            clipLibrary.IndexSourceClips();
        }

        public void RefreshCollectionClipList()
        {
            _activeCollectionClipListText.text = _activeCollection.ToString();
            clipLibrary.RefreshUI();
        }

        public void OnAtomRename(string oldID, string newID)
        {
            if (ReceiverAtomJSON.val == oldID) ReceiverAtomJSON.valNoCallback = newID;
            SyncReceiverAtomChoices();
        }
        #endregion

        #region Events

        public void OnActiveCollectionSelected(AudioMateCollectionManager.ActiveCollectionSelectedEventArgs eventArgs)
        {
            _activeCollection = eventArgs.After;
            if (_popupCollectionList.popup.currentValue != _activeCollection.Name)
            {
                _popupCollectionList.popup.currentValue = _activeCollection.Name;
            }

            _collectionNameJSON.valNoCallback = _activeCollection.Name;
            _toggleOnlyIfClearJSON.val = _activeCollection.OnlyIfClear;
            _toggleAlwaysQueueJSON.val = _activeCollection.AlwaysQueue;
            _toggleShuffleJSON.val = _activeCollection.ShuffleMode;
            _sliderPlayChanceJSON.val = _activeCollection.PlayChance;
            RefreshCollectionClipList();
        }

        public void OnActiveCollectionUpdated()
        {
            RefreshCollectionClipList();
        }

        public void OnDestroy()
        {
            if (clipLibrary != null) Destroy(clipLibrary);
            if (_rightUI != null) Destroy(_rightUI);
            if (_leftUI != null) Destroy(_leftUI);
            if (rightPanel != null) Destroy(rightPanel);
            if (leftPanel != null) Destroy(leftPanel);
        }
        #endregion

        #region Helpers
        private void Log(string message)
        {
            if ((UnityEngine.Object) controller == (UnityEngine.Object) null) return;
            controller.Log($" [UIManager]: {message}");
        }
        #endregion
    }
}

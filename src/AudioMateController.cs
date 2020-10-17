using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimpleJSON;
using UnityEngine.UI;
using AudioMate.UI;
using UnityEngine.Serialization;

namespace AudioMate {

    public static class Storables
    {
        public const string ClipCollections = "Collections";
        public const string ReceivingAtom = "ReceivingAtom";
        public const string ReceivingNode = "ReceivingNode";
        public const string PlayRandomClipAction = "PlayRandomClipFromActiveCollection";
        public const string QueueRandomClipAction = "QueueRandomClipFromActiveCollection";
        public const string PlayRandomClipInCollectionAction = "PlayRandomClipFrom";
        public const string QueueRandomClipInCollectionAction = "QueueRandomClipFrom";
        public const string TriggerActions = "TriggerActions";
    }

    #region Tools
    public static class Tools
    {
        /// <summary>
        /// Removes all whitespace from a string.
        /// </summary>
        public static string TrimAll(string str) {
            var len = str.Length;
            var src = str.ToCharArray();
            var dstIdx = 0;
            for (var i = 0; i < len; i++) {
                var ch = src[i];
                switch (ch) {
                    case '\u0020': case '\u00A0': case '\u1680': case '\u2000': case '\u2001':
                    case '\u2002': case '\u2003': case '\u2004': case '\u2005': case '\u2006':
                    case '\u2007': case '\u2008': case '\u2009': case '\u200A': case '\u202F':
                    case '\u205F': case '\u3000': case '\u2028': case '\u2029': case '\u0009':
                    case '\u000A': case '\u000B': case '\u000C': case '\u000D': case '\u0085':
                        break;
                    default:
                        src[dstIdx++] = ch;
                        break;
                }
            }
            return new string(src, 0, dstIdx);
        }

        /// <summary>
        /// Empty test for StorableStringChooserJSON choices
        /// </summary>
        public static bool IsEmptyChoice(string choice)
        {
            return string.IsNullOrEmpty(choice) || choice == "None";
        }

        /// <summary>
        /// Generates a unique ID
        /// </summary>
        public static string GenerateID()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
    #endregion


    /// <summary>
    /// AudioMate by dub
    /// -------------------------------------------------------
    /// Virt-a-Mate plugin for randomized audio clip playback
    /// and audio clip organization
    /// -------------------------------------------------------
    /// https://github.com/dub-a-licious/audio-mate
    /// </summary>
    public class AudioMateController : MVRScript
    {
        public static string GuessInitialReceivingNode(Atom atom)
        {
            switch (atom.category)
            {
                case "People":
                    return "HeadAudioSource";
                case "Sound":
                    switch (atom.type)
                    {
                        case "AudioSource":
                            return "AudioSource";
                        case "RhythmAudioSource":
                            return "RhythmSource";
                        case "AptSpeaker":
                            return "AptSpeaker_Import";
                        default:
                            return "None";
                    }
                default:
                    return "None";
            }
        }
        public AudioMateCollectionManager collections;
        public UIManager ui;
        public new MVRPluginManager Manager => base.manager;
        public FileManager fileManager;
        private TriggerManager _triggers;

        public Atom receivingAtom;
        public JSONStorable receivingNode;

        public bool needsProvisionalClipProcessing;
        public bool uiInitialized;

        private bool _restoring;
        private bool _needsSourceClipsRefresh;
        private bool _initializingUI;

        private string _missingReceiverNodeStoreId = "";

        private const bool _debug = false;

        private JSONStorableString _fileManagerJSON;
        public JSONStorableStringChooser CollectionsJSON;
        private JSONStorableAction _playRandomClipActionJSON;
        private JSONStorableAction _queueRandomClipActionJSON;
        private JSONStorableStringChooser _receivingAtomJSON;
        private JSONStorableStringChooser _receivingNodeJSON;
        public JSONStorableStringChooser TriggerColliderChooserJSON;
        public JSONStorableBool DebugToggleJSON { get; set; }

        #region Initialization
        public override void Init()
        {
            Log("### Init ###");
            try
            {
                base.Init();
                _triggers = new TriggerManager(this);
                InitStorables();
                InitFileManager();
                StartCoroutine(DeferredInit());
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateController)}.{nameof(Init)}: {e}");
            }
        }

        private IEnumerator DeferredInit()
        {
            yield return new WaitForEndOfFrame();

            if ((UnityEngine.Object) collections != (UnityEngine.Object) null) yield break;

            {
                if ((UnityEngine.Object) this == (UnityEngine.Object) null) yield break;
                while (SuperController.singleton.isLoading)
                {
                    yield return null;
                    if ((UnityEngine.Object) this == (UnityEngine.Object) null) yield break;
                }
            }

            if (_restoring) yield return null;

            {
                while (!uiInitialized)
                {
                    yield return new WaitForSeconds(.25f);
                    if ((UnityEngine.Object) this == (UnityEngine.Object) null) yield break;
                }
            }
            InitCollections();
            containingAtom.RestoreFromLast(this);
        }

        public override void InitUI()
        {
            Log("### InitUI ###");

            base.InitUI();
            try
            {
                if (UITransform == null) return;

                SuperController.singleton.StartCoroutine(InitUIDeferred());
            }
            catch (Exception exc)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateController)}.{nameof(InitUI)}: {exc}");
            }
        }

        private IEnumerator InitUIDeferred()
        {
            if ((UnityEngine.Object) ui != (UnityEngine.Object) null ||
                _initializingUI) yield break;
            _initializingUI = true;
            yield return SuperController.singleton.StartCoroutine(VamPrefabFactory.LoadUIAssets());

            var scriptUI = UITransform.GetComponentInChildren<MVRScriptUI>();

            var scrollRect =
                scriptUI.fullWidthUIContent.transform.parent.parent.parent.GetComponent<ScrollRect>();
            if ((UnityEngine.Object) scrollRect == (UnityEngine.Object) null)
                SuperController.LogError(
                    "AudioMate: Error during UI initialization. Scroll rect not at the expected hierarchy position");
            else
            {
                scrollRect.elasticity = 0;
                scrollRect.inertia = false;
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
            }

            ui = UIManager.AddTo(scriptUI.fullWidthUIContent);
            if ((UnityEngine.Object) ui == (UnityEngine.Object) null)
                SuperController.LogError(
                    "AudioMate: Error during UI initialization. UI is null.");
            ui.Init(this);
            ui.ReceiverAtomJSON = _receivingAtomJSON;
            ui.ReceiverNodeJSON = _receivingNodeJSON;
            ui.InitUI();
            //if ((UnityEngine.Object) collections == (UnityEngine.Object) null || !collections.isInitialized) yield return new WaitForEndOfFrame();
            var count = 0;
            while ((UnityEngine.Object) collections == (UnityEngine.Object) null || !collections.isInitialized)
            {
                if (++count > 200)
                {
                    SuperController.LogError(!collections.isInitialized
                        ? "AudioMate: Could not initialize collection manager."
                        : "AudioMate: Collection manager is null.");

                    yield break;
                }
                yield return new WaitForSeconds(.1f);
                if ((UnityEngine.Object) this == (UnityEngine.Object) null) yield break;
            }
            try
            {
                ui.Bind(collections);
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate: {e}");
            }
        }

        public void OnUIInitialized()
        {
            Log("### OnUIInitialized ###");
            uiInitialized = true;
            _needsSourceClipsRefresh = true;
        }

        private void InitCollections()
        {
            if ((UnityEngine.Object) collections != (UnityEngine.Object) null) return;
            Log("### InitCollections ###");
            try
            {
                collections = gameObject.AddComponent<AudioMateCollectionManager>();
                collections.OnActiveCollectionSelected.RemoveAllListeners();
                collections.OnActiveCollectionSelected.AddListener(OnActiveCollectionSelected);
                collections.OnActiveCollectionNameChanged.RemoveAllListeners();
                collections.OnActiveCollectionNameChanged.AddListener(OnActiveCollectionNameChanged);
                _playRandomClipActionJSON = new JSONStorableAction(Storables.PlayRandomClipAction,
                    () => collections.PlayRandomClipAction());
                RegisterAction(_playRandomClipActionJSON);
                _queueRandomClipActionJSON = new JSONStorableAction(Storables.QueueRandomClipAction,
                    () => collections.QueueRandomClipAction());
                RegisterAction(_queueRandomClipActionJSON);
                collections.Init(this);
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateController)}.{nameof(InitCollections)}: {e}");
            }
        }

        private void InitFileManager()
        {
            Log("### InitFileManager ###");
            fileManager = gameObject.AddComponent<FileManager>();
            fileManager.Bind(_fileManagerJSON);
        }

        private void InitStorables()
        {
            Log("### InitStorables ###");

            CollectionsJSON = new JSONStorableStringChooser(
                Storables.ClipCollections,
                new List<string>(),
                "",
                "Active Collection")
            {
                isRestorable = false,
                isStorable = false
            };
            RegisterStringChooser(CollectionsJSON);

            _receivingAtomJSON = new JSONStorableStringChooser(Storables.ReceivingAtom, null,
                containingAtom.uid, "Receiver Atom", SetReceivingAtom);
            _receivingAtomJSON.storeType = JSONStorableParam.StoreType.Full;
            RegisterStringChooser(_receivingAtomJSON);

            _receivingNodeJSON = new JSONStorableStringChooser(Storables.ReceivingNode, null,
                GuessInitialReceivingNode(containingAtom), "Receiver Node", SetReceivingNode);
            _receivingNodeJSON.storeType = JSONStorableParam.StoreType.Full;
            RegisterStringChooser(_receivingNodeJSON);

            TriggerColliderChooserJSON = new JSONStorableStringChooser(
                Storables.TriggerActions,
                _triggers.ColliderChoices,
                TriggerManager.DefaultCollider,
                "Trigger Actions");
            RegisterStringChooser(TriggerColliderChooserJSON);

            _fileManagerJSON = new JSONStorableString("File Manager", "")
            {
                isStorable = false,
                isRestorable = false
            };
            RegisterString(_fileManagerJSON);

            // DebugToggleJSON = new JSONStorableBool("Debug", true, val => _debug = val);
            // RegisterBool(DebugToggleJSON);
        }
        #endregion

        #region Lifecycle
        public void OnEnable()
        {
            _needsSourceClipsRefresh = true;
            if (collections != null)
            {
                collections.EnableAll();
                ui.RefreshCollectionClipList();
            }
        }

        public void OnDisable()
        {
            if (collections != null) collections.DisableAll();
        }

        public void Start()
        {
            Log("### Start ###");
        }

        public void OnDestroy()
        {
            Log("### OnDestroy ###");
            _triggers.OnDestroy();
            if (ui != null) Destroy(ui);
            if (collections != null) Destroy(collections);
        }


        public void Update()
        {
            if (!uiInitialized || (UnityEngine.Object) collections == (UnityEngine.Object) null) return;
            if (insideRestore) return;
            if (needsProvisionalClipProcessing && ui.clipLibrary.IsUpdated)
            {
                needsProvisionalClipProcessing = false;
                ui.clipLibrary.ProcessProvisionalClips();
            }
            if (_needsSourceClipsRefresh)
            {
                _needsSourceClipsRefresh = false;
                ui.RefreshClipLibrary();
            }
            FindMissingReceiverNode();
        }

        protected void OnAtomRename(string oldID, string newID)
        {
            if ((UnityEngine.Object) collections == (UnityEngine.Object) null) return;
            collections.OnAtomRename(oldID, newID);
            _triggers?.OnAtomRename(oldID, newID);
            ui.OnAtomRename(oldID, newID);
        }
        #endregion

        #region Load / Save
        public override JSONClass GetJSON(bool includePhysical = true, bool includeAppearance = true, bool forceStore = false)
        {
            var json = base.GetJSON(includePhysical, includeAppearance, forceStore);

            if (collections == null) return json;
            collections.Serialize(json, Storables.ClipCollections);
            needsStore = true;

            return json;
        }

        public override void RestoreFromJSON(JSONClass jc, bool restorePhysical = true, bool restoreAppearance = true, JSONArray presetAtoms = null, bool setMissingToDefault = true)
        {
            base.RestoreFromJSON(jc, restorePhysical, restoreAppearance, presetAtoms, setMissingToDefault);
            try
            {
                insideRestore = true;
                var restoredJSON = jc[Storables.ClipCollections];
                if (restoredJSON == null || restoredJSON.AsObject == null || $"{restoredJSON}" == "{}") return;
                if ((UnityEngine.Object) collections == (UnityEngine.Object) null)
                {
                    SuperController.singleton.StartCoroutine(LoadDeferred(restoredJSON));
                }
                else
                {
                    Load(restoredJSON);
                }
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateController)}.{nameof(RestoreFromJSON)}: {e}");
            }
            finally
            {
                insideRestore = false;
            }
        }

        public void Load(JSONNode json)
        {
            if ((UnityEngine.Object) collections == (UnityEngine.Object) null) return;
            try
            {
                if (_restoring) return;
                if (json == null || json.AsObject == null || $"{json}" == "{}")
                {
                    SuperController.LogError($"AudioMate.{nameof(AudioMateController)}.{nameof(Load)}: Invalid json data: {json}");
                    return;
                }
                _restoring = true;
                collections.Parse(json);
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateController)}.{nameof(Load)}: {e}");
            }
            finally
            {
                _restoring = false;
            }
        }

        private IEnumerator LoadDeferred(JSONNode json)
        {
            if (_restoring) yield break;
            yield return new WaitForEndOfFrame();
            while ((UnityEngine.Object) collections == (UnityEngine.Object) null)
            {
                yield return null;
                if ((UnityEngine.Object) this == (UnityEngine.Object) null) yield break;
            }
            Load(json);
        }
        #endregion

        #region File Import
        private string ImportAudioFiles(string requestedPath)
        {
            if (_fileManagerJSON == null) return null;
            try
            {
                SuperController.singleton.GetFilesAtPath(requestedPath).ToList().ForEach((string fileName) =>
                {
                    if (fileName.Contains(".json"))
                    {
                        return;
                    }

                    if (!fileName.Contains(".mp3") && !fileName.Contains(".wav") && !fileName.Contains(".ogg"))
                    {
                        return;
                    }

                    LoadAudio(fileName);
                    _fileManagerJSON.val += SuperController.singleton.NormalizePath(fileName) + "\n";
                });
                ui.clipLibrary.OnSourceClipsUpdated();
                return requestedPath;
            }
            catch
            {
                var folderName = "\\" + requestedPath.Substring(requestedPath.LastIndexOf('\\') + 1) + "\\";
                requestedPath = requestedPath.Replace(folderName, "\\");
                SuperController.singleton.GetFilesAtPath(requestedPath).ToList().ForEach((string fileName) =>
                {
                    if (fileName.Contains(".json"))
                    {
                        return;
                    }

                    if (!fileName.Contains(".mp3") && !fileName.Contains(".wav") && !fileName.Contains(".ogg"))
                    {
                        return;
                    }

                    //clipsSource.Add(LoadAudio(fileName));
                    LoadAudio(fileName);
                    _fileManagerJSON.val += SuperController.singleton.NormalizePath(fileName) + "\n";
                });
                ui.clipLibrary.OnSourceClipsUpdated();
                return requestedPath;
            }
        }

        private static NamedAudioClip LoadAudio(string path)
        {
            var localPath = SuperController.singleton.NormalizeLoadPath(path);
            var existing = URLAudioClipManager.singleton.GetClip(localPath);
            if (existing != null)
            {
                return existing;
            }

            var clip = URLAudioClipManager.singleton.QueueClip(SuperController.singleton.NormalizeMediaPath(path));
            if (clip == null)
            {
                return null;
            }

            var nac = URLAudioClipManager.singleton.GetClip(clip.uid);
            return nac ?? null;
        }

        #endregion

        #region Helpers

        public AudioMateClip FindAudioMateClipBySourceClipUID(string sourceClipUID)
        {
            var audioMateClip = ui.clipLibrary.FindAudioMateClipBySourceClipUID(sourceClipUID);
            return audioMateClip;
        }
        #endregion

        #region Callbacks

        public void OnAddTriggerButtonClicked(string triggerType = TriggerManager.StartTriggerAction)
        {
            if ((UnityEngine.Object) collections == (UnityEngine.Object) null) return;
            if (collections.ActiveCollection == null) return;
            var triggerName = GetPlayRandomClipTriggerName(TriggerColliderChooserJSON.val, collections.ActiveCollection.Name);
            if (string.IsNullOrEmpty(triggerName)) return;
            var receiverTargetName = AudioMateCollectionManager.GetPlayRandomClipActionName(collections.ActiveCollection.Name);
            var newTrigger = _triggers.AddTriggerAction(TriggerColliderChooserJSON.val, triggerName, storeId, receiverTargetName, triggerType);
            //Log($"Added new {triggerType} action {triggerName} for collider trigger {TriggerColliderChooserJSON.val}");
        }

        private void UpdateTriggerActionNamesOnCollectionRename(string oldCollectionName, string newCollectionName)
        {
            if (string.IsNullOrEmpty(Tools.TrimAll(oldCollectionName)) || string.IsNullOrEmpty(Tools.TrimAll(newCollectionName))) return;

            foreach (var triggerCollider in TriggerColliderChooserJSON.choices)
            {
                var oldTriggerActionName = GetPlayRandomClipTriggerName(triggerCollider, oldCollectionName);
                var newTriggerActionName = GetPlayRandomClipTriggerName(triggerCollider, newCollectionName);
                var newTriggerActionReceiverName = AudioMateCollectionManager.GetPlayRandomClipActionName(newCollectionName);

                _triggers.UpdateTriggerAction(oldTriggerActionName, newTriggerActionName, newTriggerActionReceiverName, TriggerManager.StartTriggerAction);
                _triggers.UpdateTriggerAction(oldTriggerActionName, newTriggerActionName, newTriggerActionReceiverName, TriggerManager.EndTriggerAction);
            }
        }

        private static string GetPlayRandomClipTriggerName(string triggerCollider, string collectionName)
        {
            return Tools.TrimAll($"{triggerCollider}:{AudioMateCollectionManager.GetPlayRandomClipActionName(collectionName)}");
        }

        private  string GetQueueRandomClipTriggerName(string triggerCollider, string collectionName)
        {
            return Tools.TrimAll($"{triggerCollider}:{AudioMateCollectionManager.GetQueueRandomClipActionName(collectionName)}");
        }

        public void OnActiveCollectionSelected(AudioMateCollectionManager.ActiveCollectionSelectedEventArgs eventArgs)
        {
            if ((UnityEngine.Object) collections == (UnityEngine.Object) null) return;

            var atom = collections.ActiveCollection?.ReceiverAtom;
            if (atom == null) return;
            _receivingAtomJSON.val = atom;

            var node = collections.ActiveCollection?.ReceiverNode;
            if (node == null) return;
            _receivingNodeJSON.val = node;
        }

        public void OnActiveCollectionNameChanged(AudioMateCollectionManager.ActiveCollectionNameChangedEventArgs eventArgs)
        {
            UpdateTriggerActionNamesOnCollectionRename(eventArgs.Before, eventArgs.After);
        }

        /**
		* Load audio source ReceiverAtom.
		*/
        private void SetReceivingAtom(string uid)
        {
            if (Tools.IsEmptyChoice(uid) || (UnityEngine.Object) collections == (UnityEngine.Object) null || collections.ActiveCollection == null) return;
            try
            {
                receivingAtom = SuperController.singleton.GetAtomByUid(uid);
                if (receivingAtom == null)
                {
                    SuperController.LogError(
                        $"AudioMate.{nameof(AudioMateController)}.{nameof(SetReceivingAtom)}: Audio Source ReceiverAtom has invalid uid {uid}");
                    return;
                }
                var atomChanged = collections.ActiveCollection.ReceiverAtom != receivingAtom.uid;
                collections.ActiveCollection.ReceiverAtom = receivingAtom.uid;
                if (!atomChanged) return;
                var guessedReceiver = GuessInitialReceivingNode(receivingAtom);
                insideRestore = true;
                _receivingNodeJSON.val = guessedReceiver;
                insideRestore = false;
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateController)}.{nameof(SetReceivingAtom)}: {e}");
            }
        }

        /**
		* Load audio source receiver.
		*/
        private void SetReceivingNode(string uid)
        {
            if (Tools.IsEmptyChoice(uid) || (UnityEngine.Object) collections == (UnityEngine.Object) null || collections.ActiveCollection == null) return;
            receivingNode = SuperController.singleton.GetAtomByUid(_receivingAtomJSON.val)?.GetStorableByID(uid);
            if (receivingNode == null)
            {
                _missingReceiverNodeStoreId = uid;
                return;
            }

            insideRestore = true;
            _receivingNodeJSON.val = receivingNode.name;
            insideRestore = false;
            collections.ActiveCollection.ReceiverNode = receivingNode.name;
            collections.ActiveCollection.SyncAudioReceiver();
        }

        protected void FindMissingReceiverNode() {
            if (_missingReceiverNodeStoreId != "" && receivingAtom != null) {
                var missingReceiver = receivingAtom.GetStorableByID(_missingReceiverNodeStoreId);
                if (missingReceiver == null) return;
                Log($"Late loading receiver node detected: {_missingReceiverNodeStoreId}");
                SetReceivingNode(_missingReceiverNodeStoreId);
                _missingReceiverNodeStoreId = "";
            }
        }
        #endregion

        #region Debug
        /**
         * Custom debug logging
         */
        public void Log(string message)
        {
            if (_debug) SuperController.LogMessage($"[{this.storeId}]: {message}");
        }

        /**
         * UI debugging. Depends on VAM Dev Tools by Acidbubbles (https://github.com/acidbubbles/vam-devtools).
         */
        public void DebugObject(Transform obj)
        {
            DebugObject(obj.gameObject);
        }

        public void DebugObject(GameObject obj)
        {
            if (_debug) containingAtom.BroadcastMessage("DevToolsInspectUI", obj);
        }
        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AudioMate.UI;
using Battlehub.Utils;
using Leap.Unity;
using SimpleJSON;
using UnityEngine;
using UnityEngine.Events;

namespace AudioMate
{
    public class AudioMateCollectionManager : MonoBehaviour
    {

        private class CollectionActions
        {
            public JSONStorableAction PlayAction;
            public JSONStorableAction QueueAction;
        }

        public class ActiveCollectionSelectedEventArgs { public AudioMateClipCollection Before; public AudioMateClipCollection After; }
        public class ActiveCollectionNameChangedEventArgs { public string Before; public string After; }

        public class ActiveCollectionSelectedEvent : UnityEvent<ActiveCollectionSelectedEventArgs> { }
        public class ActiveCollectionUpdatedEvent : UnityEvent { }
        public class ActiveCollectionNameChangedEvent : UnityEvent<ActiveCollectionNameChangedEventArgs> { }

        public ActiveCollectionSelectedEvent OnActiveCollectionSelected = new ActiveCollectionSelectedEvent();
        public ActiveCollectionUpdatedEvent OnActiveCollectionUpdated = new ActiveCollectionUpdatedEvent();
        public ActiveCollectionNameChangedEvent OnActiveCollectionNameChanged = new ActiveCollectionNameChangedEvent();

        private AudioMateController _controller;
        private UIManager _ui;
        private readonly List<AudioMateClipCollection> _collections = new List<AudioMateClipCollection>();

        private readonly Dictionary<string,CollectionActions> _collectionActions = new Dictionary<string, CollectionActions>();

        public JSONStorableStringChooser CollectionsJSON;

        public AudioMateClipCollection ActiveCollection { get; private set; }

        public int Count => _collections.Count;

        public IEnumerable<string> Names => Enumerable.ToList(_collections.Select(collection => collection.Name));
        public bool isInitialized;
        public AudioMateCollectionManager()
        {
        }

        #region Initialization
        public void Init(AudioMateController controller)
        {
            if ((UnityEngine.Object) controller == (UnityEngine.Object) null)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateCollectionManager)}.{nameof(Init)}: Controller is null.");
                return;
            }
            _controller = controller;
            if (isInitialized)
            {
                Log("Collection manager is already initialized.");
                return;
            }
            Log("### Init ###");
            if ((UnityEngine.Object) _controller.ui == (UnityEngine.Object) null)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateCollectionManager)}.{nameof(Init)}: UI is not initialized.");
                return;
            }
            _ui = _controller.ui;
            CollectionsJSON = _controller.CollectionsJSON;
            CollectionsJSON.setCallbackFunction = SelectActiveCollection;
            SelectActiveCollection();
            isInitialized = true;
        }

        private void AddDefaultCollection()
        {
            if (_collections == null) return;

            var newCollection = AddCollection();
            SelectActiveCollection(newCollection);
        }
        #endregion

        #region Helper
        private string GetAvailableDefaultName()
        {
            var newName = "Untitled";
            var i = 2;
            while (Names.Contains(newName))
            {
                newName = "Untitled" + i;
                i++;
            }

            return newName;
        }

        public void SyncCollectionNames()
        {
            var choices = new List<string>();
            choices.AddRange(Names);
            CollectionsJSON.choices = choices;
        }
        #endregion

        #region Collection Management

        public AudioMateClipCollection AddCollection(string collectionName = null)
        {
            if (collectionName == null)
            {
                collectionName = GetAvailableDefaultName();
            }
            var newCollection = new AudioMateClipCollection(collectionName, _controller);
            return AddCollection(newCollection);
        }

        private AudioMateClipCollection AddCollection(AudioMateClipCollection collection)
        {
            if (collection == null || string.IsNullOrEmpty(collection.Name)) return null;
            _collections.Add(collection);
            AddNewCollectionActions(collection.Name);
            if (!CollectionsJSON.choices.Contains(collection.Name))
            {
                CollectionsJSON.choices.Add(collection.Name);
            }
            SelectActiveCollection(collection);
            return collection;
        }

        public bool RemoveCollection(string collectionName)
        {
            if (string.IsNullOrEmpty(collectionName)) return false;
            var collectionToRemove = _collections.Find(x => x.Name == collectionName);
            return collectionToRemove != null && RemoveCollection(collectionToRemove);
        }

        private bool RemoveCollection(AudioMateClipCollection collection)
        {
            var collectionName = collection?.Name;
            Log($"Collection name {collectionName}");

            if (string.IsNullOrEmpty(collectionName)) return false;
            CollectionsJSON.choices.Remove(collectionName);
            RemoveCollectionActions(collectionName);
            var result = _collections.Remove(collection);
            SyncCollectionNames();
            return result;
        }

        public bool RemoveActiveCollection()
        {
            var collectionIndex = _collections.IndexOf(ActiveCollection);
            Log($"Remove collection at index {collectionIndex}");

            var result = RemoveCollection(ActiveCollection);
            if (_collections.Count == 0)
            {
                AddDefaultCollection();
            }
            else
            {
                collectionIndex--;
                SelectActiveCollection(collectionIndex == -1 ? _collections.Last() : _collections[collectionIndex]);
            }
            return result;
        }

        public void SelectActiveCollection(AudioMateClipCollection collection = null)
        {
            while (true)
            {
                if (collection != null)
                {
                    var previous = ActiveCollection;
                    ActiveCollection = collection;
                    ActiveCollection.SyncAudioReceiver();
                    if (CollectionsJSON.val != ActiveCollection.Name) CollectionsJSON.val = ActiveCollection.Name;
                    OnActiveCollectionSelected.Invoke(new ActiveCollectionSelectedEventArgs {Before = previous, After = ActiveCollection});
                    return;
                }

                if (_collections.Count > 0)
                {
                    collection = _collections.First();
                    continue;
                }
                else
                {
                    AddDefaultCollection();
                }

                break;
            }
        }

        public void SelectActiveCollection(string collectionName)
        {
            if ((UnityEngine.Object) _controller == (UnityEngine.Object) null || string.IsNullOrEmpty(collectionName)) return;
            var collection = _collections.Find(x => x.Name == collectionName);
            if (collection == null) return;
            SelectActiveCollection(collection);
        }

        public void Enable(string collectionName)
        {
            var collection = _collections.Find(x => x.Name == collectionName);
            collection?.Enable();
        }

        public void Enable()
        {
            ActiveCollection?.Enable();
        }

        public void Disable(string collectionName)
        {
            var collection = _collections.Find(x => x.Name == collectionName);
            collection?.Disable();
        }

        public void Disable()
        {
            ActiveCollection?.Disable();
        }

        public void EnableAll()
        {
            foreach (var collection in _collections)
            {
                collection.Enable();
            }
        }

        public void DisableAll()
        {
            foreach (var collection in _collections)
            {
                collection.Disable();
            }
        }

        public void SetActiveCollectionName(string newCollectionName)
        {
            if (string.IsNullOrEmpty(newCollectionName)) return;
            if (ActiveCollection == null) return;
            var oldCollectionName = ActiveCollection.Name;
            ActiveCollection.Name = newCollectionName;
            SyncCollectionNames();
            CollectionsJSON.val = newCollectionName;
            UpdateCollectionActionNames(oldCollectionName, newCollectionName);

            OnActiveCollectionNameChanged.Invoke(new ActiveCollectionNameChangedEventArgs
            {
                Before = oldCollectionName,
                After = newCollectionName
            });
        }

        /**
         * Adds a clip to the active collections
         */
        public void AddClipToActiveCollection(AudioMateClip clip, bool noEvent = false)
        {
            ActiveCollection?.Add(clip);
            if (!noEvent) OnActiveCollectionUpdated.Invoke();
        }

        /**
         * Adds a range of clips to the active collections
         */
        public void AddClipRangeToActiveCollection(int fromIndex, int toIndex, bool noEvent = false)
        {
            if (ActiveCollection == null) return;
            for (var i = fromIndex; i <= toIndex; i++)
            {
                var clip = _ui.clipLibrary.Clips.ElementAtOrDefault(i);
                if (clip == null) continue;
                ActiveCollection.Add(clip);
                _controller.ui.clipLibrary.SetCursorIndex(i);
            }
            if (!noEvent) OnActiveCollectionUpdated.Invoke();
        }

        /**
         * Removes a clip from a collection
         */
        public bool RemoveClipFromActiveCollection(AudioMateClip clip, bool noEvent = false)
        {
            if (ActiveCollection == null || !ActiveCollection.Remove(clip)) return false;
            if (!noEvent) OnActiveCollectionUpdated.Invoke();
            return true;

        }

        /**
         * Adds all available clips to the active collections
         */
        public void AddAllClipsToActiveCollection()
        {
            foreach (var clip in _ui.clipLibrary.Clips)
            {
                AddClipToActiveCollection(clip, true);
            }

            OnActiveCollectionUpdated.Invoke();
        }

        /**
         * Adds 10 clip to the active collections, starting from the cursor
         */
        public void Add10ClipsToActiveCollection()
        {
            AddClipRangeToActiveCollection(_controller.ui.clipLibrary.Cursor.Index, _controller.ui.clipLibrary.Cursor.Index + 10, true);
            OnActiveCollectionUpdated.Invoke();
        }

        /**
         * Adds 20 clip to the active collections, starting from the cursor
         */
        public void Add20ClipsToActiveCollection()
        {
            AddClipRangeToActiveCollection(_controller.ui.clipLibrary.Cursor.Index, _controller.ui.clipLibrary.Cursor.Index + 20, true);
            OnActiveCollectionUpdated.Invoke();
        }

        /**
         * Removes all clips from the active collections
         */
        public void ClearActiveCollection()
        {
            ActiveCollection?.Clear();
            OnActiveCollectionUpdated.Invoke();
        }
        #endregion

        #region Clip Actions
        /**
         * Plays a random clip from a specific Collection
         */
        public void PlayRandomClipInCollection(string collectionName, bool queue = false)
        {
            var collection = _collections.Find(x => x.Name == collectionName);
            if (queue)
            {
                collection?.QueueRandomClip();
            }
            else
            {
                collection?.PlayRandomClip();
            }
        }

        /**
         * Queues a random clip from a specific Collection
         */
        public void QueueRandomClipInCollection(string collectionName)
        {
            PlayRandomClipInCollection(collectionName, true);
        }

        public void PlayRandomClipAction(string collectionName = null)
        {
            if (collectionName == null)
            {
                ActiveCollection?.PlayRandomClip();
            }
            else
            {
                PlayRandomClipInCollection(collectionName);
            }
        }

        public void QueueRandomClipAction(string collectionName = null)
        {
            if (collectionName == null)
            {
                ActiveCollection?.QueueRandomClip();
            }
            else
            {
                QueueRandomClipInCollection(collectionName);
            }
        }
        #endregion

        /**
         * Creates all needed Actions for a new collection
         */
        public void AddNewCollectionActions(string collectionName)
        {
            Log("### AddNewCollectionActions ###");
            if (string.IsNullOrEmpty(collectionName))
            {
                Log($"{nameof(AddNewCollectionActions)}: collectionName is empty");
                return;
            }
            if (_collectionActions == null) return;
            if (_collectionActions.ContainsKey(collectionName)) return;
            var newActions = new CollectionActions
            {
                PlayAction = new JSONStorableAction(GetPlayRandomClipActionName(collectionName),
                    () => PlayRandomClipAction(collectionName)),
                QueueAction = new JSONStorableAction(GetQueueRandomClipActionName(collectionName),
                    () => QueueRandomClipAction(collectionName))
            };

            _controller.RegisterAction(newActions.PlayAction);
            _controller.RegisterAction(newActions.QueueAction);
            _collectionActions.Add(collectionName, newActions);
        }

        private void UpdateCollectionActionNames(string oldCollectionName, string newCollectionName)
        {
            Log("### UpdateCollectionActionNames ###");
            if (string.IsNullOrEmpty(oldCollectionName) || string.IsNullOrEmpty(newCollectionName))
            {
                Log($"{nameof(AddNewCollectionActions)}: one or both input values are empty");
                return;
            }
            if (_collectionActions == null) return;
            CollectionActions actions;
            if (!_collectionActions.TryGetValue(oldCollectionName, out actions)) return;
            if (actions != null)
            {
                RemoveCollectionActions(oldCollectionName);
            }
            AddNewCollectionActions(newCollectionName);
        }

        private void RemoveCollectionActions(string collectionName)
        {
            CollectionActions actions;
            if (!_collectionActions.TryGetValue(collectionName, out actions)) return;
            _controller.DeregisterAction(actions.PlayAction);
            _controller.DeregisterAction(actions.QueueAction);
            _collectionActions.Remove(collectionName);
            actions.PlayAction = null;
            actions.QueueAction = null;
        }

        public static string GetPlayRandomClipActionName(string collectionName)
        {
            return Tools.TrimAll($"{Storables.PlayRandomClipInCollectionAction}{collectionName}");
        }

        public static string GetQueueRandomClipActionName(string collectionName)
        {
            return Tools.TrimAll($"{Storables.QueueRandomClipInCollectionAction}{collectionName}");
        }

        #region Callbacks

        public void OnAtomRename(string oldID, string newID)
        {
            foreach (var collection in _collections.Where(collection => collection.ReceiverAtom == oldID))
            {
                collection.ReceiverAtom = newID;
                collection.SyncAudioReceiver();
            }
        }
        #endregion

        #region Serializer

        public void Serialize(JSONClass json, string id)
        {
            json[id] = GetCollectionsJSON();
        }

        private JSONClass GetCollectionsJSON()
        {
            var json = new JSONClass();
            try
            {
                foreach (var collection in _collections.Where(collection => collection != null && collection.Count != 0))
                {
                    json[collection.Name] = collection.ToJSON();
                }
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateCollectionManager)}.{nameof(GetCollectionsJSON)}: {e}");
            }
            return json;
        }

        public void Parse(JSONNode json)
        {
            if (json == null || json.AsObject == null)
            {
                Log("Parsing failed: empty JSONNode received.");
                return;
            }
            else
            {
                Log($"Parse: {json}");
            }
            try
            {
                _collections.Clear();

                foreach (var collectionJSON in json.Childs)
                {
                    //json = Sanitize(json);
                    var newCollection = new AudioMateClipCollection(collectionJSON["name"], _controller);
                    if (newCollection.Parse(collectionJSON))
                    {
                        AddCollection(newCollection);
                    }
                    else
                    {
                        newCollection = null;
                    }
                }

                if (_collections.Count > 0)
                {
                    SelectActiveCollection(_collections.First());
                }
            }
            catch (Exception e)
            {
                SuperController.LogError(
                    $"AudioMate.{nameof(AudioMateCollectionManager)}.{nameof(Parse)}: {e}");
            }
        }

        /*private JSONNode Sanitize(JSONNode jn)
        {
            try
            {
                _controller.Log($"{jn}");
                if (string.IsNullOrEmpty(jn["name"])) jn["name"] = GetAvailableDefaultName();
                if (string.IsNullOrEmpty(jn["enabled"])) jn["enabled"] = true.ToString();
                if (string.IsNullOrEmpty(jn["receiverAtom"])) jn["receiverAtom"] = _controller.containingAtom.uid;
                if (string.IsNullOrEmpty(jn["receiverNode"])) jn["receiverNode"] = _controller.GuessInitialReceivingNode;
                if (string.IsNullOrEmpty(jn["shuffle"])) jn["shuffle"] = true.ToString();
                if (string.IsNullOrEmpty(jn["alwaysQueue"])) jn["alwaysQueue"] = false.ToString();
                if (string.IsNullOrEmpty(jn["onlyIfClear"])) jn["onlyIfClear"] = false.ToString();
                if (string.IsNullOrEmpty(jn["playChance"])) jn["playChance"] = 1f.ToString(CultureInfo.InvariantCulture);
                if (string.IsNullOrEmpty(jn["lastClipIndex"])) jn["lastClipIndex"] = 0.ToString(CultureInfo.InvariantCulture);
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateCollectionManager)}.{nameof(Sanitize)}: {e}");
            }

            return jn;
        }*/

        #endregion

        private void Log(string message)
        {
            if ((UnityEngine.Object) _controller == (UnityEngine.Object) null)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateCollectionManager)}.{nameof(Log)}: Controller is null.");
                return;
            }
            _controller.Log($"AudioMateCollectionManager: {message}");
        }

    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AudioMate.Extension;
using Leap.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace AudioMate.UI
{

    public class ClipLibrary : MonoBehaviour
    {

        private static RectTransform _content;

        public static ClipLibrary AddTo(Transform parent)
        {
            var go = new GameObject();
            go.transform.SetParent(parent.transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.flexibleHeight = 500f;

            return Configure(go);
        }

        private static ClipLibrary Configure(GameObject go)
        {
            var group = go.AddComponent<VerticalLayoutGroup>();
            group.spacing = 5f;

            var rect = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1);

            _content = VamPrefabFactory.CreateScrollRect(go);

            return go.AddComponent<ClipLibrary>();
        }

        private static GameObject CreateContainer()
        {
            var go = new GameObject("Header");
            go.transform.SetParent(_content, false);
            var layout = go.AddComponent<LayoutMaxSize>();
            layout.adjustHeight = true;
            layout.adjustWidth = true;
            layout.SetLayoutVertical();
            {
                var child = new GameObject();
                child.transform.SetParent(go.transform, false);

                var rect = child.AddComponent<RectTransform>();
                rect.StretchParent();
            }

            return go;
        }

        private static string GetClipObjectName(AudioMateClip clip)
        {
            return $"AudioMateClip{clip.SourceClip.uid}";
        }

        public class ClipCursor
        {
            public int Index = 0;
            public AudioMateClip Clip;
        }

        private AudioMateController _controller;
        private AudioMateCollectionManager _collections;
        private AudioMateClipCollection _activeCollection;
        public VamPrefabFactory prefabFactory;

        private Transform _buttonPrefab;
        private List<NamedAudioClip> _sourceClipList;

        public List<AudioMateClip> Clips { get; private set; }
        public readonly ClipCursor Cursor = new ClipCursor();
        public bool isRefreshing;
        private bool _isBound;

        public void Init(AudioMateController controller)
        {
            if ((UnityEngine.Object) controller == (UnityEngine.Object) null) return;
            _controller = controller;
            Log("### Init");
            _buttonPrefab = controller.manager.configurableButtonPrefab;
            Clips = new List<AudioMateClip>();

            var container = CreateContainer();
            prefabFactory = container.gameObject.AddComponent<VamPrefabFactory>();
            prefabFactory.controller = controller;
        }

        public void Bind()
        {
            Log("### Bind");
            _collections = _controller.collections;
            _activeCollection = _collections.ActiveCollection;
            _isBound = true;
            IndexSourceClips();
        }

        public void SetCursorIndex(int index)
        {
            if (Cursor == null) return;
            Cursor.Index = index;
            Cursor.Clip = Clips[index];
        }

        public bool IndexSourceClips()
        {
            if (_isBound == false || isRefreshing) return false;
            Log("---> IndexSourceClips");
            try
            {
                SuperController.singleton.StopCoroutine(DeferredWaitForSourceClips());
                SuperController.singleton.StartCoroutine(DeferredWaitForSourceClips());
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(ClipLibrary)}.{nameof(IndexSourceClips)}: {e}");
            }
            return true;
        }

        private IEnumerator DeferredWaitForSourceClips()
        {
            isRefreshing = true;
            if (_sourceClipList == null)
            {
                _sourceClipList = URLAudioClipManager.singleton.GetCategoryClips("web");
                yield return null;
            }
            while (_sourceClipList == null)
            {
                _sourceClipList = URLAudioClipManager.singleton.GetCategoryClips("web");
                yield return new WaitForSeconds(.5f);
                if ((UnityEngine.Object) this == (UnityEngine.Object) null) yield break;
            }

            // Convert new clips to AudioMateClips and add them to the list
            var clipCount = 0;
            foreach (var audioMateClip in
                from sourceClip in _sourceClipList
                where Clips.Find(x => x.SourceClip.uid == sourceClip.uid) == null
                select CreateClip(sourceClip))
            {
                if (audioMateClip == null) continue;
                Clips.Add(audioMateClip);
                clipCount++;
            }
            Log($"   - Added {clipCount} clips to library.");
            // If source clip list is bigger than AudioMateClip list after refresh then we have orphaned (=with missing source clip)
            // entries in the AudioMateClip list.
            if (_sourceClipList.Count != Clips.Count)
            {
                RemoveOrphanedClips();
            }
            RefreshUI();
            isRefreshing = false;
        }

        // Removes AudioMateClips whose source clips are not available anymore.
        private void RemoveOrphanedClips()
        {
            Log("---> RemoveOrphanedClips");
            var clipCount = 0;
            foreach (var audioMateClip in Clips.Where(audioMateClip => audioMateClip.SourceClip == null))
            {
                Clips.Remove(audioMateClip);
                audioMateClip.Destroy();
                clipCount++;
            }
            Log($"   - Done! Removed {clipCount} orphaned clips.");
        }

        // Converts a NamedAudioClip into a AudioMateClip and creates the necessary UI to display the clip
        // in the clip library
        private AudioMateClip CreateClip(NamedAudioClip sourceClip)
        {
            var clip = new AudioMateClip(sourceClip);
            var clipUID = GetClipObjectName(clip);
            //Log(clipUID);
            var go = new GameObject(clipUID);
            go.transform.SetParent(_content, false);
            var gridLayout = go.AddComponent<HorizontalLayoutGroup>();
            gridLayout.spacing = 10f;
            gridLayout.childForceExpandWidth = false;
            gridLayout.childControlWidth = true;
            gridLayout.childForceExpandHeight = true;
            gridLayout.childControlHeight = false;
            gridLayout.padding = new RectOffset(5, 5, -20, -20);
            var buttonGroup = gridLayout.gameObject.AddComponent<HorizontalButtonGroup>();
            var previewButton = buttonGroup.CreateButton("\u25B6", Styles.Success, () =>
            {
                clip.SourceClip.Test();
                Cursor.Clip = clip;
                Cursor.Index = Clips.IndexOf(clip);
                RefreshUI();
            }, false, 10f);

            var toggleButton = buttonGroup.CreateButton(clip.DisplayName, Styles.Disabled, () =>
            {
                clip.ToggleState();
                if (clip.IsInActiveCollection)
                {
                    _collections.AddClipToActiveCollection(clip);
                }
                else
                {
                    _collections.RemoveClipFromActiveCollection(clip);
                }

                RefreshUI();
            }, true, 100f);
            clip.UI.PreviewButton = previewButton;
            clip.UI.ToggleButton = toggleButton;
            clip.RefreshUI();

            return clip;
        }

        public void RefreshUI()
        {
            Log($"RefreshUI");
            if (_activeCollection == null) return;
            foreach (var clip in Clips)
            {
                clip.SetState(_activeCollection.Contains(clip));
                clip.SetCursor(Cursor.Clip == clip);
            }
        }

        public void OnActiveCollectionSelected(
            AudioMateCollectionManager.ActiveCollectionSelectedEventArgs args)
        {
            if (args.After != null) _activeCollection = args.After;
            RefreshUI();
        }

        public void OnActiveCollectionUpdated()
        {
            RefreshUI();
        }

        public void OnSourceClipsUpdated()
        {
            IndexSourceClips();
        }

        public void OnDestroy()
        {
            isRefreshing = false;
            _isBound = false;
            try
            {
                if (Clips != null)
                {
                    foreach (var audioMateClip in Clips)
                    {
                        Clips?.Remove(audioMateClip);
                        audioMateClip?.Destroy();
                        if (Clips == null) break;
                    }
                }

                Destroy(_content);
            }
            catch (Exception e)
            {
                SuperController.LogMessage($"AudioMate: [ClipLibrary.OnDestroy] {e}");
            }
        }

        private void Log(string message)
        {
            if ((UnityEngine.Object) _controller == (UnityEngine.Object) null)
            {
                SuperController.LogMessage($"AudioMate: [ClipLibrary]: ${message}");
                return;
            }
            _controller.Log($" [ClipLibrary]: ${message}");
        }
    }
}

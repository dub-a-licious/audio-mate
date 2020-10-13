using System;
using AudioMate.UI;
using SimpleJSON;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AudioMate
{
    public class AudioMateClipUI
    {

        public UIDynamicButton PreviewButton;
        public UIDynamicButton ToggleButton;
        public Outline ToggleOutline;

        public bool SetInCollectionState(bool state)
        {
            if ((UnityEngine.Object) ToggleButton == (UnityEngine.Object) null) return state;
            ToggleButton.textColor = state ? Styles.EnabledText : Styles.DisabledText;
            ToggleButton.buttonColor = state ? Styles.EnabledBg : Styles.DisabledBg;
            if ((UnityEngine.Object) ToggleOutline != (UnityEngine.Object) null) ToggleOutline.effectColor = state ? Color.white : Styles.DisabledBg;

            return state;
        }

        public bool SetCursorState(bool state)
        {
            if ((UnityEngine.Object) PreviewButton == (UnityEngine.Object) null) return state;
            PreviewButton.textColor = state ? Styles.CursorEnabledText : Styles.CursorDisabledText;
            PreviewButton.buttonColor = state ? Styles.CursorEnabledBg : Styles.CursorDisabledBg;
            return state;
        }

        public void Destroy()
        {
            try
            {
                if ((UnityEngine.Object) ToggleButton != (UnityEngine.Object) null)
                {
                    Object.Destroy(ToggleButton);
                }

                if ((UnityEngine.Object) PreviewButton != (UnityEngine.Object) null)
                {
                    Object.Destroy(PreviewButton);
                }
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateClipUI)}.{nameof(Destroy)}: {e}");
            }
        }
    }

    public class AudioMateClip
    {
        public string DisplayName => SourceClip.displayName;

        public NamedAudioClip SourceClip { get; private set; }
        public AudioSourceControl Receiver;

        public bool IsInActiveCollection { get; private set; }
        public bool HasCursor { get; private set; }

        public AudioMateClipUI UI;

        public AudioMateClip(NamedAudioClip sourceClip)
        {
            SourceClip = sourceClip;
            Init();
        }
        public AudioMateClip(JSONNode jn)
        {
            FromJSON(jn);
            Init();
        }

        private void Init()
        {
            UI = new AudioMateClipUI();
            SourceClip.InitUI();
            IsInActiveCollection = false;
            HasCursor = false;
            RefreshUI();
        }

        public void RefreshUI()
        {
            if (UI == null) return;
            UI.SetInCollectionState(IsInActiveCollection);
            UI.SetCursorState(HasCursor);
        }

        public JSONClass ToJSON()
        {
            return new JSONClass
            {
                { "sourceClip", SourceClip?.uid },
            };
        }

        private void FromJSON(JSONNode jn)
        {
            if (jn == null || jn.AsObject == null) return;
            var clipUID = jn["sourceClip"];
            if (clipUID == null) clipUID = jn["sourceClipUID"];
            SourceClip = URLAudioClipManager.singleton.GetClip(clipUID);
        }

        /**
         * Play the assigned audio clip with optional "if clear" param.
         */
        public void Play(AudioSourceControl receiver = null, bool ifClear = false, bool clearQueue = false)
        {
            if (SourceClip == null) return;
            if ((UnityEngine.Object) receiver != (UnityEngine.Object) null) Receiver = receiver;
            if (ifClear) {
                Receiver.PlayIfClear(SourceClip);
            } else {
                if (clearQueue)
                {
                    Receiver.PlayNowClearQueue(SourceClip);
                }
                else
                {
                    Receiver.PlayNow(SourceClip);
                }
            }
        }

        /**
         * Add assigned audio clip to audio controller queue list.
         */
        public void Queue(AudioSourceControl receiver = null)
        {
            if (SourceClip == null) return;
            if ((UnityEngine.Object) receiver != (UnityEngine.Object) null)
            {
                Receiver = receiver;
            }
            else
            {
                return;
            }
            Receiver.QueueClip(SourceClip);
        }

        public void Destroy()
        {
            try
            {
                UI.Destroy();
                Receiver = null;
                SourceClip = null;
            } catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateClip)}.{nameof(Destroy)}: {e}");
            }
        }

        public bool ToggleState()
        {
            IsInActiveCollection = !IsInActiveCollection;
            UI.SetInCollectionState(IsInActiveCollection);
            return IsInActiveCollection;
        }

        public bool SetState(bool state)
        {
            IsInActiveCollection = state;
            UI.SetInCollectionState(state);
            return state;
        }

        public bool SetCursor(bool state)
        {
            HasCursor = state;
            UI.SetCursorState(state);
            return state;
        }
    }
}

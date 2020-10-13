using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Leap;
using Leap.Unity;
using SimpleJSON;
using UnityEngine;
using UnityEngine.UI;
using UnityThreading;

namespace AudioMate
{
    public class AudioMateClipCollection
    {
        public string Name;

        public bool Enabled = true;
        public bool ShuffleMode = true;
        public bool AlwaysQueue;
        public bool OnlyIfClear;
        public string ReceiverAtom;
        public string ReceiverNode;
        public AudioSourceControl Receiver;

        public float PlayChance = 1f;

        private int _lastPlayedClipIndex;

        private readonly AudioMateController _controller;

        private readonly List<AudioMateClip> _clips = new List<AudioMateClip>();

        private readonly List<int> _unplayedClipsList = new List<int>();

        public IEnumerable<NamedAudioClip> NamedAudioClips
        {
            get
            {
                return Enumerable.ToList(_clips.Select(clip => clip.SourceClip));
            }
        }

        public int Count => _clips.Count;

        public bool Contains(AudioMateClip clip) => (bool) _clips.Contains(clip);
        public AudioMateClip Find(System.Predicate<AudioMateClip> predicate) => _clips.Find(predicate);


        public AudioMateClipCollection(AudioMateController controller)
        {
            _controller = controller;
            ResetUnplayedClips();
            Init();
        }

        public AudioMateClipCollection(string collectionName, AudioMateController controller)
        {
            _controller = controller;
            Name = collectionName;
            ResetUnplayedClips();
            Init();
        }

        public AudioMateClipCollection(string collectionName, IEnumerable<AudioMateClip> clips, AudioMateController controller)
        {
            try
            {
                _controller = controller;
                Name = collectionName;
                _clips.AddRange(clips);
                ResetUnplayedClips();
                Init();
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateClipCollection)}.{nameof(AudioMateClipCollection)} (IEnumerable): {e}");
            }
        }

        public AudioMateClipCollection(JSONNode jn, AudioMateController controller)
        {
            if (jn == null) return;
            try
            {
                _controller = controller;
                Parse(jn);
                Init();
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateClipCollection)}.{nameof(AudioMateClipCollection)} (JSON): {e}");
            }
        }

        private void Init()
        {
            SetDefaults();
        }

        private void SetDefaults()
        {
            if (ReceiverAtom == null) ReceiverAtom = _controller.containingAtom.uid;
            if (ReceiverNode == null) ReceiverNode = AudioMateController.GuessInitialReceivingNode(_controller.containingAtom);
            SyncAudioReceiver();
        }

        public JSONClass ToJSON()
        {
            var clipsJSON = new JSONArray();
            if (_clips.Count > 0)
            {
                foreach (var clip in _clips)
                {
                    clipsJSON.Add(clip.ToJSON());
                }
            }

            var collectionJSON = new JSONClass
            {
                { "name", Name },
                { "enabled", Enabled.ToString() },
                { "receiverAtom", ReceiverAtom },
                { "receiverNode", ReceiverNode },
                { "shuffle", ShuffleMode.ToString() },
                { "alwaysQueue", AlwaysQueue.ToString() },
                { "onlyIfClear", OnlyIfClear.ToString() },
                { "playChance", PlayChance.ToString(CultureInfo.InvariantCulture) },
                { "lastClipIndex", _lastPlayedClipIndex.ToString() },
                { "clips", clipsJSON }
            };
            return collectionJSON;
        }

        public bool Parse(JSONNode jn)
        {
            try
            {
                if (jn == null || jn.AsObject == null)
                {
                    SuperController.LogMessage($"AudioMate.{nameof(AudioMateClipCollection)}.{nameof(Parse)} Method called with null JSONNode");
                    return false;
                }
                _clips.Clear();
                Name = jn["name"];
                Enabled = jn["enabled"].AsBool;
                ReceiverAtom = jn["receiverAtom"];
                ReceiverNode = jn["receiverNode"];
                ShuffleMode = jn["shuffle"].AsBool;
                AlwaysQueue = jn["alwaysQueue"].AsBool;
                OnlyIfClear = jn["onlyIfClear"].AsBool;
                PlayChance = jn["playChance"].AsFloat;
                _lastPlayedClipIndex = jn["lastClipIndex"].AsInt;

                if (jn["clips"] != null)
                {
                    foreach (JSONClass clip in jn["clips"].AsArray)
                    {
                        _clips.Add(new AudioMateClip(clip));
                    }
                }
                ResetUnplayedClips();
                SyncAudioReceiver();
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateClipCollection)}.{nameof(Parse)} {e}");
            }

            return true;
        }

        /**
         * Adds a clip to this collections
         */
        public void Add(AudioMateClip clip)
        {
            if (_clips == null) return;
            if (_clips.Contains(clip)) return;
            _clips.Add(clip);
            ResetUnplayedClips();
        }

        /**
         * Removes a clip from this collection
         */
        public bool Remove(NamedAudioClip sourceClip)
        {
            if (_clips == null) return false;
            var clip = FindByNamedAudioClip(sourceClip);
            if (clip == null) return false;
            ResetUnplayedClips();
            return true;
        }

        /**
         * Removes a clip from this collection
         */
        public bool Remove(AudioMateClip clip)
        {
            if (_clips == null) return false;
            var result = _clips.Remove(clip);
            if (result) ResetUnplayedClips();
            return result;
        }

        public void Clear()
        {
            _clips.Clear();
            ResetUnplayedClips();
        }

        public void Enable()
        {
            Enabled = true;
        }

        public void Disable()
        {
            Enabled = false;
        }

        /**
         * Checks if a NamedAudioClip already exists in this collection.
         */
        public bool ContainsNamedAudioClip(NamedAudioClip namedAudioClip)
        {
            return ContainsNamedAudioClip(namedAudioClip.uid);
        }

        private bool ContainsNamedAudioClip(string namedAudioClipUID)
        {
            return _clips != null && _clips.Exists(x => x.SourceClip.uid == namedAudioClipUID);
        }

        private AudioMateClip FindByNamedAudioClip(NamedAudioClip sourceClip)
        {
            return _clips?.Find(x => x.SourceClip == sourceClip);
        }

        /**
         * Evaluate play chance and if it succeeds get a randomly (shuffle) selected clip from the clip collection.
         */
        private AudioMateClip GetRandomClip(bool skipPlayChance = false)
        {
            if (_clips == null) return null;
            var randomClip = (AudioMateClip) null;
            var randomIndex = -1;

            try
            {
                // PlayChance test
                var randomChance = (float) UnityEngine.Random.Range(0, 100) / 100;

                if (!skipPlayChance && PlayChance < randomChance) return null;

                if (ShuffleMode)
                {
                    if (_unplayedClipsList.Count == 1)
                    {
                        randomClip = _clips[_unplayedClipsList.First()];

                        _lastPlayedClipIndex = _unplayedClipsList.ElementAtOrDefault(0);
                        ResetUnplayedClips();
                        return randomClip;
                    }
                    // ShuffleMode play
                    if (_unplayedClipsList.Count < 1)
                    {
                        ResetUnplayedClips();
                    }

                    // After playing the last clip of the unplayed clips list and resetting it can theoretically happen that
                    // the next random clip is the same as the last random clip. If that happens lets repeat randomization
                    // until we get another clip index.
                    randomIndex = UnityEngine.Random.Range(0, _unplayedClipsList.Count);
                    if (_clips.Count > 1)
                    {
                        var safety = 0;
                        while (randomIndex == _lastPlayedClipIndex)
                        {
                            randomIndex = UnityEngine.Random.Range(0, _unplayedClipsList.Count);
                            if (++safety < 100) continue;
                            return null;
                        }
                    }
                    randomClip = _clips.ElementAtOrDefault(_unplayedClipsList.ElementAtOrDefault(randomIndex));

                    // Remove played clip from unplayedClipsList list
                    _unplayedClipsList.RemoveAt(randomIndex);
                }
                else
                {
                    // Standard play
                    randomIndex = UnityEngine.Random.Range(0, _clips.Count);
                    randomClip = _clips.ElementAtOrDefault(randomIndex);
                }

                // Set last clip index to keep track of the current position
                _lastPlayedClipIndex = randomIndex;
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateClipCollection)}.{nameof(GetRandomClip)}: randomIndex: {randomIndex} {e}");
            }

            return randomClip;
        }

        /**
         * Set the audio source controller which plays the clips
         */
        public void SyncAudioReceiver()
        {
            if (ReceiverAtom == null || ReceiverNode == null) return;
            try
            {
                Receiver =
                    SuperController.singleton.GetAtomByUid(ReceiverAtom)?.GetStorableByID(ReceiverNode)
                        as AudioSourceControl;
                if ((UnityEngine.Object) Receiver == (UnityEngine.Object) null)
                {
                    _controller.Log("Invalid Audio Controller!");
                }
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateController)}.{nameof(SyncAudioReceiver)}: {e}");
            }
        }

        /**
		* Play or queue a randomly selected clip from the collection.
		*/
        public void PlayRandomClip() {
            try
            {
                if (!Enabled) return;
                if (_clips.Count == 0 || (UnityEngine.Object) Receiver == (UnityEngine.Object) null) return;

                var nextClip = GetRandomClip();
                if (AlwaysQueue)
                {
                    nextClip?.Queue(Receiver);
                } else {
                    nextClip?.Play(Receiver, OnlyIfClear);
                }
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateClipCollection)}.{nameof(PlayRandomClip)}: {e}");
            }
        }

        public void QueueRandomClip() {
            try {
                if (!Enabled) return;
                if (_clips.Count < 1 || Receiver == null) return;

                var nextClip = GetRandomClip();
                nextClip?.Queue(Receiver);
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(AudioMateClipCollection)}.{nameof(QueueRandomClip)}: {e}");
            }
        }

        /**
         * Reset the unplayed clips list, which is used for the shuffle mode. The list is a simple integer index list
         * which gets updated after every play of a clip (clip index gets removed).
         */
        private void ResetUnplayedClips()
        {
            _unplayedClipsList.Clear();
            if (_clips == null) return;
            for (var i = 0; i < _clips.Count; i++)
            {
                _unplayedClipsList.Add(i);
            }
        }

        public override string ToString()
        {
            return _clips.Aggregate("", (current, clip) => current + (clip.DisplayName + Environment.NewLine));
        }

        private void Log(string message)
        {
            _controller.Log(message);
        }
    }
}

using System;
using uFileBrowser;
using UnityEngine;
using UnityEngine.Events;

namespace AudioMate
{
    public class FileManager : MonoBehaviour
    {
        private JSONStorableString _storableJSON;
        private bool _isInitialized;
        private bool _isLoading;

        public class NewFilesImportedEvent : UnityEvent { }
        public readonly NewFilesImportedEvent OnNewFilesImported = new NewFilesImportedEvent();


        public FileManager()
        {
            _storableJSON = null;
            _isInitialized = false;
            _isLoading = false;
        }

        public void Bind(JSONStorableString storable)
        {
            _storableJSON = storable;
            _isInitialized = true;
        }

        public void OpenImportFolderDialog()
        {
            SuperController.singleton.GetDirectoryPathDialog(new FileBrowserCallback(ImportFolder), "Custom/Sounds");
        }

        public void OpenImportFileDialog()
        {
            SuperController.singleton.GetMediaPathDialog(new FileBrowserCallback(ImportFile), "mp3|ogg|wav", "Custom/Sounds");
        }

        private void ImportFolder(string path)
        {
            if (!_isInitialized || _isLoading || _storableJSON == null) return;
            try
            {
                _isLoading = true;
                SuperController.singleton.GetFilesAtPath(path).ToList().ForEach((string fileName) =>
                {
                    var isValid = !fileName.Contains(".json") &&
                                  (fileName.Contains(".mp3") || fileName.Contains(".wav") || fileName.Contains(".ogg"));
                    if (!isValid) return;

                    Load(fileName);
                });
                OnNewFilesImported.Invoke();
            }
            catch (Exception e)
            {
                SuperController.LogError($"AudioMate.{nameof(FileManager)}.{nameof(ImportFolder)}: {e}");
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void ImportFile(string path)
        {
            Load(path);
            OnNewFilesImported.Invoke();
        }

        private static void Load(string path)
        {
            var localPath = SuperController.singleton.NormalizeLoadPath(path);

            var existing = URLAudioClipManager.singleton.GetClip(localPath);
            if (existing != null) return;

            URLAudioClipManager.singleton.QueueClip(SuperController.singleton.NormalizeMediaPath(path));
        }
    }
}

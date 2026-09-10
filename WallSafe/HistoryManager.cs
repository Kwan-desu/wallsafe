using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace WallSafe
{
    public class HistoryManager
    {
        public static readonly HistoryManager Instance = new();

        private readonly List<PostItem> _history = new();
        private readonly object _lock = new();

        public event Action? HistoryChanged;

        private static string HistoryFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallSafe", "history.json");

        private HistoryManager()
        {
            Load();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(HistoryFilePath))
                {
                    var json = File.ReadAllText(HistoryFilePath);
                    var items = JsonConvert.DeserializeObject<List<PostItem>>(json);
                    if (items != null)
                    {
                        lock (_lock)
                        {
                            _history.Clear();
                            _history.AddRange(items);
                        }
                    }
                }
            }
            catch { }
        }

        public void Save()
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(HistoryFilePath)!);
                    File.WriteAllText(HistoryFilePath, JsonConvert.SerializeObject(_history, Formatting.Indented));
                }
                HistoryChanged?.Invoke();
            }
            catch { }
        }

        public void RecordApplied(PostItem post, string? appliedPath = null)
        {
            if (post == null) return;

            lock (_lock)
            {
                // Remove existing entry if already in history so we can move it to the top
                _history.RemoveAll(h => h.Id == post.Id && h.Source.Equals(post.Source, StringComparison.OrdinalIgnoreCase));

                post.AppliedAt = DateTime.Now;
                if (!string.IsNullOrEmpty(appliedPath))
                {
                    post.LocalPath = appliedPath;
                }

                _history.Insert(0, post);

                // Retain up to 200 items in history
                if (_history.Count > 200)
                {
                    _history.RemoveRange(200, _history.Count - 200);
                }
            }

            Save();
        }

        public List<PostItem> GetAllHistory()
        {
            lock (_lock)
            {
                return new List<PostItem>(_history);
            }
        }

        public int GetCount()
        {
            lock (_lock)
            {
                return _history.Count;
            }
        }

        public void ClearHistory()
        {
            lock (_lock)
            {
                _history.Clear();
            }
            Save();
        }

        /// <summary>Restore a previously-captured history snapshot (used for Undo).</summary>
        public void RestoreHistory(List<PostItem> snapshot)
        {
            if (snapshot == null) return;
            lock (_lock)
            {
                _history.Clear();
                _history.AddRange(snapshot);
            }
            Save();
        }
    }
}

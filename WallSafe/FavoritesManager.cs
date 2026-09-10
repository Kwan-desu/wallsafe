using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace WallSafe
{
    public class FavoritesManager
    {
        public static readonly FavoritesManager Instance = new();

        private readonly List<PostItem> _favorites = new();
        private readonly object _lock = new();

        // Named collections/folders users can organize favorites into.
        private readonly List<string> _collections = new();

        public event Action? FavoritesChanged;

        private static string FavoritesFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallSafe", "favorites.json");

        private static string CollectionsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallSafe", "favorite_collections.json");

        private FavoritesManager()
        {
            Load();
        }

        private void Load()
        {
            try
            {
                if (File.Exists(FavoritesFilePath))
                {
                    var json = File.ReadAllText(FavoritesFilePath);
                    var items = JsonConvert.DeserializeObject<List<PostItem>>(json);
                    if (items != null)
                    {
                        lock (_lock)
                        {
                            _favorites.Clear();
                            _favorites.AddRange(items);
                        }
                    }
                }
            }
            catch { }

            // Load named collections (explicit list so empty collections persist too).
            try
            {
                if (File.Exists(CollectionsFilePath))
                {
                    var names = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(CollectionsFilePath));
                    if (names != null)
                    {
                        lock (_lock)
                        {
                            _collections.Clear();
                            foreach (var n in names)
                                if (!string.IsNullOrWhiteSpace(n) && !_collections.Contains(n))
                                    _collections.Add(n);
                        }
                    }
                }
            }
            catch { }

            // Ensure any collection referenced by a favorite is in the list.
            lock (_lock)
            {
                foreach (var f in _favorites)
                    if (!string.IsNullOrWhiteSpace(f.Collection) && !_collections.Contains(f.Collection))
                        _collections.Add(f.Collection);
            }
        }

        private void SaveCollections()
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(CollectionsFilePath)!);
                    File.WriteAllText(CollectionsFilePath, JsonConvert.SerializeObject(_collections, Formatting.Indented));
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
                    Directory.CreateDirectory(Path.GetDirectoryName(FavoritesFilePath)!);
                    File.WriteAllText(FavoritesFilePath, JsonConvert.SerializeObject(_favorites, Formatting.Indented));
                }
                FavoritesChanged?.Invoke();
            }
            catch { }
        }

        public bool IsFavorite(int id, string source)
        {
            lock (_lock)
            {
                return _favorites.Any(f => f.Id == id && f.Source.Equals(source, StringComparison.OrdinalIgnoreCase));
            }
        }

        public void AddFavorite(PostItem post)
        {
            lock (_lock)
            {
                if (!_favorites.Any(f => f.Id == post.Id && f.Source.Equals(post.Source, StringComparison.OrdinalIgnoreCase)))
                {
                    post.IsFavorite = true;
                    _favorites.Insert(0, post);
                }
            }
            Save();
        }

        public void RemoveFavorite(int id, string source)
        {
            lock (_lock)
            {
                var item = _favorites.FirstOrDefault(f => f.Id == id && f.Source.Equals(source, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                {
                    item.IsFavorite = false;
                    _favorites.Remove(item);
                }
            }
            Save();
        }

        public bool ToggleFavorite(PostItem post)
        {
            bool nowFav;
            if (IsFavorite(post.Id, post.Source))
            {
                RemoveFavorite(post.Id, post.Source);
                nowFav = false;
            }
            else
            {
                AddFavorite(post);
                nowFav = true;
            }
            post.IsFavorite = nowFav;
            return nowFav;
        }

        public List<PostItem> GetAllFavorites()
        {
            lock (_lock)
            {
                return _favorites.Select(f => { f.IsFavorite = true; return f; }).ToList();
            }
        }

        // ─────────────── Collections / folders ───────────────

        /// <summary>Names of all user collections (folders).</summary>
        public List<string> GetCollections()
        {
            lock (_lock) return new List<string>(_collections);
        }

        /// <summary>Move a collection to a new index to reorder the folder list.</summary>
        public void MoveCollection(string name, int newIndex)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            lock (_lock)
            {
                int oldIndex = _collections.FindIndex(c => c.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (oldIndex < 0) return;
                newIndex = System.Math.Clamp(newIndex, 0, _collections.Count - 1);
                if (newIndex == oldIndex) return;
                var item = _collections[oldIndex];
                _collections.RemoveAt(oldIndex);
                _collections.Insert(newIndex, item);
            }
            SaveCollections();
            FavoritesChanged?.Invoke();
        }

        public bool CreateCollection(string name)
        {
            name = name?.Trim() ?? "";
            if (string.IsNullOrEmpty(name)) return false;
            lock (_lock)
            {
                if (_collections.Contains(name, StringComparer.OrdinalIgnoreCase)) return false;
                _collections.Add(name);
            }
            SaveCollections();
            FavoritesChanged?.Invoke();
            return true;
        }

        public void DeleteCollection(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            lock (_lock)
            {
                _collections.RemoveAll(c => c.Equals(name, StringComparison.OrdinalIgnoreCase));
                // Un-assign items that were in this collection (they remain favorites, uncategorized).
                foreach (var f in _favorites)
                    if (f.Collection.Equals(name, StringComparison.OrdinalIgnoreCase))
                        f.Collection = "";
            }
            SaveCollections();
            Save();
        }

        public bool RenameCollection(string oldName, string newName)
        {
            newName = newName?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrEmpty(newName)) return false;
            lock (_lock)
            {
                if (_collections.Contains(newName, StringComparer.OrdinalIgnoreCase)) return false;
                int idx = _collections.FindIndex(c => c.Equals(oldName, StringComparison.OrdinalIgnoreCase));
                if (idx < 0) return false;
                _collections[idx] = newName;
                foreach (var f in _favorites)
                    if (f.Collection.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                        f.Collection = newName;
            }
            SaveCollections();
            Save();
            return true;
        }

        /// <summary>Assign a favorite to a collection (empty string = uncategorized). Adds it as a favorite first if needed.</summary>
        public void AssignToCollection(PostItem post, string collection)
        {
            if (post == null) return;
            collection = collection?.Trim() ?? "";

            lock (_lock)
            {
                if (!string.IsNullOrEmpty(collection) && !_collections.Contains(collection, StringComparer.OrdinalIgnoreCase))
                    _collections.Add(collection);

                var item = _favorites.FirstOrDefault(f => f.Id == post.Id && f.Source.Equals(post.Source, StringComparison.OrdinalIgnoreCase));
                if (item == null)
                {
                    post.IsFavorite = true;
                    post.Collection = collection;
                    _favorites.Insert(0, post);
                }
                else
                {
                    item.Collection = collection;
                }
            }
            SaveCollections();
            Save();
        }

        /// <summary>Favorites not assigned to any collection (the "Unsorted" pool).</summary>
        public List<PostItem> GetUncategorizedFavorites()
        {
            lock (_lock)
            {
                return _favorites.Where(f => string.IsNullOrWhiteSpace(f.Collection))
                    .Select(f => { f.IsFavorite = true; return f; }).ToList();
            }
        }

        /// <summary>Preview image URL for a collection (first item's thumbnail). Empty name = uncategorized cover.</summary>
        public string GetCollectionCover(string? collection)
        {
            lock (_lock)
            {
                PostItem? first = string.IsNullOrWhiteSpace(collection)
                    ? _favorites.FirstOrDefault(f => string.IsNullOrWhiteSpace(f.Collection))
                    : _favorites.FirstOrDefault(f => f.Collection.Equals(collection, StringComparison.OrdinalIgnoreCase));
                return first?.PreviewUrl ?? "";
            }
        }

        /// <summary>Return favorites in a given collection. Empty/null name returns all favorites.</summary>
        public List<PostItem> GetFavoritesByCollection(string? collection)
        {
            lock (_lock)
            {
                IEnumerable<PostItem> q = _favorites;
                if (!string.IsNullOrWhiteSpace(collection))
                    q = q.Where(f => f.Collection.Equals(collection, StringComparison.OrdinalIgnoreCase));
                return q.Select(f => { f.IsFavorite = true; return f; }).ToList();
            }
        }
    }
}

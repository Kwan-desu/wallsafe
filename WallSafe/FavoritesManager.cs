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

        public event Action? FavoritesChanged;

        private static string FavoritesFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallSafe", "favorites.json");

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
    }
}

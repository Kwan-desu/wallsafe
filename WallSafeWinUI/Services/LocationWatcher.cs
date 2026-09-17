using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Devices.Geolocation;

namespace WallSafeWinUI.Services
{
    /// <summary>
    /// Location-triggered auto panic: when the device enters the configured area
    /// (latitude/longitude + radius), applies the safe wallpaper; optionally restores
    /// on leaving. Mirrors the Wi-Fi watcher's rising/falling-edge behavior.
    ///
    /// Uses the Windows Geolocator. Location access must be granted by the user; if
    /// permission is denied or unavailable, the watcher is a no-op.
    /// </summary>
    public sealed class LocationWatcher : IDisposable
    {
        private static LocationWatcher? _instance;
        public static LocationWatcher Instance => _instance ??= new LocationWatcher();

        private Geolocator? _geolocator;
        private bool _insideArea;
        private string? _wallpaperBeforeTrigger;
        private bool _disposed;
        private readonly object _sync = new();

        private LocationWatcher() { }

        public async Task StartAsync()
        {
            Stop();
            if (_disposed || !Settings.Instance.LocationAutoWallpaperEnabled) return;

            try
            {
                var access = await Geolocator.RequestAccessAsync();
                if (access != GeolocationAccessStatus.Allowed) return;

                _geolocator = new Geolocator
                {
                    DesiredAccuracy = PositionAccuracy.High,
                    MovementThreshold = 20, // meters
                    ReportInterval = 5000
                };
                _geolocator.PositionChanged += OnPositionChanged;
            }
            catch { }
        }

        public void Stop()
        {
            lock (_sync)
            {
                if (_geolocator != null)
                {
                    try { _geolocator.PositionChanged -= OnPositionChanged; } catch { }
                    _geolocator = null;
                }
            }
        }

        /// <summary>Try to read a single current position (for the "use current location" button).</summary>
        public static async Task<(double Lat, double Lon)?> GetCurrentAsync()
        {
            try
            {
                var access = await Geolocator.RequestAccessAsync();
                if (access != GeolocationAccessStatus.Allowed) return null;
                var geo = new Geolocator { DesiredAccuracy = PositionAccuracy.High };
                var pos = await geo.GetGeopositionAsync();
                var c = pos.Coordinate.Point.Position;
                return (c.Latitude, c.Longitude);
            }
            catch { return null; }
        }

        private void OnPositionChanged(Geolocator sender, PositionChangedEventArgs args)
        {
            try
            {
                var c = args.Position.Coordinate.Point.Position;
                double distance = HaversineMeters(
                    c.Latitude, c.Longitude,
                    Settings.Instance.LocationLatitude, Settings.Instance.LocationLongitude);

                bool inside = distance <= Math.Max(20, Settings.Instance.LocationRadiusMeters);

                if (inside && !_insideArea)
                {
                    _insideArea = true;
                    ApplyTriggerWallpaper();
                }
                else if (!inside && _insideArea)
                {
                    _insideArea = false;
                    if (Settings.Instance.LocationRestoreOnLeave && !string.IsNullOrEmpty(_wallpaperBeforeTrigger))
                    {
                        var prev = _wallpaperBeforeTrigger;
                        UiDispatch.Post(() =>
                        {
                            if (File.Exists(prev!))
                                WallpaperManager.Instance.ApplyWallpaperFromPath(prev!, Settings.Instance.TargetMonitor);
                        });
                    }
                }
            }
            catch { }
        }

        private void ApplyTriggerWallpaper()
        {
            try { _wallpaperBeforeTrigger = WallpaperManager.GetCurrentWallpaper(); } catch { }
            UiDispatch.Post(() => WallpaperManager.Instance.ApplySafeWallpaper());
        }

        private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000.0;
            double dLat = DegToRad(lat2 - lat1);
            double dLon = DegToRad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(DegToRad(lat1)) * Math.Cos(DegToRad(lat2)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private static double DegToRad(double d) => d * Math.PI / 180.0;

        public void Dispose()
        {
            _disposed = true;
            Stop();
        }
    }
}

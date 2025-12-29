using System.Text.Json;
using System.Text.Json.Serialization;

namespace Outgrowth.Services;

/// <summary>
/// Persistent timer that tracks elapsed time even when the game is closed
/// Saves and loads last timestamp from app data directory
/// Uses singleton pattern for global access
/// </summary>
public class PersistentTimer
{
    private static PersistentTimer? _instance;
    private readonly string _saveFilePath;
    private DateTime _lastSaveTime;
    private DateTime _startTime;
    private bool _isRunning;
    
    /// <summary>
    /// Gets the singleton instance of PersistentTimer
    /// </summary>
    public static PersistentTimer Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new PersistentTimer();
            }
            return _instance;
        }
    }
    
    /// <summary>
    /// Gets the total elapsed time since the timer was started (including persisted time)
    /// </summary>
    public TimeSpan TotalElapsedTime
    {
        get
        {
            if (!_isRunning)
                return _lastSaveTime - _startTime;
            
            return DateTime.UtcNow - _startTime;
        }
    }
    
    /// <summary>
    /// Gets the elapsed time in seconds
    /// </summary>
    public double ElapsedSeconds => TotalElapsedTime.TotalSeconds;
    
    /// <summary>
    /// Gets whether the timer is currently running
    /// </summary>
    public bool IsRunning => _isRunning;
    
    private PersistentTimer(string saveFileName = "gametimer.json")
    {
        _saveFilePath = Path.Combine(FileSystem.AppDataDirectory, saveFileName);
        _isRunning = false;
        _startTime = DateTime.UtcNow;
        _lastSaveTime = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Starts the timer and loads persisted time if available
    /// </summary>
    public void Start()
    {
        System.Diagnostics.Debug.WriteLine("[PersistentTimer] Start() called");
        LoadPersistedTime();
        _isRunning = true;
        System.Diagnostics.Debug.WriteLine($"[PersistentTimer] Timer started. StartTime: {_startTime:yyyy-MM-dd HH:mm:ss}, IsRunning: {_isRunning}");
    }
    
    /// <summary>
    /// Stops the timer and saves current time
    /// </summary>
    public void Stop()
    {
        _isRunning = false;
        SavePersistedTime();
    }
    
    /// <summary>
    /// Resets the timer to zero
    /// </summary>
    public void Reset()
    {
        _isRunning = false;
        _startTime = DateTime.UtcNow;
        _lastSaveTime = DateTime.UtcNow;
        SavePersistedTime();
    }
    
    /// <summary>
    /// Saves current timer state to disk
    /// </summary>
    public void SavePersistedTime()
    {
        try
        {
            var timerData = new TimerData
            {
                StartTime = _startTime,
                LastSaveTime = DateTime.UtcNow
            };
            
            var json = JsonSerializer.Serialize(timerData, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            // Write to file synchronously and flush to ensure data is written to disk
            using (var fileStream = new FileStream(_saveFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(fileStream))
            {
                writer.Write(json);
                writer.Flush();
                fileStream.Flush(true); // Flush to disk
            }
            
            _lastSaveTime = timerData.LastSaveTime;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving timer data: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Loads persisted timer state from disk
    /// </summary>
    private void LoadPersistedTime()
    {
        try
        {
            if (File.Exists(_saveFilePath))
            {
                var json = File.ReadAllText(_saveFilePath);
                var timerData = JsonSerializer.Deserialize<TimerData>(json);
                
                if (timerData != null)
                {
                    _startTime = timerData.StartTime;
                    _lastSaveTime = timerData.LastSaveTime;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading timer data: {ex.Message}");
            // If loading fails, start fresh
            _startTime = DateTime.UtcNow;
            _lastSaveTime = DateTime.UtcNow;
        }
    }
    
    /// <summary>
    /// Resets the singleton instance (useful for testing or resetting the game)
    /// </summary>
    public static void ResetInstance()
    {
        _instance = null;
    }
    
    /// <summary>
    /// Data structure for persisting timer state
    /// </summary>
    private class TimerData
    {
        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; }
        
        [JsonPropertyName("lastSaveTime")]
        public DateTime LastSaveTime { get; set; }
    }
}


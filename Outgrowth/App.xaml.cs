using Outgrowth.Models;
using Outgrowth.Services;

namespace Outgrowth
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override async void OnStart()
        {
            base.OnStart();
            
            // Start persistent timer
            PersistentTimer.Instance.Start();
            
            // Start periodic plant growth updates
            PlantsManager.Instance.StartPeriodicUpdates();
            
            // Initialize all data libraries from JSON files using GameDataManager (parallel optimized loading)
            try
            {
                await GameDataManager.InitializeAllAsync();
            }
            catch (Exception ex)
            {
                // Log error or handle as needed
                System.Diagnostics.Debug.WriteLine($"Error initializing data libraries: {ex.Message}");
                // In production, you might want to show an error dialog or handle gracefully
            }
        }
        
        protected override void OnSleep()
        {
            base.OnSleep();
            
            // Stop plant growth updates before saving (prevents timer from running while app is closing)
            PlantsManager.Instance.DisposeTimer();
            
            // Save plants and timer state when app goes to sleep (background or closing)
            System.Diagnostics.Debug.WriteLine("[App] OnSleep called - saving game state");
            SaveGameState();
        }
        
        protected override void OnResume()
        {
            base.OnResume();
            
            // Restart timer when app resumes (it will load persisted time)
            PersistentTimer.Instance.Start();
            
            // Restart plant growth updates (timer will be recreated in StartPeriodicUpdates if needed)
            PlantsManager.Instance.StartPeriodicUpdates();
            
            System.Diagnostics.Debug.WriteLine("[App] OnResume called - timers restarted");
        }
        
        /// <summary>
        /// Saves game state (plants and timer) when app goes to sleep or closes
        /// </summary>
        private void SaveGameState()
        {
            try
            {
                // Try to save from GreenhousePage if loaded (most up-to-date state)
                Outgrowth.Views.GreenhousePage.SavePlantsIfLoaded();
                
                // Also try to save from last known mapping (fallback if page not loaded)
                PlantsSaveService.SaveGameState();
                // Save materials state (resources, liquids, seeds)
                GameDataManager.SaveMaterialsState();
                
                // Save timer state
                PersistentTimer.Instance.Stop();
                
                System.Diagnostics.Debug.WriteLine("[App] Game state saved");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Error saving game state: {ex.Message}");
            }
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell());
            
#if WINDOWS
            // Set windowed fullscreen for Windows - window will fill the screen
            var displayInfo = DeviceDisplay.MainDisplayInfo;
            window.Width = displayInfo.Width / displayInfo.Density;
            window.Height = displayInfo.Height / displayInfo.Density;
            window.X = 0;
            window.Y = 0;
#endif
            
            // Subscribe to window destroying event for all platforms to save on close
            window.Destroying += OnWindowDestroying;
            
            return window;
        }
        
        private void OnWindowDestroying(object? sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[App] Window.Destroying called - saving game state");
            
            // Stop plant growth updates before saving (prevents timer from running while app is closing)
            PlantsManager.Instance.DisposeTimer();
            
            SaveGameState();
        }
    }
}
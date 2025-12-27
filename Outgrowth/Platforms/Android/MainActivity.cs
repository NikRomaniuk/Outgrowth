using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;

namespace Outgrowth
{
    [Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ScreenOrientation = ScreenOrientation.SensorLandscape, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
    public class MainActivity : MauiAppCompatActivity
    {
        protected override void OnCreate(Bundle? savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            
            // Enable immersive fullscreen mode to hide status bar and navigation bar
            EnableImmersiveMode();
        }

        public override void OnWindowFocusChanged(bool hasFocus)
        {
            base.OnWindowFocusChanged(hasFocus);
            
            // Re-enable immersive mode when window gains focus (e.g., after returning from another app)
            if (hasFocus)
            {
                EnableImmersiveMode();
            }
        }

        private void EnableImmersiveMode()
        {
            var window = Window;
            if (window == null) return;

            var decorView = window.DecorView;
            var insetsController = WindowCompat.GetInsetsController(window, decorView);

            if (insetsController != null)
            {
                // Hide system bars (status bar and navigation bar)
                insetsController.Hide(WindowInsetsCompat.Type.SystemBars());
                
                // Enable immersive sticky mode - bars stay hidden until user swipes
                insetsController.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
            }
            else
            {
                // Fallback for older Android versions
                var flags = (int)(SystemUiFlags.Fullscreen 
                    | SystemUiFlags.HideNavigation 
                    | SystemUiFlags.ImmersiveSticky);
                decorView.SystemUiVisibility = (StatusBarVisibility)flags;
            }
        }
    }
}

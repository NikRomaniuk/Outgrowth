namespace Outgrowth.Services;

// Animated page navigation with fade transitions
public class NavigationService
{
#if WINDOWS
    // Windows uses single shared overlay moved between pages
    private static BoxView? _fadeOverlay;
    private static Shell? _shell;
    private static bool _isNavigating = false;
    
    public static void Initialize(BoxView fadeOverlay, Shell shell)
    {
        _fadeOverlay = fadeOverlay;
        _shell = shell;
        _fadeOverlay.Opacity = 0;
        _fadeOverlay.IsVisible = false;
        _fadeOverlay.InputTransparent = false;
        
        shell.Navigated += OnNavigated;
    }
    
    private static void OnNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        AttachOverlayToCurrentPage();
    }
    
    private static void AttachOverlayToCurrentPage()
    {
        if (_shell?.CurrentPage == null || _fadeOverlay == null)
            return;
        
        try
        {
            var page = _shell.CurrentPage;
            
            if (page is ContentPage contentPage && contentPage.Content is Grid rootGrid)
            {
                // Remove from previous parent if needed
                if (_fadeOverlay.Parent is Grid oldGrid && oldGrid != rootGrid)
                {
                    oldGrid.Children.Remove(_fadeOverlay);
                }
                
                if (rootGrid.Children.Contains(_fadeOverlay))
                    return;
                
                if (rootGrid.ColumnDefinitions.Count > 0)
                    Grid.SetColumnSpan(_fadeOverlay, rootGrid.ColumnDefinitions.Count);
                
                if (rootGrid.RowDefinitions.Count > 0)
                    Grid.SetRowSpan(_fadeOverlay, rootGrid.RowDefinitions.Count);
                
                _fadeOverlay.ZIndex = 10000;
                rootGrid.Children.Add(_fadeOverlay);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error attaching overlay: {ex.Message}");
        }
    }
    
    public static async Task NavigateWithFadeAsync(string route, uint fadeDuration = 250, uint pauseDuration = 100)
    {
        if (_isNavigating || _fadeOverlay == null || _shell == null)
            return;
        
        _isNavigating = true;
        
        try
        {
            AttachOverlayToCurrentPage();
            await Task.Delay(50);
            
            _fadeOverlay.InputTransparent = false;
            _fadeOverlay.IsVisible = true;
            _fadeOverlay.Opacity = 0;
            await _fadeOverlay.FadeTo(1.0, fadeDuration, Easing.CubicInOut);
            
            await Task.Delay((int)pauseDuration);
            await _shell.GoToAsync(route);
            await Task.Delay(200);
            
            if (_fadeOverlay.IsVisible)
            {
                _fadeOverlay.Opacity = 1.0;
                await _fadeOverlay.FadeTo(0, fadeDuration, Easing.CubicInOut);
            }
            
            _fadeOverlay.IsVisible = false;
            _fadeOverlay.InputTransparent = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation error: {ex.Message}");
            
            if (_fadeOverlay != null)
            {
                _fadeOverlay.Opacity = 0;
                _fadeOverlay.IsVisible = false;
                _fadeOverlay.InputTransparent = true;
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }
#elif ANDROID
    // Android uses static overlays on each page
    private static Shell? _shell;
    private static bool _isNavigating = false;
    
    public static void Initialize(BoxView fadeOverlay, Shell shell)
    {
        _shell = shell;
        // Each page has its own overlay in XAML
    }
    
    public static async Task NavigateWithFadeAsync(string route, uint fadeDuration = 200, uint pauseDuration = 50)
    {
        if (_isNavigating || _shell == null)
            return;
        
        _isNavigating = true;
        
        try
        {
            var currentOverlay = GetPageOverlay(_shell.CurrentPage);
            
            if (currentOverlay != null)
            {
                currentOverlay.InputTransparent = false;
                currentOverlay.IsVisible = true;
                currentOverlay.Opacity = 0;
                await currentOverlay.FadeTo(1.0, fadeDuration, Easing.SinOut);
                await Task.Delay((int)pauseDuration);
            }
            else
            {
                await Task.Delay((int)(fadeDuration + pauseDuration));
            }
            
            await _shell.GoToAsync(route);
            await Task.Delay(30);
            
            var newOverlay = GetPageOverlay(_shell.CurrentPage);
            
            if (newOverlay != null)
            {
                // Set black immediately before page renders
                newOverlay.IsVisible = true;
                newOverlay.Opacity = 1.0;
                newOverlay.InputTransparent = false;
                
                await Task.Delay(300);
                await newOverlay.FadeTo(0, fadeDuration, Easing.SinIn);
                
                newOverlay.IsVisible = false;
                newOverlay.InputTransparent = true;
            }
            else
            {
                await Task.Delay(300 + (int)fadeDuration);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Navigation error (Android): {ex.Message}");
            
            var currentOverlay = GetPageOverlay(_shell?.CurrentPage);
            if (currentOverlay != null)
            {
                currentOverlay.Opacity = 0;
                currentOverlay.IsVisible = false;
                currentOverlay.InputTransparent = true;
            }
        }
        finally
        {
            _isNavigating = false;
        }
    }
    
    private static BoxView? GetPageOverlay(Page? page)
    {
        if (page is ContentPage contentPage && contentPage.Content is Grid rootGrid)
        {
            foreach (var child in rootGrid.Children)
            {
                if (child is BoxView boxView && 
                    boxView.Color == Colors.Black && 
                    boxView.ZIndex == 10000)
                {
                    return boxView;
                }
            }
        }
        
        return null;
    }
#endif
}


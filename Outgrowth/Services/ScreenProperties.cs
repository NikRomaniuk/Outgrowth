using Microsoft.Maui.Controls;

namespace Outgrowth.Services;

// Centralized screen size and scale calculations
public class ScreenProperties
{
    private static ScreenProperties? _instance;
    
    public double PageWidth { get; private set; }
    public double PageHeight { get; private set; }
    
    public double TargetWidth { get; private set; }
    public double TargetHeight { get; private set; }
    
    public const double ReferenceWidth = 1920.0;
    public const double ReferenceHeight = 1080.0;
    
    public double Scale { get; private set; }
    public double ScaledWidth { get; private set; }
    public double ScaledHeight { get; private set; }
    
    public double OffsetX { get; private set; }
    public double OffsetY { get; private set; }
    
    public const double WindowsBaseWidth = 1920.0;
    public double AdaptiveScale { get; private set; }
    
    private ScreenProperties()
    {
    }
    
    public static ScreenProperties Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new ScreenProperties();
            }
            return _instance;
        }
    }
    
    public void UpdateScreenProperties(double pageWidth, double pageHeight)
    {
        if (pageWidth <= 0 || pageHeight <= 0)
            return;
        
        PageWidth = pageWidth;
        PageHeight = pageHeight;
        
        // Calculate 16:9 target size
        TargetHeight = pageHeight;
        TargetWidth = TargetHeight * 16.0 / 9.0;
        
        // Scale by width if it exceeds screen
        if (TargetWidth > pageWidth)
        {
            TargetWidth = pageWidth;
            TargetHeight = TargetWidth * 9.0 / 16.0;
        }
        
        // Scale = min(scaleX, scaleY) to fit within available space
        var scaleX = TargetWidth / ReferenceWidth;
        var scaleY = TargetHeight / ReferenceHeight;
        Scale = Math.Min(scaleX, scaleY);
        
        ScaledWidth = ReferenceWidth * Scale;
        ScaledHeight = ReferenceHeight * Scale;
        
        // Center offset after scaling
        OffsetX = (TargetWidth - ScaledWidth) / 2.0;
        OffsetY = (TargetHeight - ScaledHeight) / 2.0;
        
        // Adaptive scale: Windows 1920px = 1.0
        AdaptiveScale = pageWidth / WindowsBaseWidth;
    }

    /// <summary>
    /// Update dynamic resource font sizes based on adaptive scale.
    /// This centralizes font/size calculations so pages don't duplicate the logic.
    /// It updates the application-level ResourceDictionary entries used by panels.
    /// </summary>
    /// <param name="adaptiveScale">Scale factor where 1.0 == Windows 1920px baseline.</param>
    public void UpdateFontSizes(double adaptiveScale)
    {
        const double baseTitleSize = 30.0;
        const double baseBodySize = 20.0;
        const double baseQtySize = 20.0;
        const double baseIconSize = 80.0;

        var resources = Application.Current?.Resources;
        if (resources == null)
            return;

        resources["ResourcePanelTitleSize"] = baseTitleSize * adaptiveScale;
        resources["ResourcePanelBodySize"] = baseBodySize * adaptiveScale;
        resources["ResourcePanelQtySize"] = baseQtySize * adaptiveScale;
        resources["ResourcePanelIconSize"] = baseIconSize * adaptiveScale;
        // Set font family resources (registered in MauiProgram fonts)
        // Use SilkscreenBold for titles and SilkscreenRegular for body/qty
        if (!resources.ContainsKey("ResourcePanelTitleFont"))
            resources["ResourcePanelTitleFont"] = "SilkscreenBold";
        else
            resources["ResourcePanelTitleFont"] = "SilkscreenBold";

        if (!resources.ContainsKey("ResourcePanelBodyFont"))
            resources["ResourcePanelBodyFont"] = "SilkscreenRegular";
        else
            resources["ResourcePanelBodyFont"] = "SilkscreenRegular";

        if (!resources.ContainsKey("ResourcePanelQtyFont"))
            resources["ResourcePanelQtyFont"] = "SilkscreenRegular";
        else
            resources["ResourcePanelQtyFont"] = "SilkscreenRegular";
    }
    
    public static void Reset()
    {
        _instance = null;
    }
}


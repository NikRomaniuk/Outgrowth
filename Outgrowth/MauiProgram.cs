using Microsoft.Extensions.Logging;
using Outgrowth.Services;

namespace Outgrowth
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    // Add Silkscreen fonts for resource panels
                    fonts.AddFont("Silkscreen-Regular.ttf", "SilkscreenRegular");
                    fonts.AddFont("Silkscreen-Bold.ttf", "SilkscreenBold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}

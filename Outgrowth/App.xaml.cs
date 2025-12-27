namespace Outgrowth
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
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
            
            return window;
        }
    }
}
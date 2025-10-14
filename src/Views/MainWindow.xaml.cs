using System;
using System.Windows;
using Serilog;
using Syncfusion.Windows.Tools.Controls;

namespace WileyWidget
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            Log.Debug("MainWindow: Constructor called");
            
            InitializeComponent();
            
            Log.Debug("MainWindow: InitializeComponent completed");
            
            // Configure DockingManager after initialization
            ConfigureDockingManager();
            
            // Add event handlers for diagnostics
            Loaded += MainWindow_Loaded;
            SizeChanged += MainWindow_SizeChanged;
            Activated += MainWindow_Activated;
            ContentRendered += MainWindow_ContentRendered;
            
            Log.Debug("MainWindow: Constructor completed, event handlers attached");
        }

        private void ConfigureDockingManager()
        {
            try
            {
                // Configure DockingManager properties as per Syncfusion documentation
                MainDockingManager.UseDocumentContainer = true;
                MainDockingManager.ContainerMode = DocumentContainerMode.TDI;
                MainDockingManager.PersistState = true;
                
                Log.Information("DockingManager configured successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to configure DockingManager");
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Log.Information("MainWindow: Loaded event - Size: {Width}x{Height}, Position: ({Left}, {Top}), State: {State}, Visible: {IsVisible}",
                ActualWidth, ActualHeight, Left, Top, WindowState, IsVisible);
                
            // Log content control state
            var contentControl = Content as System.Windows.Controls.ContentControl;
            if (contentControl != null)
            {
                Log.Information("MainWindow: ContentControl - ActualSize: {Width}x{Height}, Content: {ContentType}",
                    contentControl.ActualWidth, contentControl.ActualHeight, 
                    contentControl.Content?.GetType().Name ?? "null");
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Log.Debug("MainWindow: SizeChanged - NewSize: {Width}x{Height}, PreviousSize: {PrevWidth}x{PrevHeight}",
                e.NewSize.Width, e.NewSize.Height, e.PreviousSize.Width, e.PreviousSize.Height);
        }

        private void MainWindow_Activated(object sender, System.EventArgs e)
        {
            Log.Debug("MainWindow: Activated event - Window brought to foreground");
        }

        private void MainWindow_ContentRendered(object sender, System.EventArgs e)
        {
            Log.Information("MainWindow: ContentRendered event - All content has been rendered");
        }
    }
}
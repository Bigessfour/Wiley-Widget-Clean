using System.Windows;

namespace WileyWidget
{
    public partial class TestWindow : Window
    {
        public TestWindow()
        {
            InitializeComponent();
            System.Console.WriteLine("[DEBUG] TestWindow constructor called successfully");
        }
    }
}
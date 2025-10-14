using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WileyWidget.ViewModels
{
    public partial class MainViewModel : INotifyPropertyChanged
    {
        public MainViewModel()
        {
            // Simple constructor for DI
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

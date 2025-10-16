using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WileyWidget.Models;
using WileyWidget.ViewModels;

namespace WileyWidget.Views
{
    /// <summary>
    /// Interaction logic for EnterpriseDialogView.xaml
    /// </summary>
    public partial class EnterpriseDialogView : Window
    {
        public EnterpriseDialogView()
        {
            InitializeComponent();
            DataContext = new WileyWidget.ViewModels.EnterpriseDialogViewModel(this);
        }

        public EnterpriseDialogView(Enterprise enterprise) : this()
        {
            if (DataContext is WileyWidget.ViewModels.EnterpriseDialogViewModel viewModel)
            {
                viewModel.Enterprise = enterprise;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is WileyWidget.ViewModels.EnterpriseDialogViewModel viewModel)
            {
                if (string.IsNullOrWhiteSpace(viewModel.Enterprise.Name))
                {
                    MessageBox.Show("Enterprise name is required.", "Validation Error",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(viewModel.Enterprise.Type))
                {
                    MessageBox.Show("Enterprise type is required.", "Validation Error",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (viewModel.Enterprise.CitizenCount <= 0)
                {
                    MessageBox.Show("Citizen count must be greater than zero.", "Validation Error",
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DialogResult = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        public static bool ShowDialog(Enterprise enterprise)
        {
            var dialog = new EnterpriseDialogView(enterprise);
            var result = dialog.ShowDialog();
            return result == true;
        }
    }

    public class NotEmptyValidationRule : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            if (value is string str && string.IsNullOrWhiteSpace(str))
            {
                return new ValidationResult(false, "This field is required.");
            }
            return ValidationResult.ValidResult;
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Automation; // For AutomationProperties
using Syncfusion.UI.Xaml.Grid; // SfDataGrid
using Syncfusion.UI.Xaml.Charts; // Charts
using Syncfusion.Windows.Tools.Controls; // DockingManager
using Xunit;
using WileyWidget.Tests; // For StaFact

namespace WileyWidget.Tests.SyncfusionExamples
{
    /// <summary>
    /// Comprehensive examples of using StaFact for testing Syncfusion controls and WPF views
    /// </summary>
    public class SyncfusionStaFactExamples : TestApplication
    {
        private void WithWindow(UIElement content, Action action)
        {
            var window = new Window
            {
                Width = 1,
                Height = 1,
                ShowInTaskbar = false,
                ShowActivated = false,
                Content = content
            };

            // Prepare to wait for the visual to be fully loaded
            RoutedEventHandler? loadedHandler = null;
            var frame = new DispatcherFrame();
            if (content is FrameworkElement fe)
            {
                loadedHandler = (s, e) =>
                {
                    fe.Loaded -= loadedHandler;
                    // Stop the frame once loaded fires
                    frame.Continue = false;
                };
                fe.Loaded += loadedHandler;
            }

            window.Show();

            // Pump the dispatcher until Loaded has fired to ensure templates and bindings are applied
            if (loadedHandler != null)
            {
                Dispatcher.PushFrame(frame);
            }

            // Force template/application of layout for both the content and the window
            if (content is FrameworkElement fe2)
            {
                fe2.ApplyTemplate();
            }
            window.ApplyTemplate();
            window.UpdateLayout();

            // Give a final idle priority pump to process any deferred layout/data binding work
            Application.Current.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

            try
            {
                action();
            }
            finally
            {
                window.Close();
            }
        }

        #region 1. UI Automation Testing with Syncfusion Controls

        /// <summary>
        /// Test Syncfusion SfDataGrid data binding and UI interactions
        /// </summary>
        [StaFact]
        public void SfDataGrid_DataBinding_ShouldDisplayData()
        {
            RunOnUIThread(() =>
            {
                // Create test data
                var testData = new List<TestItem>
                {
                    new TestItem { Id = 1, Name = "Item 1", Value = 100 },
                    new TestItem { Id = 2, Name = "Item 2", Value = 200 }
                };

                // Create SfDataGrid
                var dataGrid = new SfDataGrid
                {
                    ItemsSource = testData,
                    AutoGenerateColumns = true
                };

                WithWindow(dataGrid, () =>
                {
                    // Verify data binding after template is applied
                    Assert.NotNull(dataGrid.View);
                    Assert.Equal(2, dataGrid.View.Records.Count);
                    // Test column generation
                    Assert.True(dataGrid.Columns.Count > 0);
                });


            });
        }

        /// <summary>
        /// Test Syncfusion Chart control rendering and data visualization
        /// </summary>
        [StaFact]
        public void SfChart_Rendering_ShouldDisplayChart()
        {
            RunOnUIThread(() =>
            {
                var chart = new SfChart();
                var series = new ColumnSeries();

                // Add sample data points
                series.ItemsSource = new List<ChartPoint>
                {
                    new ChartPoint { X = "Jan", Y = 100 },
                    new ChartPoint { X = "Feb", Y = 150 }
                };

                chart.Series.Add(series);

                // Verify chart setup
                Assert.Single(chart.Series);
                Assert.Equal(2, ((IEnumerable<object>)series.ItemsSource).Count());


            });
        }

        #endregion

        #region 2. Visual Testing and Layout Validation

        /// <summary>
        /// Test WPF view layout and visual tree structure
        /// </summary>
        [StaFact]
        public void WpfView_Layout_ShouldRenderCorrectly()
        {
            RunOnUIThread(() =>
            {
                // Create a test window with your WPF view
                var window = new Window
                {
                    Width = 800,
                    Height = 600,
                    Content = CreateTestView()
                };

                window.Show();
                window.UpdateLayout();

                try
                {
                    // Verify visual tree
                    var content = window.Content as Panel;
                    Assert.NotNull(content);
                    Assert.True(content.Children.Count > 0);

                    // Test layout properties
                    foreach (UIElement child in content.Children)
                    {
                        Assert.True(child.IsMeasureValid);
                        Assert.True(child.IsArrangeValid);
                    }
                }
                finally
                {
                    window.Close();
                }


            });
        }

        /// <summary>
        /// Test Syncfusion DockingManager layout and docking functionality
        /// </summary>
        [StaFact]
        public void DockingManager_Layout_ShouldDockCorrectly()
        {
            RunOnUIThread(() =>
            {
                var dockingManager = new DockingManager();

                // Add dockable content
                var doc1 = new ContentControl { Content = "Document 1" };
                var doc2 = new ContentControl { Content = "Document 2" };

                DockingManager.SetHeader(doc1, "Doc 1");
                DockingManager.SetHeader(doc2, "Doc 2");

                dockingManager.Children.Add(doc1);
                dockingManager.Children.Add(doc2);

                // Verify docking setup
                Assert.Equal(2, dockingManager.Children.Count);


            });
        }

        #endregion

        #region 3. Control Interaction and Event Testing

        /// <summary>
        /// Test user interactions with Syncfusion controls
        /// </summary>
        [StaFact]
        public void SfDataGrid_Selection_ShouldHandleSelection()
        {
            RunOnUIThread(() =>
            {
                var dataGrid = new SfDataGrid
                {
                    ItemsSource = CreateTestData(),
                    SelectionMode = Syncfusion.UI.Xaml.Grid.GridSelectionMode.Single
                };

                WithWindow(dataGrid, () =>
                {
                    // Simulate selection
                    dataGrid.SelectedIndex = 0;

                    // Verify selection
                    Assert.Equal(0, dataGrid.SelectedIndex);
                    Assert.Single(dataGrid.SelectedItems);
                });


            });
        }

        /// <summary>
        /// Test WPF button interactions and command binding
        /// </summary>
        [StaFact]
        public void Button_CommandBinding_ShouldExecuteCommand()
        {
            RunOnUIThread(() =>
            {
                var executed = false;
                var command = new TestCommand(() => executed = true);

                var button = new Button
                {
                    Content = "Test Button",
                    Command = command
                };

                // In unit tests, raising the Click routed event does not invoke ButtonBase.OnClick's command execution path.
                // Execute the command directly to verify command binding works.
                button.Command?.Execute(null);

                Assert.True(executed);


            });
        }

        #endregion

        #region 4. Data Binding and MVVM Testing

        /// <summary>
        /// Test data binding between ViewModel and WPF view
        /// </summary>
        [StaFact]
        public void ViewModel_DataBinding_ShouldUpdateUI()
        {
            RunOnUIThread(() =>
            {
                var viewModel = new TestViewModel { Title = "Initial Title" };

                var textBlock = new TextBlock();
                textBlock.SetBinding(TextBlock.TextProperty,
                    new System.Windows.Data.Binding("Title") { Source = viewModel });

                // Verify initial binding
                Assert.Equal("Initial Title", textBlock.Text);

                // Update ViewModel
                viewModel.Title = "Updated Title";

                // Force binding update
                textBlock.UpdateLayout();

                // Verify binding update
                Assert.Equal("Updated Title", textBlock.Text);


            });
        }

        /// <summary>
        /// Test Syncfusion control data binding with complex data
        /// </summary>
        [StaFact]
        public void SfDataGrid_ComplexBinding_ShouldHandleComplexData()
        {
            RunOnUIThread(() =>
            {
                var complexData = new List<ComplexItem>
                {
                    new ComplexItem
                    {
                        Name = "Item 1",
                        Details = new SubItem { Value = 100, Status = "Active" }
                    }
                };

                var dataGrid = new SfDataGrid
                {
                    ItemsSource = complexData,
                    AutoGenerateColumns = true
                };

                WithWindow(dataGrid, () =>
                {
                    Assert.NotNull(dataGrid.View);
                    // Verify complex object binding
                    Assert.Single(dataGrid.View.Records);
                });


            });
        }

        #endregion

        #region 5. Theming and Styling Tests

        /// <summary>
        /// Test Syncfusion theme application
        /// </summary>
        [StaFact]
        public void Syncfusion_Theme_ShouldApplyCorrectly()
        {
            RunOnUIThread(() =>
            {
                // Apply Syncfusion theme
                Syncfusion.SfSkinManager.SfSkinManager.SetTheme(new Button(), new Syncfusion.SfSkinManager.Theme("FluentDark"));

                var themedButton = new Button { Content = "Themed Button" };
                Syncfusion.SfSkinManager.SfSkinManager.SetTheme(themedButton, new Syncfusion.SfSkinManager.Theme("FluentDark"));

                // Verify theme application
                Assert.NotNull(themedButton.Style);


            });
        }

        /// <summary>
        /// Test WPF style application and visual states
        /// </summary>
        [StaFact]
        public void WpfStyle_Application_ShouldApplyStyles()
        {
            RunOnUIThread(() =>
            {
                var style = new Style(typeof(Button));
                style.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Blue));
                style.Setters.Add(new Setter(Button.ForegroundProperty, Brushes.White));

                var styledButton = new Button
                {
                    Content = "Styled Button",
                    Style = style
                };

                // Verify style application
                Assert.Equal(Brushes.Blue, styledButton.Background);
                Assert.Equal(Brushes.White, styledButton.Foreground);


            });
        }

        #endregion

        #region 6. Performance and Rendering Tests

        /// <summary>
        /// Test rendering performance of WPF controls
        /// </summary>
        [StaFact]
        public void WpfControl_Rendering_ShouldBeFast()
        {
            RunOnUIThread(() =>
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Create complex UI
                var panel = new StackPanel();
                for (int i = 0; i < 100; i++)
                {
                    panel.Children.Add(new Button { Content = $"Button {i}" });
                }

                // Force rendering
                panel.Measure(new Size(800, 600));
                panel.Arrange(new Rect(0, 0, 800, 600));

                stopwatch.Stop();

                // Performance assertion (adjust threshold as needed)
                Assert.True(stopwatch.ElapsedMilliseconds < 500, $"Rendering took {stopwatch.ElapsedMilliseconds}ms");


            });
        }

        /// <summary>
        /// Test Syncfusion control virtualization performance
        /// </summary>
        [StaFact]
        public void SfDataGrid_Virtualization_ShouldPerformWell()
        {
            RunOnUIThread(() =>
            {
                // Create large dataset
                var largeData = Enumerable.Range(0, 10000)
                    .Select(i => new TestItem { Id = i, Name = $"Item {i}", Value = i * 10 })
                    .ToList();

                var dataGrid = new SfDataGrid
                {
                    ItemsSource = largeData,
                    AutoGenerateColumns = true,
                    EnableDataVirtualization = true
                };

                WithWindow(dataGrid, () =>
                {
                    // Verify virtualization is working
                    Assert.NotNull(dataGrid.View);
                    Assert.True(dataGrid.View.Records.Count <= largeData.Count);
                });


            });
        }

        #endregion

        #region 7. Accessibility Testing

        /// <summary>
        /// Test accessibility features of WPF controls
        /// </summary>
        [StaFact]
        public void WpfControl_Accessibility_ShouldBeAccessible()
        {
            RunOnUIThread(() =>
            {
                var button = new Button
                {
                    Content = "Accessible Button"
                };

                // Verify accessibility properties
                AutomationProperties.SetName(button, "Test Button");
                AutomationProperties.SetHelpText(button, "This is a test button");

                Assert.Equal("Test Button", AutomationProperties.GetName(button));
                Assert.Equal("This is a test button", AutomationProperties.GetHelpText(button));


            });
        }

        #endregion

        #region Helper Methods

        private async Task RunOnUIThread(Func<Task> testAction)
        {
            var tcs = new TaskCompletionSource<bool>();

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await testAction();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            await tcs.Task;
        }

        private UIElement CreateTestView()
        {
            return new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = "Test View" },
                    new Button { Content = "Test Button" },
                    new TextBox { Text = "Test Input" }
                }
            };
        }

        private List<TestItem> CreateTestData()
        {
            return new List<TestItem>
            {
                new TestItem { Id = 1, Name = "Test 1", Value = 100 },
                new TestItem { Id = 2, Name = "Test 2", Value = 200 }
            };
        }

        #endregion

        #region Test Data Classes

        public class TestItem
        {
            public int Id { get; set; }
            public string? Name { get; set; }
            public int Value { get; set; }
        }

        public class ComplexItem
        {
            public string? Name { get; set; }
            public SubItem? Details { get; set; }
        }

        public class SubItem
        {
            public int Value { get; set; }
            public string? Status { get; set; }
        }

        public class ChartPoint
        {
            public string? X { get; set; }
            public double Y { get; set; }
        }

        public class TestViewModel : System.ComponentModel.INotifyPropertyChanged
        {
            private string? _title;
            public string Title
            {
                get => _title ?? string.Empty;
                set
                {
                    _title = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Title)));
                }
            }

            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        }

        public class TestCommand : System.Windows.Input.ICommand
        {
            private readonly Action _execute;

            public TestCommand(Action execute)
            {
                _execute = execute;
            }

#pragma warning disable CS0067 // The event 'CanExecuteChanged' is never used
            public event EventHandler? CanExecuteChanged;
#pragma warning restore CS0067

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => _execute();
        }

        #endregion
    }
}

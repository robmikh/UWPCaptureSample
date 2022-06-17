using Microsoft.Graphics.Canvas;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.Graphics.Capture;
using Windows.Graphics.Display;
using Windows.Security.Authorization.AppCapabilityAccess;
using Windows.UI.Composition;
using Windows.UI.Popups;
using Windows.UI.WindowManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace UWPCaptureSample
{
    public sealed partial class MainPage : Page
    {
        private Compositor _compositor;
        private SpriteVisual _root;
        private SpriteVisual _content;
        private CompositionSurfaceBrush _brush;

        private CanvasDevice _device;
        private SimpleCapture _capture;

        private bool _isProgrammaticPresent = false;
        private bool _isBorderlessPresent = false;
        private bool _isCusrorCapturePresent = false;

        public MainPage()
        {
            this.InitializeComponent();

            var ignored = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            InitializeComposition();
            UpdateFeaturePresence();
            if (_isProgrammaticPresent)
            {
                await RefreshProgrammaticComboBoxesAsync();
            }
            else
            {
                WindowsComboBox.IsEnabled = false;
                DisplaysComboBox.IsEnabled = false;
                RefreshButton.IsEnabled = false;
            }
            if (!_isBorderlessPresent)
            {
                await RequestBorderAccessAsync();
            }
            else
            {
                BorderCheckBox.IsEnabled = false;
            }
            if (!_isCusrorCapturePresent)
            {
                CursorCheckBox.IsEnabled = false;
            }
        }

        private void UpdateFeaturePresence()
        {
            _isProgrammaticPresent = ApiInformation.IsMethodPresent(typeof(GraphicsCaptureItem).FullName, nameof(GraphicsCaptureItem.TryCreateFromWindowId));
            _isBorderlessPresent = ApiInformation.IsPropertyPresent(typeof(GraphicsCaptureSession).FullName, nameof(GraphicsCaptureSession.IsBorderRequired));
            _isCusrorCapturePresent = ApiInformation.IsPropertyPresent(typeof(GraphicsCaptureSession).FullName, nameof(GraphicsCaptureSession.IsCursorCaptureEnabled));
        }

        private void InitializeComposition()
        {
            _compositor = Window.Current.Compositor;
            _root = _compositor.CreateSpriteVisual();
            _content = _compositor.CreateSpriteVisual();
            _brush = _compositor.CreateSurfaceBrush();

            _root.RelativeSizeAdjustment = Vector2.One;
            _content.AnchorPoint = new Vector2(0.5f, 0.5f);
            _content.RelativeOffsetAdjustment = new Vector3(0.5f, 0.5f, 0);
            _content.RelativeSizeAdjustment = Vector2.One;
            _content.Size = new Vector2(-80, -80);
            _content.Brush = _brush;
            _brush.HorizontalAlignmentRatio = 0.5f;
            _brush.VerticalAlignmentRatio = 0.5f;
            _brush.Stretch = CompositionStretch.Uniform;
            var shadow = _compositor.CreateDropShadow();
            shadow.Mask = _brush;
            _content.Shadow = shadow;
            _root.Children.InsertAtTop(_content);

            _device = new CanvasDevice();
            ElementCompositionPreview.SetElementChildVisual(VisualGrid, _root);
        }

        private async Task RequestBorderAccessAsync()
        {
            // We need to request access, but being denied is ok. It just means the
            // system won't honor our border preferences unless the user grants us access.
            var ignoredResult = await GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Borderless);
        }

        private async Task RefreshProgrammaticComboBoxesAsync()
        {
            var accessResult = await GraphicsCaptureAccess.RequestAccessAsync(GraphicsCaptureAccessKind.Programmatic);
            if (accessResult == AppCapabilityAccessStatus.Allowed)
            {
                WindowsComboBox.IsEnabled = true;
                DisplaysComboBox.IsEnabled = true;

                var windowList = new List<GraphicsCaptureItem>();
                var windows = WindowServices.FindAllTopLevelWindowIds();
                foreach (var window in windows)
                {
                    var item = GraphicsCaptureItem.TryCreateFromWindowId(window);
                    if (item != null)
                    {
                        windowList.Add(item);
                    }
                }
                WindowsComboBox.ItemsSource = windowList;

                var displayList = new List<GraphicsCaptureItem>();
                var displays = DisplayServices.FindAll();
                foreach (var display in displays)
                {
                    var item = GraphicsCaptureItem.TryCreateFromDisplayId(display);
                    if (item != null)
                    {
                        displayList.Add(item);
                    }
                }
                DisplaysComboBox.ItemsSource = displayList;
            }
            else
            {
                WindowsComboBox.IsEnabled = false;
                DisplaysComboBox.IsEnabled = false;
            }
        }

        private void StartCapture(GraphicsCaptureItem item)
        {
            _capture = new SimpleCapture(_device, item);
            BorderCheckBox.IsChecked = true;
            CursorCheckBox.IsChecked = true;

            var surface = _capture.CreateSurface(_compositor);
            _brush.Surface = surface;

            _capture.StartCapture();
        }

        private void StopCapture()
        {
            _capture?.Dispose();
            _brush.Surface = null;
        }

        private async void OpenPickerButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new GraphicsCapturePicker();
            var item = await picker.PickSingleItemAsync();
            if (item != null)
            {
                StartCapture(item);
            }
        }

        private void StopCaptureButton_Click(object sender, RoutedEventArgs e)
        {
            StopCapture();
        }

        private void ProgrammaticComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            if (comboBox.SelectedItem == null)
            {
                return;
            }

            var item = (GraphicsCaptureItem)comboBox.SelectedItem;
            StartCapture(item);
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshProgrammaticComboBoxesAsync();
        }

        private void BorderCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SetBorderRequired(true);
        }

        private void BorderCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SetBorderRequired(false);
        }

        private void SetBorderRequired(bool isRequired)
        {
            if (_isBorderlessPresent)
            {
                _capture?.SetBorderRequired(isRequired);
            }
        }

        private void CursorCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SetCursorCaptureEnabled(true);
        }

        private void CursorCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SetCursorCaptureEnabled(false);
        }

        private void SetCursorCaptureEnabled(bool isEnabled)
        {
            if (_isCusrorCapturePresent)
            {
                _capture?.SetIsCursorCaptureEnabled(isEnabled);
            }
        }
    }
}

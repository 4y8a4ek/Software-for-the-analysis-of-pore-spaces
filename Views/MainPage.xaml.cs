using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using DigitalCoreAnalyser.ViewModels;

namespace DigitalCoreAnalyser.Views
{
    public sealed partial class MainPage : Page
    {
        private VoxelRenderer _renderer = new();
        private Point _last;

        public MainViewModel ViewModel => (MainViewModel)DataContext;

        public MainPage()
        {
            this.InitializeComponent();

            ViewModel.SetInputFields(NewSubSampleName, NewSubSampleX, NewSubSampleY, NewSubSampleZ, NewSubSampleSize);

            // Инициализация рендерера
            _renderer.Initialize(ThreeDImage);

            // Подписываемся на событие обновления 3D
            if (ViewModel != null)
            {
                ViewModel.RequestRender3D += () =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        Render3D();
                    });
                };
            }

            // Добавляем обработчики для ThreeDImage
            ThreeDImage.PointerPressed += OnDown;
            ThreeDImage.PointerMoved += OnMove;

            this.Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            if (ViewModel?.Is3DView == true)
                Render3D();
        }

        private void OnDown(object sender, PointerRoutedEventArgs e)
        {
            var image = sender as Image;
            if (image != null)
            {
                _last = e.GetCurrentPoint(image).Position;
            }
        }

        private void OnMove(object sender, PointerRoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var image = sender as Image;
            if (image != null && e.GetCurrentPoint(image).Properties.IsLeftButtonPressed)
            {
                var p = e.GetCurrentPoint(image).Position;

                ViewModel.RotationY += (p.X - _last.X) * 0.5;
                ViewModel.RotationX += (p.Y - _last.Y) * 0.5;

                _last = p;

                Render3D();
            }
        }

        private void Render3D()
        {
            if (ViewModel?.CurrentSample == null)
                return;

            _renderer.Render(ThreeDImage, ViewModel.CurrentSample, ViewModel.RotationX, ViewModel.RotationY);
        }

        private void Slider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (ViewModel != null)
            {
                ViewModel.SliceIndex = (int)e.NewValue;
                ViewModel.UpdateSliceCommand.Execute(null);
            }
        }
    }
}
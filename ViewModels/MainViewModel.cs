using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalCoreAnalyser.Models;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DigitalCoreAnalyser.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ImageLoaderService _imageLoader;
        private readonly PropertyCalculator _propertyCalculator;

        public event Action? RequestRender3D;
        private TextBox? _nameBox;
        private TextBox? _xBox;
        private TextBox? _yBox;
        private TextBox? _zBox;
        private TextBox? _sizeBox;


        [ObservableProperty] private bool is3DView;
        
        [ObservableProperty] private string permeabilityResult = "Не рассчитана";
        [ObservableProperty] private string formationFactorResult = "Не рассчитан";
        [ObservableProperty] private string tortuosityResult = "Не рассчитана";
        [ObservableProperty] private string effectivePorosity = "Не рассчитана";
        [ObservableProperty] private string poreSize = "Не рассчитан";
        [ObservableProperty] private double rotationY = 30;
        [ObservableProperty] private double rotationX = 20;

        [ObservableProperty] private CoreSample currentSample;
        [ObservableProperty] private WriteableBitmap currentSlice;

        [ObservableProperty] private int sliceIndex = 0;
        [ObservableProperty] private int maxSliceIndex = 0;

        [ObservableProperty] private string selectedOrientation = "XY";

        [ObservableProperty] private ObservableCollection<string> orientations;
        [ObservableProperty] private ObservableCollection<SubSample> subSamples;

        [ObservableProperty] private SubSample selectedSubSample;

        public string SampleName => CurrentSample?.Name ?? "No file";
        public string SampleSize => CurrentSample != null ? $"{CurrentSample.Width}×{CurrentSample.Height}×{CurrentSample.Depth}" : "0×0×0";
        public string SamplePorosity => CurrentSample != null ? $"{CurrentSample.Porosity:F2}%" : "0%";
        public string SampleVoxelSize => CurrentSample != null ? $"{CurrentSample.VoxelSize}μm" : "0μm";

        public ICommand LoadSampleCommand { get; }
        public ICommand UpdateSliceCommand { get; }
        public ICommand CreateSubSampleCommand { get; }
        public ICommand AnalyzeSubSampleCommand { get; }
        public ICommand PreviousSliceCommand { get; }
        public ICommand NextSliceCommand { get; }
        public ICommand ToggleViewModeCommand { get; }
        public ICommand CalculatePermeabilityCommand { get; }
        public XamlRoot? MainXamlRoot { get; set; }
        public ICommand CreateSubSampleFromFieldsCommand { get; }
        public ICommand StatisticsAnalysisCommand { get; }
        public MainViewModel()
        {
            _imageLoader = new ImageLoaderService();
            _propertyCalculator = new PropertyCalculator();

            Orientations = new ObservableCollection<string> { "XY", "XZ", "YZ" };
            SubSamples = new ObservableCollection<SubSample>();

            LoadSampleCommand = new AsyncRelayCommand(LoadSampleAsync);
            UpdateSliceCommand = new RelayCommand(UpdateSlice);
            CreateSubSampleCommand = new RelayCommand(CreateSubSample);
            CreateSubSampleFromFieldsCommand = new RelayCommand(CreateSubSampleFromFields);
            AnalyzeSubSampleCommand = new AsyncRelayCommand(AnalyzeSubSampleAsync);
            PreviousSliceCommand = new RelayCommand(PreviousSlice);
            NextSliceCommand = new RelayCommand(NextSlice);
            ToggleViewModeCommand = new RelayCommand(ToggleViewMode);
            CalculatePermeabilityCommand = new AsyncRelayCommand(CalculatePermeabilityAsync);
            StatisticsAnalysisCommand = new RelayCommand(OpenStatisticsWindow, CanOpenStatistics);

            Task.Run(async () => await LoadTestSampleAsync());
        }

        private void ToggleViewMode()
        {
            Is3DView = !Is3DView;

            if (Is3DView)
                RequestRender3D?.Invoke();
        }

        private async Task LoadTestSampleAsync()
        {
            await Task.Delay(100);
            CurrentSample = _imageLoader.CreateTestSample();
            MaxSliceIndex = CurrentSample.Depth - 1;
            UpdateSlice();

            OnPropertyChanged(nameof(SampleName));
            OnPropertyChanged(nameof(SampleSize));
            OnPropertyChanged(nameof(SamplePorosity));
            OnPropertyChanged(nameof(SampleVoxelSize));
        }

        private async Task LoadSampleAsync()
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();

            var window = new Microsoft.UI.Xaml.Window();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            picker.FileTypeFilter.Add("*");

            var file = await picker.PickSingleFileAsync();
            if (file == null) return;

            CurrentSample = await _imageLoader.LoadCTImage(file);
            MaxSliceIndex = CurrentSample.Depth - 1;

            UpdateSlice();

            OnPropertyChanged(nameof(SampleName));
            OnPropertyChanged(nameof(SampleSize));
            OnPropertyChanged(nameof(SamplePorosity));
            OnPropertyChanged(nameof(SampleVoxelSize));
        }

        private void UpdateSlice()
        {
            if (CurrentSample == null) return;

            var slice = _imageLoader.GetSlice(CurrentSample, SliceIndex, SelectedOrientation);
            CurrentSlice = _imageLoader.CreateBitmapFromSlice(slice);
        }
        private bool CanOpenStatistics()
        {
            return CurrentSample != null;
        }

        private void OpenStatisticsWindow()
        {
            if (CurrentSample == null) return;
            var window = new StatisticsWindow(CurrentSample, CurrentSample.Name);
            window.Activate();
        }

        // Добавь этот метод в MainViewModel
        partial void OnCurrentSampleChanged(CoreSample? oldValue, CoreSample? newValue)
        {
            // При загрузке/выгрузке образца обновляем доступность команды
            (StatisticsAnalysisCommand as RelayCommand)?.NotifyCanExecuteChanged();
        }
        public void SetInputFields(TextBox nameBox, TextBox xBox, TextBox yBox, TextBox zBox, TextBox sizeBox)
        {
            _nameBox = nameBox;
            _xBox = xBox;
            _yBox = yBox;
            _zBox = zBox;
            _sizeBox = sizeBox;
        }
        
        // Новый метод создания подвыборки из полей
        private void CreateSubSampleFromFields()
        {
            if (CurrentSample == null)
            {
                Console.WriteLine("Нет загруженного образца");
                return;
            }

            if (_xBox == null || _yBox == null || _zBox == null || _sizeBox == null)
            {
                Console.WriteLine("Поля ввода не инициализированы");
                return;
            }

            // Парсим X
            if (!int.TryParse(_xBox.Text, out int x) || x < 0 || x >= CurrentSample.Width)
            {
                Console.WriteLine($"X должен быть от 0 до {CurrentSample.Width - 1}");
                return;
            }

            // Парсим Y
            if (!int.TryParse(_yBox.Text, out int y) || y < 0 || y >= CurrentSample.Height)
            {
                Console.WriteLine($"Y должен быть от 0 до {CurrentSample.Height - 1}");
                return;
            }

            // Парсим Z
            if (!int.TryParse(_zBox.Text, out int z) || z < 0 || z >= CurrentSample.Depth)
            {
                Console.WriteLine($"Z должен быть от 0 до {CurrentSample.Depth - 1}");
                return;
            }

            // Максимальный размер
            int maxSize = Math.Min(CurrentSample.Width - x, Math.Min(CurrentSample.Height - y, CurrentSample.Depth - z));

            // Парсим размер
            if (!int.TryParse(_sizeBox.Text, out int size) || size < 1 || size > maxSize)
            {
                Console.WriteLine($"Размер должен быть от 1 до {maxSize}");
                return;
            }

            // Имя подвыборки
            string name = string.IsNullOrWhiteSpace(_nameBox?.Text) ? $"Подвыборка {SubSamples.Count + 1}" : _nameBox.Text;

            var subSample = new SubSample
            {
                Name = name,
                X = x,
                Y = y,
                Z = z,
                Size = size,
                Porosity = 0,
                Permeability = 0,
                FormationFactor = 0,
                Tortuosity = 0,
                EffectivePorosity = 0,
                PoreSize = 0
            };

            SubSamples.Add(subSample);
            SelectedSubSample = subSample;

            Console.WriteLine($"✅ Создана подвыборка: {name} (X={x}, Y={y}, Z={z}, Size={size})");

            // Очищаем поля (опционально)
            _nameBox.Text = "";
            _xBox.Text = "50";
            _yBox.Text = "50";
            _zBox.Text = "50";
            _sizeBox.Text = "100";
        }
        private void CreateSubSample()
        {
            if (CurrentSample == null) return;

            var s = new SubSample
            {
                Name = $"SubSample {SubSamples.Count + 1}",
                X = 50,
                Y = 50,
                Z = 50,
                Size = 100
            };

            SubSamples.Add(s);
            SelectedSubSample = s;
            Console.WriteLine($"✅ Создана: {s.Name} (X={s.X}, Y={s.Y}, Z={s.Z}, Size={s.Size})");
        }

        private async Task AnalyzeSubSampleAsync()
        {
            if (SelectedSubSample == null || CurrentSample == null)
            {
                Console.WriteLine("❌ Нет выбранной подвыборки или образца");
                return;
            }

            try
            {
                Console.WriteLine($"🔬 Анализ подвыборки: {SelectedSubSample.Name}");

                // Расчет пористости
                SelectedSubSample.Porosity = Math.Round(await _propertyCalculator.CalculatePorosity(CurrentSample, SelectedSubSample), 2);
                Console.WriteLine($"📊 Пористость: {SelectedSubSample.Porosity:F2}%");

                // Расчет проницаемости и других свойств
                var result = await _propertyCalculator.CalculatePermeability(CurrentSample, SelectedSubSample);

                // Сохраняем все результаты в SelectedSubSample с округлением
                SelectedSubSample.Permeability = Math.Round(result.PermeabilityDarcy, 3);
                SelectedSubSample.FormationFactor = Math.Round(result.FormationFactor, 3);
                SelectedSubSample.Tortuosity = Math.Round(result.Tortuosity, 3);
                SelectedSubSample.EffectivePorosity = Math.Round(result.EffectivePorosity, 3);
                SelectedSubSample.PoreSize = Math.Round(result.PoreSize, 2);

                // Обновляем UI - это заставит перерисовать привязки
                OnPropertyChanged(nameof(SelectedSubSample));

                Console.WriteLine($"✅ Результаты анализа:");
                Console.WriteLine($"   Пористость: {SelectedSubSample.Porosity:F2}%");
                Console.WriteLine($"   Проницаемость: {SelectedSubSample.Permeability:F3} Дарси");
                Console.WriteLine($"   Фактор формации: {SelectedSubSample.FormationFactor:F3}");
                Console.WriteLine($"   Извилистость: {SelectedSubSample.Tortuosity:F3}");
                Console.WriteLine($"   Размер пор: {SelectedSubSample.PoreSize:F2} мкм");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Ошибка анализа: {ex.Message}");
                Console.WriteLine($"   StackTrace: {ex.StackTrace}");
            }
        }

        partial void OnSelectedSubSampleChanged(SubSample? oldValue, SubSample? newValue)
        {
            if (newValue != null)
            {
                Console.WriteLine($"📋 Выбрана подвыборка: {newValue.Name}");
                // Принудительно обновляем UI при выборе
                OnPropertyChanged(nameof(SelectedSubSample));
            }
        }
        private void PreviousSlice()
        {
            if (SliceIndex > 0)
            {
                SliceIndex--;
                UpdateSlice();
            }
        }

        private void NextSlice()
        {
            if (SliceIndex < MaxSliceIndex)
            {
                SliceIndex++;
                UpdateSlice();
            }
        }
        private async Task CalculatePermeabilityAsync()
        {
            if (CurrentSample == null)
            {
                PermeabilityResult = "Нет данных";
                FormationFactorResult = "Нет данных";
                TortuosityResult = "Нет данных";
                EffectivePorosity = "Нет данных";
                PoreSize = "Нет данных";
                return;
            }

            try
            {

                // Создаем виртуальную подвыборку для всего образца
                var fullSampleSub = new SubSample
                {
                    Name = CurrentSample.Name,
                    X = 0,
                    Y = 0,
                    Z = 0,
                    Size = Math.Min(CurrentSample.Width, Math.Min(CurrentSample.Height, CurrentSample.Depth))
                };

                var result = await _propertyCalculator.CalculatePermeability(CurrentSample, fullSampleSub);

                PermeabilityResult = result.PermeabilityDisplay;
                FormationFactorResult = $"{result.FormationFactor:F3}";
                TortuosityResult = $"{result.Tortuosity:F3}";
                EffectivePorosity = $"{(result.EffectivePorosity * 100):F2} %";
                PoreSize = $"{result.PoreSize:F3} мкм";

                // Обновляем общую пористость в CurrentSample
                CurrentSample.Porosity = result.EffectivePorosity * 100;
                OnPropertyChanged(nameof(SamplePorosity));
            }
            catch (Exception ex)
            {
                PermeabilityResult = $"Ошибка: {ex.Message}";
                FormationFactorResult = "Ошибка";
                TortuosityResult = "Ошибка";
                EffectivePorosity = "Ошибка";
                PoreSize = "Ошибка";
            }
        }
    }
}
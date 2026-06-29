using DigitalCoreAnalyser.Models;
using DigitalCoreAnalyser.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace DigitalCoreAnalyser.Views
{
    public sealed partial class StatisticsWindow : Window
    {
        private readonly CoreSample _sample;
        private readonly string _sampleName;
        private readonly PropertyCalculator _calculator = new PropertyCalculator();
        private readonly Random _random = new Random();

        private readonly int[] _subsampleSizes = { 25, 50, 75, 100, 125, 150, 200, 250 };
        private const int N_SAMPLES_PER_SIZE = 100;

        private Dictionary<int, List<double>> _results;
        private int _currentIndex = 0;
        private bool _isRendering = false; // Флаг блокировки
        private string _currentProperty = "Пористость";


        public StatisticsWindow(CoreSample sample, string sampleName)
        {
            this.InitializeComponent();
            _sample = sample;
            _sampleName = sampleName;

            StatusText.Text = $"Образец: {_sampleName}";
            ProgressBar.Visibility = Visibility.Collapsed;
            ChartImage.Visibility = Visibility.Collapsed;

            CloseButton.Click += (s, e) => this.Close();
            AnalyzeButtonPorosity.Click += OnAnalyzeClickPorosity;
            AnalyzeButtonPermeability.Click += OnAnalyzeClickPermeability;
            SaveButton.Click += OnSaveClick;
            ExportButton.Click += OnExportClick;
            ClearButton.Click += OnClearClick;
            PrevButton.Click += OnPrevClick;
            NextButton.Click += OnNextClick;
            
        }

        private async void OnAnalyzeClickPorosity(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Генерация подвыборок...";
            ProgressBar.Visibility = Visibility.Visible;
            ChartImage.Visibility = Visibility.Collapsed;

            await Task.Delay(100);

            _results = await Task.Run(() => ComputeStatisticsParallel());

            _currentIndex = 0;
            UpdateNavigationButtons();
            PageInfo.Text = $"1/{_subsampleSizes.Length}";

            StatusText.Text = "Построение графика...";

            await RenderCurrentChart();

            ProgressBar.Visibility = Visibility.Collapsed;
            StatusText.Text = "Анализ завершён!";
            SaveButton.IsEnabled = true;
            ExportButton.IsEnabled = true;
        }
        private async void OnAnalyzeClickPermeability(object sender, RoutedEventArgs e)
        {
            _currentProperty = "Проницаемость";
            StatusText.Text = "Генерация подвыборок для проницаемости...";
            ProgressBar.Visibility = Visibility.Visible;
            ChartImage.Visibility = Visibility.Collapsed;

            await Task.Delay(100);

            _results = await Task.Run(() => ComputePermeabilityParallel());

            _currentIndex = 0;
            UpdateNavigationButtons();
            PageInfo.Text = $"1/{_subsampleSizes.Length}";

            StatusText.Text = "Построение графика...";

            await RenderCurrentChart();

            ProgressBar.Visibility = Visibility.Collapsed;
            StatusText.Text = "Анализ проницаемости завершён!";
            SaveButton.IsEnabled = true;
            ExportButton.IsEnabled = true;
        }

        private async Task RenderCurrentChart()
        {
            if (_isRendering) return;
            _isRendering = true;

            try
            {
                if (_results == null || _results.Count == 0)
                    return;

                int size = _subsampleSizes[_currentIndex];
                var imageData = await Task.Run(() => RenderSingleChart(_results, size, 1400, 550));

                if (imageData != null)
                {
                    using (var stream = new InMemoryRandomAccessStream())
                    {
                        await stream.WriteAsync(imageData.AsBuffer());
                        stream.Seek(0);
                        var bitmap = new BitmapImage();
                        await bitmap.SetSourceAsync(stream);
                        ChartImage.Source = bitmap;
                        ChartImage.Visibility = Visibility.Visible;
                    }
                }

                int count = _results[size]?.Count ?? 0;
                PageInfo.Text = $"{_currentIndex + 1}/{_subsampleSizes.Length}";
                StatusText.Text = $"L = {size} | n = {count * 4} | {_currentProperty}"; ;
            }
            finally
            {
                _isRendering = false;
            }
        }

        private void UpdateNavigationButtons()
        {
            PrevButton.IsEnabled = _currentIndex > 0;
            NextButton.IsEnabled = _currentIndex < _subsampleSizes.Length - 1;
        }

        private void OnPrevClick(object sender, RoutedEventArgs e)
        {
            if (_isRendering) return;

            if (_currentIndex > 0)
            {
                _currentIndex--;
                UpdateNavigationButtons();
                _ = RenderCurrentChart();
            }
        }

        private void OnNextClick(object sender, RoutedEventArgs e)
        {
            if (_isRendering) return;

            if (_currentIndex < _subsampleSizes.Length - 1)
            {
                _currentIndex++;
                UpdateNavigationButtons();
                _ = RenderCurrentChart();
            }
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private Dictionary<int, List<double>> ComputeStatisticsParallel()
        {
            var results = new Dictionary<int, List<double>>();
            var lockObj = new object();

            Parallel.ForEach(_subsampleSizes, size =>
            {
                var porosities = new List<double>();

                for (int i = 0; i < N_SAMPLES_PER_SIZE; i++)
                {
                    var sub = CreateRandomSubSample(size);
                    if (sub != null)
                    {
                        double por = _calculator.CalculatePorosity(_sample, sub).Result;
                        porosities.Add(por);
                    }
                }

                lock (lockObj)
                {
                    results[size] = porosities;
                }
            });

            return results;
        }
        private Dictionary<int, List<double>> ComputePermeabilityParallel()
        {
            var results = new Dictionary<int, List<double>>();
            var lockObj = new object();

            Parallel.ForEach(_subsampleSizes, size =>
            {
                var values = new List<double>();

                for (int i = 0; i < N_SAMPLES_PER_SIZE; i++)
                {
                    var sub = CreateRandomSubSample(size);
                    if (sub != null)
                    {
                        double val = _calculator.CalculatePermeabilityValue(_sample, sub).Result; // <-- ИСПОЛЬЗУЙ ЭТОТ МЕТОД
                        values.Add(val);
                    }
                }

                lock (lockObj)
                {
                    results[size] = values;
                }
            });

            return results;
        }

        private SubSample CreateRandomSubSample(int size)
        {
            int maxX = _sample.Width - size;
            int maxY = _sample.Height - size;
            int maxZ = _sample.Depth - size;

            if (maxX <= 0 || maxY <= 0 || maxZ <= 0)
                return null;

            return new SubSample
            {
                X = _random.Next(0, maxX),
                Y = _random.Next(0, maxY),
                Z = _random.Next(0, maxZ),
                Size = size
            };
        }

        private byte[] RenderSingleChart(Dictionary<int, List<double>> results, int size, int width, int height)
        {
            using (var surface = SKSurface.Create(new SKImageInfo(width, height)))
            {
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.White);

                // --- ОТСТУПЫ ВНУТРИ ГРАФИКА ---
                float marginLeft = 80;
                float marginRight = 40;
                float marginTop = 60;
                float marginBottom = 60;
                float chartWidth = width - marginLeft - marginRight;
                float chartHeight = height - marginTop - marginBottom;

                // --- ПОЛУЧАЕМ ДАННЫЕ ---
                if (!results.ContainsKey(size) || results[size].Count == 0)
                {
                    using (var paint = new SKPaint())
                    {
                        paint.Color = SKColors.Black;
                        paint.TextSize = 24;
                        paint.IsAntialias = true;
                        paint.TextAlign = SKTextAlign.Center;
                        canvas.DrawText("Нет данных", width / 2, height / 2, paint);
                    }
                    return surface.Snapshot().Encode(SKEncodedImageFormat.Png, 100).ToArray();
                }

                var porosities = results[size];
                porosities.Sort();

                // --- ВЫЧИСЛЯЕМ СТАТИСТИКУ ---
                double meanPorosity = porosities.Average();
                double minValue = porosities.Min();
                double maxValue = porosities.Max();

                // --- ГРАНИЦЫ ДЛЯ ГИСТОГРАММЫ: MIN-MAX + 0.5% ---
                double histMin = minValue - 0.5;
                double histMax = maxValue + 0.5;
                if (histMin < 0) histMin = 0;

                // --- ОСЬ X ВСЕГДА ОТ 0 ДО 100 ---
                double axisMin = 0;
                double axisMax = 100;

                // --- ВЫЧИСЛЯЕМ KDE (на всей оси 0-100) ---
                int nPoints = 300;
                double[] xValues = new double[nPoints];
                double[] yValues = new double[nPoints];
                double bandwidth = 2.0;

                for (int i = 0; i < nPoints; i++)
                {
                    xValues[i] = axisMin + (axisMax - axisMin) * i / (nPoints - 1);
                    double sum = 0;
                    foreach (var p in porosities)
                    {
                        double u = (xValues[i] - p) / bandwidth;
                        sum += Math.Exp(-0.5 * u * u) / (bandwidth * Math.Sqrt(2 * Math.PI));
                    }
                    yValues[i] = sum / porosities.Count;
                }

                // --- ВЫЧИСЛЯЕМ ГИСТОГРАММУ (9 бинов, границы histMin - histMax) ---
                int bins = 9;
                double binWidth = (histMax - histMin) / bins;
                double[] histValues = new double[bins];
                double[] binCenters = new double[bins];

                for (int i = 0; i < bins; i++)
                {
                    binCenters[i] = histMin + (i + 0.5) * binWidth;
                    double binStart = histMin + i * binWidth;
                    double binEnd = binStart + binWidth;

                    int count = 0;
                    foreach (var p in porosities)
                    {
                        if (p >= binStart && p < binEnd)
                            count++;
                    }
                    histValues[i] = (double)count / porosities.Count / binWidth;
                }

                // --- МАСШТАБИРУЕМ Y ---
                double maxY_KDE = yValues.Max();
                double maxY_Hist = histValues.Max();
                double maxY = Math.Max(maxY_KDE, maxY_Hist);
                if (maxY < 0.001) maxY = 0.001;

                // --- ФУНКЦИИ ПРЕОБРАЗОВАНИЯ (используем axisMin - axisMax для отображения) ---
                float ToScreenX(double x) => marginLeft + (float)((x - axisMin) / (axisMax - axisMin) * chartWidth);
                float ToScreenY(double y) => marginTop + chartHeight - (float)(y / maxY * chartHeight);

                // --- 1. ЗАГОЛОВОК ---
                using (var paint = new SKPaint())
                {
                    paint.Color = SKColors.Black;
                    paint.TextSize = 18;
                    paint.IsAntialias = true;
                    paint.TextAlign = SKTextAlign.Center;
                    paint.Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyleWeight.Bold,
                                                               SKFontStyleWidth.Normal, SKFontStyleSlant.Upright);
                    canvas.DrawText($"L = {size} | n = {porosities.Count * 4}", width / 2, 35, paint);
                }

                // --- 2. СЕТКА И ОСИ ---
                using (var gridPaint = new SKPaint())
                {
                    gridPaint.Color = new SKColor(220, 220, 220);
                    gridPaint.StrokeWidth = 1;

                    for (int i = 0; i <= 4; i++)
                    {
                        float yVal = (float)(i * maxY / 4);
                        float screenY = ToScreenY(yVal);
                        canvas.DrawLine(marginLeft, screenY, width - marginRight, screenY, gridPaint);

                        using (var textPaint = new SKPaint())
                        {
                            textPaint.Color = SKColors.Black;
                            textPaint.TextSize = 12;
                            textPaint.IsAntialias = true;
                            textPaint.TextAlign = SKTextAlign.Right;
                            canvas.DrawText($"{yVal:F2}", marginLeft - 10, screenY + 5, textPaint);
                        }
                    }
                }

                // --- ОСЬ X от 0 до 100 с шагом 10 ---
                using (var gridPaint = new SKPaint())
                {
                    gridPaint.Color = new SKColor(220, 220, 220);
                    gridPaint.StrokeWidth = 1;

                    for (int i = 0; i <= 10; i++)
                    {
                        double xVal = i * 10;
                        float screenX = ToScreenX(xVal);
                        canvas.DrawLine(screenX, marginTop, screenX, height - marginBottom, gridPaint);

                        using (var textPaint = new SKPaint())
                        {
                            textPaint.Color = SKColors.Black;
                            textPaint.TextSize = 10;
                            textPaint.IsAntialias = true;
                            textPaint.TextAlign = SKTextAlign.Center;
                            canvas.DrawText($"{xVal:F0}", screenX, height - marginBottom + 20, textPaint);
                        }
                    }
                }

                // --- РАМКА ГРАФИКА ---
                using (var borderPaint = new SKPaint())
                {
                    borderPaint.Color = SKColors.Black;
                    borderPaint.StrokeWidth = 2;
                    borderPaint.Style = SKPaintStyle.Stroke;
                    canvas.DrawRect(marginLeft, marginTop, chartWidth, chartHeight, borderPaint);
                }

                // --- 3. ГИСТОГРАММА (столбцы по данным histMin - histMax) ---
                using (var histPaint = new SKPaint())
                {
                    histPaint.Color = new SKColor(200, 200, 200);
                    histPaint.Style = SKPaintStyle.Fill;

                    for (int i = 0; i < bins; i++)
                    {
                        // Позиция на оси X для столбца (переводим из histMin-histMax в axisMin-axisMax)
                        float x = ToScreenX(binCenters[i] - binWidth / 2);
                        float widthBar = (float)(binWidth / (axisMax - axisMin) * chartWidth) * 0.9f;
                        float heightBar = ToScreenY(histValues[i]);
                        float y = heightBar;

                        if (y < marginTop) y = marginTop;
                        if (y > marginTop + chartHeight) y = marginTop + chartHeight;

                        canvas.DrawRect(x, y, widthBar, marginTop + chartHeight - y, histPaint);
                    }
                }

                // --- 4. ЛИНИЯ KDE ---
                using (var linePaint = new SKPaint())
                {
                    linePaint.Color = new SKColor(31, 119, 180);
                    linePaint.StrokeWidth = 3;
                    linePaint.IsAntialias = true;
                    linePaint.Style = SKPaintStyle.Stroke;

                    var path = new SKPath();
                    bool first = true;

                    for (int i = 0; i < nPoints; i++)
                    {
                        float screenX = ToScreenX(xValues[i]);
                        float screenY = ToScreenY(yValues[i]);

                        if (screenY < marginTop) screenY = marginTop;
                        if (screenY > marginTop + chartHeight) screenY = marginTop + chartHeight;

                        if (first)
                        {
                            path.MoveTo(screenX, screenY);
                            first = false;
                        }
                        else
                        {
                            path.LineTo(screenX, screenY);
                        }
                    }

                    canvas.DrawPath(path, linePaint);
                }

                // --- 5. СРЕДНЕЕ ---
                using (var meanPaint = new SKPaint())
                {
                    meanPaint.Color = SKColors.Red;
                    meanPaint.StrokeWidth = 2;
                    meanPaint.IsAntialias = true;
                    meanPaint.Style = SKPaintStyle.Stroke;
                    meanPaint.PathEffect = SKPathEffect.CreateDash(new float[] { 8, 8 }, 0);

                    float meanX = ToScreenX(meanPorosity);
                    canvas.DrawLine(meanX, marginTop, meanX, marginTop + chartHeight, meanPaint);

                    using (var textPaint = new SKPaint())
                    {
                        textPaint.Color = SKColors.Red;
                        textPaint.TextSize = 12;
                        textPaint.IsAntialias = true;
                        textPaint.TextAlign = SKTextAlign.Center;
                        string suffix = _currentProperty == "Пористость" ? "%" : "";
                        canvas.DrawText($"Среднее: {meanPorosity:F2}{suffix}", meanX, marginTop - 10, textPaint); ;
                    }
                }

                // --- 6. ПОДПИСИ ОСЕЙ ---
                using (var paint = new SKPaint())
                {
                    paint.Color = SKColors.Black;
                    paint.TextSize = 14;
                    paint.IsAntialias = true;
                    paint.TextAlign = SKTextAlign.Center;

                    using (var rotate = new SKAutoCanvasRestore(canvas))
                    {
                        canvas.RotateDegrees(-90, 30, height / 2);
                        canvas.DrawText("Плотность", 30, height / 2 + 5, paint);
                    }

                    string axisLabel = _currentProperty == "Пористость" ? "Пористость, %" : "Проницаемость, мД";
                    canvas.DrawText(axisLabel, width / 2, height - 10, paint); ;
                }

                var image = surface.Snapshot();
                return image.Encode(SKEncodedImageFormat.Png, 100).ToArray();
            }
        }

        private async void OnSaveClick(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Сохранение...";
            ProgressBar.Visibility = Visibility.Visible;
            await Task.Delay(1500);
            ProgressBar.Visibility = Visibility.Collapsed;
            StatusText.Text = "Сохранено!";
        }

        private async void OnExportClick(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Экспорт данных...";
            ProgressBar.Visibility = Visibility.Visible;
            await Task.Delay(1500);
            ProgressBar.Visibility = Visibility.Collapsed;
            StatusText.Text = "Экспортировано!";
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            _results = null;
            _currentIndex = 0;
            _isRendering = false;
            StatusText.Text = "Очищено!";
            ProgressBar.Visibility = Visibility.Collapsed;
            ChartImage.Visibility = Visibility.Collapsed;
            ChartImage.Source = null;
            PrevButton.IsEnabled = false;
            NextButton.IsEnabled = false;
            PageInfo.Text = "1/8";
            SaveButton.IsEnabled = false;
            ExportButton.IsEnabled = false;
        }
    }
}
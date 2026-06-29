using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Microsoft.UI.Dispatching;
using System.Runtime.InteropServices.WindowsRuntime;

namespace DigitalCoreAnalyser.Services
{
    public class VoxelRenderer
    {
        private Image _renderImage; // Контрол для отображения 3D
        private float _rotationX = 30f;
        private float _rotationY = 45f;

        /// <summary>
        /// Инициализация рендерера с Image контролом
        /// </summary>
        public void Initialize(Image imageControl)
        {
            _renderImage = imageControl;
        }

        /// <summary>
        /// Основной метод рендеринга - сохраняет изображение из Open3D
        /// </summary>
        public async void Render(Image image, CoreSample sample, double rotX, double rotY)
        {
            if (sample == null || image == null) return;

            _renderImage = image;
            _rotationX = (float)rotX;
            _rotationY = (float)rotY;

            // Получаем изображение из Open3D
            var imageBytes = await RenderWithOpen3D(sample);

            if (imageBytes != null)
            {
                // Отображаем полученное изображение
                var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
                if (dispatcherQueue != null)
                {
                    dispatcherQueue.TryEnqueue(async () =>
                    {
                        using (var stream = new InMemoryRandomAccessStream())
                        {
                            await stream.WriteAsync(imageBytes.AsBuffer());
                            stream.Seek(0);

                            var bitmap = new BitmapImage();
                            await bitmap.SetSourceAsync(stream);
                            _renderImage.Source = bitmap;
                        }
                    });
                }
            }
        }

        /// <summary>
        /// Визуализация через Open3D и возврат изображения
        /// </summary>
        private async Task<byte[]> RenderWithOpen3D(CoreSample sample)
        {
            if (sample == null) return null;

            string tempFile = System.IO.Path.GetTempFileName() + ".raw";
            string pythonScript = System.IO.Path.GetTempFileName() + ".py";
            string outputImage = System.IO.Path.GetTempFileName() + ".png";

            try
            {
                // Сохраняем воксельные данные
                using (var fs = new FileStream(tempFile, FileMode.Create))
                {
                    for (int z = 0; z < sample.Depth; z++)
                        for (int y = 0; y < sample.Height; y++)
                            for (int x = 0; x < sample.Width; x++)
                            {
                                fs.WriteByte(sample.VoxelData[z, y, x]);
                            }
                }

                // Создаём Python скрипт
                string script = $@"
import open3d as o3d
import numpy as np

# Загрузка данных
with open(r'{tempFile}', 'rb') as f:
    data = np.frombuffer(f.read(), dtype=np.uint8)

size = {sample.Width}
data = data.reshape(size, size, size)

# Координаты пор и матрицы
pores = np.argwhere(data == 0)
matrix = np.argwhere(data == 1)

# Семплирование (уменьшаем для скорости)
max_points = 64000000
if len(pores) > max_points:
    idx = np.random.choice(len(pores), max_points, replace=False)
    pores = pores[idx]

if len(matrix) > max_points:
    idx = np.random.choice(len(matrix), max_points, replace=False)
    matrix = matrix[idx]

# Нормализация
if len(pores) > 0:
    center = np.mean(pores, axis=0)
    pores = pores - center
if len(matrix) > 0:
    center = np.mean(matrix, axis=0)
    matrix = matrix - center

# Создаём облака точек
geometries = []

if len(pores) > 0:
    pcd_pores = o3d.geometry.PointCloud()
    pcd_pores.points = o3d.utility.Vector3dVector(pores.astype(np.float64))
    pcd_pores.paint_uniform_color([1, 0, 0])  # Красный
    geometries.append(pcd_pores)

if len(matrix) > 0:
    pcd_matrix = o3d.geometry.PointCloud()
    pcd_matrix.points = o3d.utility.Vector3dVector(matrix.astype(np.float64))
    pcd_matrix.paint_uniform_color([0.1, 0.1, 0.1])  # Почти чёрный
    geometries.append(pcd_matrix)

if len(geometries) == 0:
    exit(0)

# Настройка визуализатора для сохранения в файл
vis = o3d.visualization.Visualizer()
vis.create_window(visible=False, width=800, height=600)

for geom in geometries:
    vis.add_geometry(geom)

# Настройка камеры
view_control = vis.get_view_control()
view_control.set_lookat([0, 0, 0])
view_control.set_front([0, 0, -1])
view_control.set_up([0, 1, 0])
view_control.set_zoom(0.8)

# Сохраняем изображение
vis.poll_events()
vis.update_renderer()
vis.capture_screen_image(r'{outputImage}')
vis.destroy_window()
";

                await File.WriteAllTextAsync(pythonScript, script);

                // Запускаем Python процесс
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "python",
                        Arguments = $"\"{pythonScript}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                // Читаем полученное изображение
                if (File.Exists(outputImage))
                {
                    return File.ReadAllBytes(outputImage);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Open3D ошибка: {ex.Message}");
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
                if (File.Exists(pythonScript)) File.Delete(pythonScript);
                if (File.Exists(outputImage)) File.Delete(outputImage);
            }

            return null;
        }

        public void SetQuality(int step) { }
        public void SetPointSize(float size) { }
        public void Zoom(float delta) { }
        public void ResetCamera() { }
    }
}
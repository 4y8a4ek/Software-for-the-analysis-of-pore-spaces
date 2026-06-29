using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage;
using Microsoft.UI.Xaml.Media.Imaging;

namespace DigitalCoreAnalyser.Services
{
    public class ImageLoaderService
    {
        public async Task<CoreSample> LoadCTImage(StorageFile file)
        {
            var sample = new CoreSample { Name = file.Name };

            try
            {
                using (var stream = await file.OpenStreamForReadAsync())
                {
                    byte[] buffer = new byte[stream.Length];
                    await stream.ReadAsync(buffer, 0, buffer.Length);

                    // Определяем размер куба
                    int size = (int)Math.Round(Math.Pow(buffer.Length, 1.0 / 3.0));
                    Console.WriteLine($"📊 File size: {buffer.Length} bytes, cube size: {size}x{size}x{size}");

                    sample.Width = size;
                    sample.Height = size;
                    sample.Depth = size;
                    sample.VoxelSize = 5.0;
                    sample.VoxelData = new byte[size, size, size];

                    int index = 0;
                    for (int z = 0; z < size; z++)
                    {
                        for (int y = 0; y < size; y++)
                        {
                            for (int x = 0; x < size; x++)
                            {
                                if (index < buffer.Length)
                                    sample.VoxelData[x, y, z] = buffer[index++];
                            }
                        }
                    }

                    sample.Porosity = CalculatePorosity(sample);
                    Console.WriteLine($"📊 Porosity: {sample.Porosity:F2}%");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading file: {ex.Message}");
                sample = CreateTestSample();
            }

            return sample;
        }

        public CoreSample CreateTestSample()
        {
            var sample = new CoreSample
            {
                Name = "Test Sample",
                Width = 200,
                Height = 200,
                Depth = 200,
                VoxelSize = 5.0,
                VoxelData = new byte[200, 200, 200]
            };

            Random rand = new Random(42);

            // Создаём поры (белые)
            for (int i = 0; i < 50; i++)
            {
                int cx = rand.Next(50, 150);
                int cy = rand.Next(50, 150);
                int cz = rand.Next(50, 150);
                int radius = rand.Next(5, 15);

                for (int x = Math.Max(0, cx - radius); x < Math.Min(200, cx + radius); x++)
                {
                    for (int y = Math.Max(0, cy - radius); y < Math.Min(200, cy + radius); y++)
                    {
                        for (int z = Math.Max(0, cz - radius); z < Math.Min(200, cz + radius); z++)
                        {
                            int dx = x - cx;
                            int dy = y - cy;
                            int dz = z - cz;
                            if (dx * dx + dy * dy + dz * dz < radius * radius)
                            {
                                sample.VoxelData[x, y, z] = 0; // пора (будет белой)
                            }
                        }
                    }
                }
            }

            // Материал - 255 (черный)
            for (int x = 0; x < 200; x++)
                for (int y = 0; y < 200; y++)
                    for (int z = 0; z < 200; z++)
                        if (sample.VoxelData[x, y, z] != 0)
                            sample.VoxelData[x, y, z] = 255;

            sample.Porosity = CalculatePorosity(sample);
            return sample;
        }

        private double CalculatePorosity(CoreSample sample)
        {
            int poreCount = 0;
            int total = 0;

            for (int x = 0; x < sample.Width; x++)
                for (int y = 0; y < sample.Height; y++)
                    for (int z = 0; z < sample.Depth; z++)
                    {
                        total++;
                        if (sample.VoxelData[x, y, z] == 0) // 0 = пора
                            poreCount++;
                    }

            return (double)poreCount / total * 100.0;
        }

        public SliceData GetSlice(CoreSample sample, int index, string orientation)
        {
            var slice = new SliceData
            {
                SliceIndex = index,
                Orientation = orientation
            };

            switch (orientation)
            {
                case "XY":
                    slice.Width = sample.Width;
                    slice.Height = sample.Height;
                    slice.PixelData = new byte[slice.Width * slice.Height];

                    for (int y = 0; y < sample.Height; y++)
                        for (int x = 0; x < sample.Width; x++)
                        {
                            int idx = y * sample.Width + x;
                            slice.PixelData[idx] = sample.VoxelData[x, y, index];
                        }
                    break;

                case "XZ":
                    slice.Width = sample.Width;
                    slice.Height = sample.Depth;
                    slice.PixelData = new byte[slice.Width * slice.Height];

                    for (int z = 0; z < sample.Depth; z++)
                        for (int x = 0; x < sample.Width; x++)
                        {
                            int idx = z * sample.Width + x;
                            slice.PixelData[idx] = sample.VoxelData[x, index, z];
                        }
                    break;

                case "YZ":
                    slice.Width = sample.Height;
                    slice.Height = sample.Depth;
                    slice.PixelData = new byte[slice.Width * slice.Height];

                    for (int z = 0; z < sample.Depth; z++)
                        for (int y = 0; y < sample.Height; y++)
                        {
                            int idx = z * sample.Height + y;
                            slice.PixelData[idx] = sample.VoxelData[index, y, z];
                        }
                    break;
            }

            return slice;
        }

        public WriteableBitmap CreateBitmapFromSlice(SliceData slice)
        {
            if (slice == null || slice.PixelData == null || slice.PixelData.Length == 0)
            {
                Console.WriteLine("❌ CreateBitmapFromSlice: slice is null or empty");
                return null;
            }

            Console.WriteLine($"🎨 Creating bitmap: {slice.Width}x{slice.Height}, pixels: {slice.PixelData.Length}");

            // Статистика
            int min = 255, max = 0, sum = 0;
            for (int i = 0; i < slice.PixelData.Length; i++)
            {
                byte val = slice.PixelData[i];
                if (val < min) min = val;
                if (val > max) max = val;
                sum += val;
            }
            Console.WriteLine($"📊 Pixel stats - min: {min}, max: {max}, avg: {sum / slice.PixelData.Length}");

            var bitmap = new WriteableBitmap(slice.Width, slice.Height);

            using (var stream = bitmap.PixelBuffer.AsStream())
            {
                for (int i = 0; i < slice.PixelData.Length; i++)
                {
                    byte value = slice.PixelData[i];

                    // Конвертируем 0/1 в 0/255
                    byte normalizedValue = (byte)(value * 255);

                    // Инвертируем: 0 (пора) -> 255 (белый), 1 (материал) -> 0 (черный)
                    byte invertedValue = (byte)(255 - normalizedValue);

                    stream.WriteByte(invertedValue); // B
                    stream.WriteByte(invertedValue); // G
                    stream.WriteByte(invertedValue); // R
                    stream.WriteByte(255);           // A
                }
                stream.Flush();
            }

            Console.WriteLine($"✅ Bitmap created successfully");
            return bitmap;
        }
    }
}
using System.Collections.Concurrent;
using System.Threading.Tasks;
namespace DigitalCoreAnalyser.Services
{
    public class PropertyCalculator
    {
        public Task<double> CalculatePorosity(CoreSample sample, SubSample subSample)
        {
            return Task.Run(() =>
            {
                int poreCount = 0;
                int totalVoxels = 0;

                int startX = Math.Max(0, subSample.X);
                int startY = Math.Max(0, subSample.Y);
                int startZ = Math.Max(0, subSample.Z);

                int endX = Math.Min(sample.Width, subSample.X + subSample.Size);
                int endY = Math.Min(sample.Height, subSample.Y + subSample.Size);
                int endZ = Math.Min(sample.Depth, subSample.Z + subSample.Size);

                for (int z = startZ; z < endZ; z++)
                {
                    for (int y = startY; y < endY; y++)
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            totalVoxels++;
                            // Пороговое значение для пор (можно настраивать)
                            if (sample.VoxelData[x, y, z] < 1)
                                poreCount++;
                        }
                    }
                }

                return totalVoxels > 0 ? (double)poreCount / totalVoxels * 100.0 : 0;
            });
        }

        public double CalculatePorosityForREV(CoreSample sample, int startX, int startY, int startZ, int size)
        {
            int poreCount = 0;
            int totalVoxels = 0;

            int endX = Math.Min(sample.Width, startX + size);
            int endY = Math.Min(sample.Height, startY + size);
            int endZ = Math.Min(sample.Depth, startZ + size);

            for (int z = startZ; z < endZ; z++)
                for (int y = startY; y < endY; y++)
                    for (int x = startX; x < endX; x++)
                    {
                        totalVoxels++;
                        if (sample.VoxelData[x, y, z] < 1)
                            poreCount++;
                    }

            return totalVoxels > 0 ? (double)poreCount / totalVoxels * 100.0 : 0;
        }
        /// <summary>
        /// Расчет проницаемости по формуле Больцмана-Лапласа (LMP - Local Porosity Method)
        /// </summary>
        /// <summary>
        /// Расчет проницаемости по формуле Больцмана-Лапласа (LMP - Local Porosity Method)
        /// </summary>
        public Task<double> CalculatePermeabilityValue(CoreSample sample, SubSample subSample)
        {
            return Task.Run(async () =>
            {
                var result = await CalculatePermeability(sample, subSample);
                return result.PermeabilityDarcy;
            });
        }
        public Task<PermeabilityResult> CalculatePermeability(CoreSample sample, SubSample subSample)
        {
            return Task.Run(() =>
            {
                Console.WriteLine("Calculating permeability");
                var result = new PermeabilityResult
                {
                    Name = subSample.Name,
                    EffectivePorosity = CalculatePorosity(sample, subSample).Result / 100.0 // в долях
                };

                // Получаем подвыборку данных
                int startX = Math.Max(0, subSample.X);
                int startY = Math.Max(0, subSample.Y);
                int startZ = Math.Max(0, subSample.Z);
                int endX = Math.Min(sample.Width, subSample.X + subSample.Size);
                int endY = Math.Min(sample.Height, subSample.Y + subSample.Size);
                int endZ = Math.Min(sample.Depth, subSample.Z + subSample.Size);

                int sizeX = endX - startX;
                int sizeY = endY - startY;
                int sizeZ = endZ - startZ;

                if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
                    return result;

                // 1. Расчет средней пористости
                double totalPorosity = 0;
                int sliceCount = 0;

                for (int z = startZ; z < endZ; z++)
                {
                    int slicePores = 0;
                    int sliceTotal = 0;

                    for (int y = startY; y < endY; y++)
                    {
                        for (int x = startX; x < endX; x++)
                        {
                            sliceTotal++;
                            if (sample.VoxelData[x, y, z] < 1) // Пора
                            {
                                slicePores++;
                            }
                        }
                    }

                    double localPhi = (double)slicePores / sliceTotal;
                    totalPorosity += localPhi;
                    sliceCount++;
                }

                double avgPorosity = sliceCount > 0 ? totalPorosity / sliceCount : 0;

                Console.WriteLine($"Пористость: {avgPorosity * 100:F2}%");

                // 2. Расчет фактора формации по формуле Арчи (для песчаников)
                double cementationExponent = 1.8; // m - цементационный фактор (1.8-2.0)
                double tortuosityFactor = 0.62;   // a - фактор извилистости (0.62 для песчаников)

                result.FormationFactor = tortuosityFactor / Math.Pow(avgPorosity, cementationExponent);
                Console.WriteLine($"Фактор формации: {result.FormationFactor:F3}");

                // 3. Расчет извилистости через фактор формации
                // τ = √(F * φ)
                result.Tortuosity = Math.Sqrt(result.FormationFactor * avgPorosity);
                Console.WriteLine($"Извилистость: {result.Tortuosity:F3}");

                // 4. Расчет характерного размера пор (в метрах!)
                // Используем медианный размер, но с правильной конвертацией
                double characteristicPoreSizeMicrons = CalculateCharacteristicPoreSize(sample, startX, startY, startZ, sizeX, sizeY, sizeZ);
                double characteristicPoreSizeMeters = characteristicPoreSizeMicrons * 1e-6; // микрометры -> метры

                Console.WriteLine($"Характерный размер пор: {characteristicPoreSizeMicrons:F2} мкм = {characteristicPoreSizeMeters:E6} м");
                result.PoreSize = characteristicPoreSizeMicrons;
                // 5. Расчет проницаемости по формуле Козени-Кармана (более точная)
                // K = (φ * d²) / (72 * τ²) (для сферических частиц)
                double kM2 = (avgPorosity * Math.Pow(characteristicPoreSizeMeters, 2)) / (72 * Math.Pow(result.Tortuosity, 2));

                // Конвертация в Дарси (1 Дарси = 0.987e-12 м²)
                result.PermeabilityM2 = kM2;
                result.PermeabilityDarcy = kM2 / 0.987e-12;

                Console.WriteLine($"Проницаемость: {result.PermeabilityDarcy:E3} Дарси");

                return result;
            });
        }

        /// <summary>
        /// Расчет извилистости (tortuosity) на основе бинарной матрицы
        /// </summary>
        private double CalculateTortuosity(bool[,] poreMatrix)
        {
            int width = poreMatrix.GetLength(0);
            int height = poreMatrix.GetLength(1);

            // Простой метод: находим среднюю длину пути между порами
            double totalDistance = 0;
            int pathsCount = 0;

            // Поиск связных путей
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (poreMatrix[x, y])
                    {
                        var pathLength = FindShortestPathToEdge(poreMatrix, x, y, width, height);
                        if (pathLength > 0)
                        {
                            totalDistance += pathLength;
                            pathsCount++;
                        }
                    }
                }
            }

            double avgDistance = pathsCount > 0 ? totalDistance / pathsCount : 1.0;

            // Извилистость = средняя длина пути / прямое расстояние
            return Math.Max(1.0, avgDistance / Math.Sqrt(width * width + height * height) * 10);
        }

        /// <summary>
        /// Поиск кратчайшего пути от поры до границы
        /// </summary>
        private int FindShortestPathToEdge(bool[,] matrix, int startX, int startY, int width, int height)
        {
            var queue = new Queue<(int x, int y, int dist)>();
            var visited = new bool[width, height];

            queue.Enqueue((startX, startY, 0));
            visited[startX, startY] = true;

            while (queue.Count > 0)
            {
                var (x, y, dist) = queue.Dequeue();

                // Проверка достижения границы
                if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    return dist;

                // Проверка соседей (4-направления)
                var neighbors = new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) };
                foreach (var (nx, ny) in neighbors)
                {
                    if (nx >= 0 && nx < width && ny >= 0 && ny < height &&
                        !visited[nx, ny] && matrix[nx, ny])
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue((nx, ny, dist + 1));
                    }
                }
            }

            return -1; // Путь не найден
        }

        /// <summary>
        /// Расчет характерного размера пор (средний диаметр)
        /// </summary>
        /// <summary>
        /// Расчет характерного размера пор (средний диаметр) - МАКСИМАЛЬНО ОПТИМИЗИРОВАННАЯ ВЕРСИЯ
        /// </summary>
        /// <summary>
        /// Расчет характерного размера пор (гидравлический радиус)
        /// </summary>
        /// <summary>
        /// Расчет характерного размера пор (гидравлический радиус)
        /// </summary>
        private double CalculateCharacteristicPoreSize(CoreSample sample, int startX, int startY, int startZ, int sizeX, int sizeY, int sizeZ)
        {
            Console.WriteLine("🔵 Расчет гидравлического радиуса пор...");

            long totalPoreVolume = 0;
            long totalPoreSurface = 0;

            // Исправлено: Math.Min только с 2 аргументами, берем минимальный из трех
            int minSize = Math.Min(sizeX, Math.Min(sizeY, sizeZ));
            int step = Math.Max(1, minSize / 100);

            Console.WriteLine($"🔵 Шаг семплирования: {step}, минимальный размер: {minSize}");

            for (int x = 0; x < sizeX; x += step)
            {
                for (int y = 0; y < sizeY; y += step)
                {
                    for (int z = 0; z < sizeZ; z += step)
                    {
                        int realX = startX + x;
                        int realY = startY + y;
                        int realZ = startZ + z;

                        if (sample.VoxelData[realX, realY, realZ] < 1) // Пора
                        {
                            totalPoreVolume++;

                            // Считаем поверхность пор (границы с материалом)
                            // Проверяем 6 соседей
                            if (realX + 1 >= startX + sizeX || sample.VoxelData[realX + 1, realY, realZ] >= 1) totalPoreSurface++;
                            if (realX - 1 < startX || sample.VoxelData[realX - 1, realY, realZ] >= 1) totalPoreSurface++;
                            if (realY + 1 >= startY + sizeY || sample.VoxelData[realX, realY + 1, realZ] >= 1) totalPoreSurface++;
                            if (realY - 1 < startY || sample.VoxelData[realX, realY - 1, realZ] >= 1) totalPoreSurface++;
                            if (realZ + 1 >= startZ + sizeZ || sample.VoxelData[realX, realY, realZ + 1] >= 1) totalPoreSurface++;
                            if (realZ - 1 < startZ || sample.VoxelData[realX, realY, realZ - 1] >= 1) totalPoreSurface++;
                        }
                    }
                }

                // Прогресс
                if ((x + step) % Math.Max(1, sizeX / 10) == 0)
                {
                    Console.WriteLine($"🔵 Прогресс: {((x + step) * 100 / sizeX)}%");
                }
            }

            Console.WriteLine($"🔵 Объем пор (сэмплированных): {totalPoreVolume}");
            Console.WriteLine($"🔵 Поверхность пор: {totalPoreSurface}");

            if (totalPoreSurface == 0) return sample.VoxelSize;

            // Гидравлический радиус = объем / поверхность
            // Умножаем на размер вокселя для получения реального размера
            double hydraulicRadiusMicrons = ((double)totalPoreVolume / totalPoreSurface) * sample.VoxelSize;

            // Эквивалентный диаметр поры = 4 * гидравлический радиус
            double poreDiameterMicrons = hydraulicRadiusMicrons * 4;

            Console.WriteLine($"🔵 Гидравлический радиус: {hydraulicRadiusMicrons:F3} мкм");
            Console.WriteLine($"🔵 Эквивалентный диаметр поры: {poreDiameterMicrons:F3} мкм");

            return poreDiameterMicrons;
        }
    }
}
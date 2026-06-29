namespace DigitalCoreAnalyser.Models
{
    public class CoreSample
    {
        public string Name { get; set; } = string.Empty;
        public int Width { get; set; }
        public int Height { get; set; }
        public int Depth { get; set; }
        public byte[,,] VoxelData { get; set; } = new byte[0, 0, 0];
        public double VoxelSize { get; set; }
        public double Porosity { get; set; }
        public double Permeability { get; set; }
        public List<SubSample> SubSamples { get; set; } = new List<SubSample>();
    }

    public class SubSample
    {
        public string Name { get; set; } = string.Empty;
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int Size { get; set; }
        public double Porosity { get; set; }
        public double Permeability { get; set; }
        public double FormationFactor { get; set; }
        public double Tortuosity { get; set; }
        public double EffectivePorosity { get; set; }
        public double PoreSize { get; set; }
        public byte[,,] VoxelData { get; set; } = new byte[0, 0, 0];
    }

    public class SliceData
    {
        public byte[] PixelData { get; set; } = new byte[0];
        public int Width { get; set; }
        public int Height { get; set; }
        public int SliceIndex { get; set; }
        public string Orientation { get; set; } = string.Empty;
    }
}
namespace DigitalCoreAnalyser.Models
{
    public class PermeabilityResult
    {
        public string Name { get; set; } = string.Empty;
        public double PermeabilityDarcy { get; set; } // в Дарси
        public double PermeabilityM2 { get; set; }    // в м²
        public double FormationFactor { get; set; }   // Фактор формации
        public double Tortuosity { get; set; }        // Извилистость
        public double EffectivePorosity { get; set; } // Эффективная пористость
        public double PoreSize {get; set;} // Характерный размер пор
        public DateTime CalculationDate { get; set; } = DateTime.Now;

        // Для отображения в UI
        public string PermeabilityDisplay => $"{PermeabilityDarcy:F3} Дарси";
        public string PermeabilityM2Display => $"{PermeabilityM2:E6} м²";
    }
}
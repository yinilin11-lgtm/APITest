namespace APITest.Models
{
    public class ToyotaCar
    {
        public int Id { get; set; }

        public string Model { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public decimal StartingPriceWan { get; set; }

        public decimal? MaxPriceWan { get; set; }

        public int Seats { get; set; }

        public string FuelType { get; set; } = string.Empty;

        public bool HasHybridOption { get; set; }

        public bool IsSuv { get; set; }

        public bool IsElectric { get; set; }

        public int? EngineCc { get; set; }

        public string? HorsePower { get; set; }

        public decimal? FuelEconomyKmPerLiter { get; set; }

        public string BestFor { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string SourceUrl { get; set; } = string.Empty;

        public DateOnly SourceCheckedDate { get; set; }
    }
}

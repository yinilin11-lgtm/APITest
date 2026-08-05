using APITest.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APITest.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ToyotaCarsController(AppDbContext db) : ControllerBase
    {
        [HttpGet(Name = "GetToyotaCars")]
        public async Task<ActionResult<IEnumerable<ToyotaCarResponse>>> GetCars(
            [FromQuery] decimal? maxPriceWan = null,
            [FromQuery] string? category = null,
            [FromQuery] bool? hybrid = null)
        {
            var query = db.ToyotaCars.AsNoTracking();

            if (maxPriceWan is not null)
            {
                query = query.Where(car => car.StartingPriceWan <= maxPriceWan.Value);
            }

            if (!string.IsNullOrWhiteSpace(category))
            {
                query = query.Where(car => car.Category.Contains(category.Trim()));
            }

            if (hybrid is not null)
            {
                query = query.Where(car => car.HasHybridOption == hybrid.Value);
            }

            var cars = await query
                .OrderBy(car => car.StartingPriceWan)
                .Select(car => new ToyotaCarResponse
                {
                    Id = car.Id,
                    Model = car.Model,
                    Category = car.Category,
                    PriceRangeWan = $"{car.StartingPriceWan:0.0}-{car.MaxPriceWan:0.0}",
                    Seats = car.Seats,
                    FuelType = car.FuelType,
                    HasHybridOption = car.HasHybridOption,
                    BestFor = car.BestFor,
                    SourceUrl = car.SourceUrl,
                    SourceCheckedDate = car.SourceCheckedDate
                })
                .ToListAsync();

            return Ok(cars);
        }

        [HttpGet("compare", Name = "CompareToyotaCars")]
        public async Task<ActionResult<CompareToyotaCarsResponse>> CompareCars([FromQuery] string[] models)
        {
            var requestedModels = models
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Select(model => model.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToList();

            if (requestedModels.Count != 2)
            {
                return BadRequest(new ErrorResponse("Choose exactly two Toyota models to compare.", HttpContext.TraceIdentifier));
            }

            var cars = await db.ToyotaCars
                .AsNoTracking()
                .Where(car => requestedModels.Contains(car.Model))
                .ToListAsync();

            if (cars.Count != 2)
            {
                return NotFound(new ErrorResponse("One or both Toyota models were not found.", HttpContext.TraceIdentifier));
            }

            var first = cars.First(car => string.Equals(car.Model, requestedModels[0], StringComparison.OrdinalIgnoreCase));
            var second = cars.First(car => string.Equals(car.Model, requestedModels[1], StringComparison.OrdinalIgnoreCase));

            return Ok(new CompareToyotaCarsResponse
            {
                FirstModel = first.Model,
                SecondModel = second.Model,
                Rows =
                [
                    CompareRow("Price", $"{first.StartingPriceWan:0.0}-{first.MaxPriceWan:0.0} 萬", $"{second.StartingPriceWan:0.0}-{second.MaxPriceWan:0.0} 萬"),
                    CompareRow("Fuel / Powertrain", first.FuelType, second.FuelType),
                    CompareRow("Fuel Economy", FormatFuelEconomy(first.FuelEconomyKmPerLiter), FormatFuelEconomy(second.FuelEconomyKmPerLiter)),
                    CompareRow("Interior / Seats", $"{first.Seats} seats, {first.Category}", $"{second.Seats} seats, {second.Category}"),
                    CompareRow("Best Use", first.BestFor, second.BestFor),
                    CompareRow("Safety / Source", $"Official Toyota data checked {first.SourceCheckedDate:yyyy-MM-dd}", $"Official Toyota data checked {second.SourceCheckedDate:yyyy-MM-dd}")
                ]
            });
        }

        private static ComparisonRow CompareRow(string label, string first, string second)
        {
            return new ComparisonRow
            {
                Label = label,
                First = first,
                Second = second
            };
        }

        private static string FormatFuelEconomy(decimal? fuelEconomy)
        {
            return fuelEconomy is null ? "Not listed in database" : $"{fuelEconomy:0.0} km/L";
        }
    }

    public class ToyotaCarResponse
    {
        public int Id { get; set; }

        public string Model { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string PriceRangeWan { get; set; } = string.Empty;

        public int Seats { get; set; }

        public string FuelType { get; set; } = string.Empty;

        public bool HasHybridOption { get; set; }

        public string BestFor { get; set; } = string.Empty;

        public string SourceUrl { get; set; } = string.Empty;

        public DateOnly SourceCheckedDate { get; set; }
    }

    public class CompareToyotaCarsResponse
    {
        public string FirstModel { get; set; } = string.Empty;

        public string SecondModel { get; set; } = string.Empty;

        public List<ComparisonRow> Rows { get; set; } = [];
    }

    public class ComparisonRow
    {
        public string Label { get; set; } = string.Empty;

        public string First { get; set; } = string.Empty;

        public string Second { get; set; } = string.Empty;
    }
}

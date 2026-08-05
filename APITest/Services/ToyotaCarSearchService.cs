using System.Text;
using System.Text.RegularExpressions;
using APITest.Data;
using APITest.Models;
using Microsoft.EntityFrameworkCore;

namespace APITest.Services
{
    public class ToyotaCarSearchService(AppDbContext db)
    {
        private static readonly Regex BudgetRegex = new(@"(?<amount>\d+(?:\.\d+)?)\s*(wan|w|萬|萬元)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PassengerRegex = new(@"(?<count>\d+)\s*(people|persons|passengers|seats|人|個人|位)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public async Task<ToyotaRecommendationResult> SearchAsync(string message)
        {
            var criteria = ToyotaRecommendationCriteria.FromMessage(message);
            var allCars = await db.ToyotaCars.AsNoTracking().ToListAsync();

            var rankedCars = allCars
                .Select(car => new RankedToyotaCar(car, ScoreCar(car, criteria)))
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Car.StartingPriceWan)
                .Take(5)
                .Select(item => item.Car)
                .ToList();

            if (rankedCars.Count == 0)
            {
                rankedCars = allCars
                    .OrderBy(car => car.StartingPriceWan)
                    .Take(5)
                    .ToList();
            }

            return new ToyotaRecommendationResult(criteria, rankedCars);
        }

        public static string BuildPromptContext(ToyotaRecommendationResult result)
        {
            if (result.Cars.Count == 0)
            {
                return "No Toyota vehicle database records were found.";
            }

            var builder = new StringBuilder();
            builder.AppendLine("Structured customer needs detected by backend recommendation logic:");
            builder.AppendLine(result.Criteria.ToPromptText());
            builder.AppendLine();
            builder.AppendLine("Toyota vehicle database matches. Use only this data for exact vehicle facts:");

            foreach (var car in result.Cars)
            {
                builder.AppendLine($"- {car.Model}: {car.Category}, {car.Seats} seats, {car.FuelType}, price NT${car.StartingPriceWan:0.0}-{car.MaxPriceWan:0.0} wan, engine {FormatValue(car.EngineCc)} c.c., horsepower {FormatValue(car.HorsePower)}, fuel economy {FormatValue(car.FuelEconomyKmPerLiter)} km/L, best for {car.BestFor}. Source checked {car.SourceCheckedDate:yyyy-MM-dd}: {car.SourceUrl}");
            }

            return builder.ToString();
        }

        private static int ScoreCar(ToyotaCar car, ToyotaRecommendationCriteria criteria)
        {
            var score = 1;

            if (criteria.BudgetWan is not null)
            {
                score += car.StartingPriceWan <= criteria.BudgetWan.Value ? 8 : -20;
            }

            if (criteria.FamilySize is not null)
            {
                score += car.Seats >= criteria.FamilySize.Value ? 5 : -12;

                if (criteria.FamilySize.Value >= 5 && (car.Category.Contains("MPV") || car.IsSuv))
                {
                    score += 4;
                }
            }

            if (criteria.NeedsSuv is true)
            {
                score += car.IsSuv ? 7 : -8;
            }

            if (criteria.PrefersHybrid is true)
            {
                score += car.HasHybridOption ? 7 : -8;
            }

            if (criteria.NeedsEasyParking is true)
            {
                score += car.Category.Contains("Compact") || car.Model.Contains("VIOS") || car.Model.Contains("ALTIS") ? 5 : -4;
            }

            if (criteria.IsDailyCommute is true)
            {
                score += car.BestFor.Contains("commute", StringComparison.OrdinalIgnoreCase) ||
                    car.BestFor.Contains("city", StringComparison.OrdinalIgnoreCase) ||
                    car.HasHybridOption ? 4 : 0;
            }

            if (criteria.NeedsFamilyUse is true)
            {
                score += car.IsSuv || car.Category.Contains("MPV") || car.Seats >= 5 ? 4 : -5;
            }

            return score;
        }

        private static string FormatValue<T>(T? value)
        {
            return value is null ? "not listed" : value.ToString() ?? "not listed";
        }

        private record RankedToyotaCar(ToyotaCar Car, int Score);

        public record ToyotaRecommendationResult(ToyotaRecommendationCriteria Criteria, List<ToyotaCar> Cars);

        public class ToyotaRecommendationCriteria
        {
            public decimal? BudgetWan { get; init; }

            public int? FamilySize { get; init; }

            public bool? IsDailyCommute { get; init; }

            public bool? NeedsEasyParking { get; init; }

            public bool? PrefersHybrid { get; init; }

            public bool? NeedsSuv { get; init; }

            public bool? NeedsFamilyUse { get; init; }

            public static ToyotaRecommendationCriteria FromMessage(string message)
            {
                var lower = message.ToLowerInvariant();
                var familySize = TryExtractPassengerCount(message);

                return new ToyotaRecommendationCriteria
                {
                    BudgetWan = TryExtractBudgetWan(message),
                    FamilySize = familySize,
                    IsDailyCommute = ContainsAny(lower, "commute", "daily", "work", "office", "city driving", "通勤", "上班", "每天"),
                    NeedsEasyParking = ContainsAny(lower, "parking", "park", "easy to park", "compact", "city", "停車", "好停", "市區", "城市", "小台"),
                    PrefersHybrid = ContainsAny(lower, "hybrid", "fuel", "fuel-saving", "efficient", "economy", "phev", "油電", "省油", "油耗", "插電"),
                    NeedsSuv = ContainsAny(lower, "suv", "crossover", "休旅", "越野", "高一點"),
                    NeedsFamilyUse = familySize >= 3 || ContainsAny(lower, "family", "kids", "children", "child", "baby", "weekend trip", "travel", "家庭", "小孩", "孩子", "嬰兒", "旅行")
                };
            }

            public string ToPromptText()
            {
                return string.Join(Environment.NewLine, [
                    $"- Budget: {FormatCriteria(BudgetWan, value => $"NT${value:0.#} wan")}",
                    $"- Family size / passengers: {FormatCriteria(FamilySize, value => $"{value} people")}",
                    $"- Daily commute: {FormatCriteria(IsDailyCommute)}",
                    $"- Easy parking / city use: {FormatCriteria(NeedsEasyParking)}",
                    $"- Hybrid or fuel-saving preference: {FormatCriteria(PrefersHybrid)}",
                    $"- SUV preference: {FormatCriteria(NeedsSuv)}",
                    $"- Family use: {FormatCriteria(NeedsFamilyUse)}"
                ]);
            }

            private static decimal? TryExtractBudgetWan(string message)
            {
                var match = BudgetRegex.Match(message);
                return match.Success && decimal.TryParse(match.Groups["amount"].Value, out var amount)
                    ? amount
                    : null;
            }

            private static int? TryExtractPassengerCount(string message)
            {
                var match = PassengerRegex.Match(message);
                if (match.Success && int.TryParse(match.Groups["count"].Value, out var count))
                {
                    return count;
                }

                var lower = message.ToLowerInvariant();
                if (ContainsAny(lower, "two kids", "2 kids", "兩個小孩", "二個小孩"))
                {
                    return 4;
                }

                if (ContainsAny(lower, "one kid", "1 kid", "一個小孩"))
                {
                    return 3;
                }

                return null;
            }

            private static bool ContainsAny(string value, params string[] keywords)
            {
                return keywords.Any(value.Contains);
            }

            private static string FormatCriteria(bool? value)
            {
                return value is null ? "not specified" : value.Value ? "yes" : "no";
            }

            private static string FormatCriteria<T>(T? value, Func<T, string> formatter) where T : struct
            {
                return value is null ? "not specified" : formatter(value.Value);
            }
        }
    }
}

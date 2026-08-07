using System.Text;
using System.Text.RegularExpressions;
using ToyotaVehicleAdvisor.Data;
using ToyotaVehicleAdvisor.Models;
using Microsoft.EntityFrameworkCore;

namespace ToyotaVehicleAdvisor.Services
{
    public class ToyotaCarSearchService(AppDbContext db)
    {
        private static readonly Regex BudgetRegex = new(@"(?<amount>\d+(?:\.\d+)?)\s*(wan|w|萬|萬元)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PassengerRegex = new(@"(?<count>\d+)\s*(people|persons|passengers|seats|人|個人|位)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public async Task<ToyotaRecommendationResult> SearchAsync(string message)
        {
            var criteria = ToyotaRecommendationCriteria.FromMessage(message);
            var allCars = await db.ToyotaCars.AsNoTracking().ToListAsync();

            var queryCars = ApplyDirectQuery(allCars, criteria);
            if (queryCars is not null)
            {
                return new ToyotaRecommendationResult(criteria, queryCars);
            }

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

        private static List<ToyotaCar>? ApplyDirectQuery(List<ToyotaCar> allCars, ToyotaRecommendationCriteria criteria)
        {
            return criteria.QueryIntent switch
            {
                ToyotaQueryIntent.HighestPrice => allCars
                    .OrderByDescending(car => car.MaxPriceWan)
                    .ThenByDescending(car => car.StartingPriceWan)
                    .Take(5)
                    .ToList(),
                ToyotaQueryIntent.LowestPrice => allCars
                    .OrderBy(car => car.StartingPriceWan)
                    .ThenBy(car => car.MaxPriceWan)
                    .Take(5)
                    .ToList(),
                ToyotaQueryIntent.BudgetUnder when criteria.BudgetWan is not null => allCars
                    .Where(car => car.StartingPriceWan <= criteria.BudgetWan.Value)
                    .OrderByDescending(car => car.StartingPriceWan)
                    .ThenBy(car => car.Model)
                    .Take(8)
                    .ToList(),
                ToyotaQueryIntent.SevenSeats => allCars
                    .Where(car => car.Seats >= 7)
                    .OrderBy(car => car.StartingPriceWan)
                    .Take(8)
                    .ToList(),
                ToyotaQueryIntent.Hybrid => allCars
                    .Where(car => car.HasHybridOption)
                    .OrderBy(car => car.StartingPriceWan)
                    .Take(8)
                    .ToList(),
                ToyotaQueryIntent.Electric => allCars
                    .Where(car => car.IsElectric)
                    .OrderBy(car => car.StartingPriceWan)
                    .Take(8)
                    .ToList(),
                ToyotaQueryIntent.Suv => allCars
                    .Where(car => car.IsSuv)
                    .OrderBy(car => car.StartingPriceWan)
                    .Take(8)
                    .ToList(),
                ToyotaQueryIntent.SportsCar => allCars
                    .Where(IsPerformanceCar)
                    .OrderBy(car => car.StartingPriceWan)
                    .Take(8)
                    .ToList(),
                ToyotaQueryIntent.Commercial => allCars
                    .Where(car => car.Category.Contains("Commercial", StringComparison.OrdinalIgnoreCase) ||
                        car.Category.Contains("Pickup", StringComparison.OrdinalIgnoreCase) ||
                        car.Model.Contains("TOWN ACE", StringComparison.OrdinalIgnoreCase) ||
                        car.Model.Contains("HILUX", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(car => car.StartingPriceWan)
                    .Take(8)
                    .ToList(),
                ToyotaQueryIntent.PremiumComfort => allCars
                    .Where(car => car.Category.Contains("Luxury", StringComparison.OrdinalIgnoreCase) ||
                        car.Category.Contains("Crossover Sedan", StringComparison.OrdinalIgnoreCase) ||
                        car.Model.Contains("ALPHARD", StringComparison.OrdinalIgnoreCase) ||
                        car.Model.Contains("CROWN", StringComparison.OrdinalIgnoreCase) ||
                        car.Model.Contains("CAMRY", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(car => car.MaxPriceWan)
                    .Take(8)
                    .ToList(),
                _ => null
            };
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

            if (criteria.WantsSportsCar is true)
            {
                score += IsPerformanceCar(car) ||
                    car.BestFor.Contains("performance", StringComparison.OrdinalIgnoreCase) ||
                    car.BestFor.Contains("driving enjoyment", StringComparison.OrdinalIgnoreCase) ? 12 : -6;
            }

            if (criteria.NeedsCommercialUse is true)
            {
                score += car.Category.Contains("Commercial", StringComparison.OrdinalIgnoreCase) ||
                    car.Category.Contains("Pickup", StringComparison.OrdinalIgnoreCase) ||
                    car.Model.Contains("TOWN ACE", StringComparison.OrdinalIgnoreCase) ||
                    car.Model.Contains("HILUX", StringComparison.OrdinalIgnoreCase) ? 12 : -5;
            }

            if (criteria.WantsPremiumComfort is true)
            {
                score += car.Category.Contains("Luxury", StringComparison.OrdinalIgnoreCase) ||
                    car.Category.Contains("Crossover Sedan", StringComparison.OrdinalIgnoreCase) ||
                    car.Model.Contains("ALPHARD", StringComparison.OrdinalIgnoreCase) ||
                    car.Model.Contains("CROWN", StringComparison.OrdinalIgnoreCase) ||
                    car.Model.Contains("CAMRY", StringComparison.OrdinalIgnoreCase) ? 9 : -3;
            }

            if (criteria.NeedsOutdoorUse is true)
            {
                score += car.IsSuv ||
                    car.Category.Contains("Pickup", StringComparison.OrdinalIgnoreCase) ||
                    car.Model.Contains("RAV4", StringComparison.OrdinalIgnoreCase) ||
                    car.Model.Contains("LAND CRUISER", StringComparison.OrdinalIgnoreCase) ||
                    car.Model.Contains("HILUX", StringComparison.OrdinalIgnoreCase) ? 8 : -4;
            }

            return score;
        }

        private static bool IsPerformanceCar(ToyotaCar car)
        {
            return car.Category.Contains("Sports", StringComparison.OrdinalIgnoreCase) ||
                car.Model.Equals("GR86", StringComparison.OrdinalIgnoreCase) ||
                car.Model.Equals("GR YARIS", StringComparison.OrdinalIgnoreCase) ||
                car.Model.Equals("GR SUPRA", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatValue<T>(T? value)
        {
            return value is null ? "not listed" : value.ToString() ?? "not listed";
        }

        private record RankedToyotaCar(ToyotaCar Car, int Score);

        public record ToyotaRecommendationResult(ToyotaRecommendationCriteria Criteria, List<ToyotaCar> Cars);

        public enum ToyotaQueryIntent
        {
            Recommendation,
            HighestPrice,
            LowestPrice,
            BudgetUnder,
            SevenSeats,
            Hybrid,
            Electric,
            Suv,
            SportsCar,
            Commercial,
            PremiumComfort
        }

        public class ToyotaRecommendationCriteria
        {
            public ToyotaQueryIntent QueryIntent { get; init; }

            public decimal? BudgetWan { get; init; }

            public int? FamilySize { get; init; }

            public bool? IsDailyCommute { get; init; }

            public bool? NeedsEasyParking { get; init; }

            public bool? PrefersHybrid { get; init; }

            public bool? NeedsSuv { get; init; }

            public bool? NeedsFamilyUse { get; init; }

            public bool? WantsSportsCar { get; init; }

            public bool? NeedsCommercialUse { get; init; }

            public bool? WantsPremiumComfort { get; init; }

            public bool? NeedsOutdoorUse { get; init; }

            public bool? WantsHighestPrice { get; init; }

            public static ToyotaRecommendationCriteria FromMessage(string message)
            {
                var lower = message.ToLowerInvariant();
                var familySize = TryExtractPassengerCount(message);
                var budgetWan = TryExtractBudgetWan(message);

                return new ToyotaRecommendationCriteria
                {
                    QueryIntent = DetectQueryIntent(lower, budgetWan, familySize),
                    BudgetWan = budgetWan,
                    FamilySize = familySize,
                    IsDailyCommute = ContainsAny(lower, "commute", "daily", "work", "office", "city driving", "通勤", "上班", "上課", "每天", "代步", "日常", "買菜", "短程", "平常開"),
                    NeedsEasyParking = ContainsAny(lower, "parking", "park", "easy to park", "compact", "city", "停車", "好停", "市區", "城市", "小台", "小車", "新手", "第一台車", "巷子", "窄路"),
                    PrefersHybrid = ContainsAny(lower, "hybrid", "fuel", "fuel-saving", "efficient", "economy", "phev", "electric", "ev", "省油", "油電", "油耗", "節能", "插電", "電動", "純電", "充電"),
                    NeedsSuv = ContainsAny(lower, "suv", "crossover", "off-road", "4wd", "awd", "休旅", "休旅車", "越野", "四輪傳動", "底盤高", "空間大"),
                    NeedsFamilyUse = familySize >= 3 || ContainsAny(lower, "family", "kids", "children", "child", "baby", "weekend trip", "travel", "小孩", "孩子", "兒童", "寶寶", "家庭", "家人", "親子", "接小孩", "載小孩"),
                    WantsSportsCar = ContainsAny(lower, "sports car", "sporty", "performance", "coupe", "fun to drive", "gr86", "supra", "gr yaris", "gr ", "track", "跑車", "性能", "雙門", "熱血", "駕駛樂趣", "開快", "帥", "操控", "賽道", "甩尾"),
                    NeedsCommercialUse = ContainsAny(lower, "truck", "van", "cargo", "delivery", "business", "commercial", "貨車", "廂型車", "貨卡", "載貨", "送貨", "做生意", "公司用", "商用", "工具車"),
                    WantsPremiumComfort = ContainsAny(lower, "luxury", "premium", "executive", "chauffeur", "vip", "comfortable", "expensive", "most expensive", "highest price", "top price", "高級", "豪華", "商務", "老闆", "接送", "主管", "貴賓", "舒適", "貴", "最貴", "最高價", "價格最高", "預算高"),
                    NeedsOutdoorUse = ContainsAny(lower, "camping", "outdoor", "road trip", "long distance", "off-road", "露營", "戶外", "長途", "爬山", "旅行", "旅遊", "出遊", "越野"),
                    WantsHighestPrice = ContainsAny(lower, "most expensive", "highest price", "top price", "highest-priced", "pricey", "最貴", "最高價", "價格最高", "售價最高", "最豪華", "預算最高")
                };
            }

            public string ToPromptText()
            {
                return string.Join(Environment.NewLine, [
                    $"- Query type: {QueryIntent}",
                    $"- Budget: {FormatCriteria(BudgetWan, value => $"NT${value:0.#} wan")}",
                    $"- Family size / passengers: {FormatCriteria(FamilySize, value => $"{value} people")}",
                    $"- Daily commute: {FormatCriteria(IsDailyCommute)}",
                    $"- Easy parking / city use: {FormatCriteria(NeedsEasyParking)}",
                    $"- Hybrid or fuel-saving preference: {FormatCriteria(PrefersHybrid)}",
                    $"- SUV preference: {FormatCriteria(NeedsSuv)}",
                    $"- Family use: {FormatCriteria(NeedsFamilyUse)}",
                    $"- Sport / performance preference: {FormatCriteria(WantsSportsCar)}",
                    $"- Commercial / cargo use: {FormatCriteria(NeedsCommercialUse)}",
                    $"- Premium comfort preference: {FormatCriteria(WantsPremiumComfort)}",
                    $"- Outdoor / long-distance use: {FormatCriteria(NeedsOutdoorUse)}",
                    $"- Highest-price request: {FormatCriteria(WantsHighestPrice)}"
                ]);
            }

            private static decimal? TryExtractBudgetWan(string message)
            {
                var match = BudgetRegex.Match(message);
                return match.Success && decimal.TryParse(match.Groups["amount"].Value, out var amount)
                    ? amount
                    : null;
            }

            private static ToyotaQueryIntent DetectQueryIntent(string lower, decimal? budgetWan, int? familySize)
            {
                if (ContainsAny(lower, "most expensive", "highest price", "top price", "highest-priced", "pricey", "最貴", "最高價", "價格最高", "售價最高", "最豪華", "預算最高"))
                {
                    return ToyotaQueryIntent.HighestPrice;
                }

                if (ContainsAny(lower, "cheapest", "lowest price", "least expensive", "entry-level", "最便宜", "最低價", "價格最低", "入門", "平價排行"))
                {
                    return ToyotaQueryIntent.LowestPrice;
                }

                if (budgetWan is not null && ContainsAny(lower, "under", "below", "less than", "within", "afford", "buy", "which", "what", "list", "以內", "以下", "不超過", "低於", "能買", "可以買", "有哪些", "有什麼", "哪幾台"))
                {
                    return ToyotaQueryIntent.BudgetUnder;
                }

                if (familySize >= 7 || ContainsAny(lower, "7 seats", "seven seats", "7-seater", "seven-seater", "七人座", "7人座", "七個人", "七位", "七人"))
                {
                    return ToyotaQueryIntent.SevenSeats;
                }

                if (ContainsAny(lower, "electric", "ev", "battery", "電動", "純電") &&
                    ContainsAny(lower, "有哪些", "which", "what", "list", "有什麼", "哪幾台", "車款"))
                {
                    return ToyotaQueryIntent.Electric;
                }

                if (ContainsAny(lower, "hybrid", "phev", "fuel-saving", "油電", "插電", "省油") &&
                    ContainsAny(lower, "有哪些", "which", "what", "list", "有什麼", "哪幾台", "車款"))
                {
                    return ToyotaQueryIntent.Hybrid;
                }

                if (ContainsAny(lower, "suv", "crossover", "休旅", "休旅車") &&
                    ContainsAny(lower, "有哪些", "which", "what", "list", "有什麼", "哪幾台", "車款"))
                {
                    return ToyotaQueryIntent.Suv;
                }

                if (ContainsAny(lower, "sports car", "sporty", "performance", "coupe", "gr86", "supra", "gr yaris", "跑車", "性能", "雙門", "gr ") &&
                    ContainsAny(lower, "有哪些", "which", "what", "list", "有什麼", "哪幾台", "車款", "推薦"))
                {
                    return ToyotaQueryIntent.SportsCar;
                }

                if (ContainsAny(lower, "truck", "van", "cargo", "delivery", "business", "commercial", "貨車", "廂型車", "貨卡", "載貨", "送貨", "商用", "公司用"))
                {
                    return ToyotaQueryIntent.Commercial;
                }

                if (ContainsAny(lower, "luxury", "premium", "executive", "chauffeur", "vip", "豪華", "商務", "老闆", "接送", "貴賓") &&
                    ContainsAny(lower, "有哪些", "which", "what", "list", "有什麼", "哪幾台", "車款", "推薦"))
                {
                    return ToyotaQueryIntent.PremiumComfort;
                }

                return ToyotaQueryIntent.Recommendation;
            }

            private static int? TryExtractPassengerCount(string message)
            {
                var match = PassengerRegex.Match(message);
                if (match.Success && int.TryParse(match.Groups["count"].Value, out var count))
                {
                    return count;
                }

                var lower = message.ToLowerInvariant();
                if (ContainsAny(lower, "two kids", "2 kids"))
                {
                    return 4;
                }

                if (ContainsAny(lower, "one kid", "1 kid"))
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

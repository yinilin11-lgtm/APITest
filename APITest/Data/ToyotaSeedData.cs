using APITest.Models;
using Microsoft.EntityFrameworkCore;

namespace APITest.Data
{
    public static class ToyotaSeedData
    {
        private static readonly DateOnly SourceCheckedDate = new(2026, 8, 5);

        public static async Task InitializeAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cars = CreateCars();

            Directory.CreateDirectory("Data");
            await db.Database.EnsureCreatedAsync();

            var existingCount = await db.ToyotaCars.CountAsync();
            if (existingCount >= cars.Length)
            {
                return;
            }

            if (existingCount > 0)
            {
                db.ToyotaCars.RemoveRange(db.ToyotaCars);
                await db.SaveChangesAsync();
            }

            db.ToyotaCars.AddRange(cars);
            await db.SaveChangesAsync();
        }

        private static ToyotaCar[] CreateCars()
        {
            return
            [
                Car("TOWN ACE Truck", "Light Commercial Vehicle", 51.5m, 57.5m, 2, "Gasoline", false, false, false, "Small business delivery, cargo work, budget commercial use", "Compact commercial truck for business owners who need cargo capacity at a low entry price."),
                Car("TOWN ACE Van", "Light Commercial Vehicle", 53.9m, 63.5m, 2, "Gasoline", false, false, false, "Small business delivery, enclosed cargo, practical commercial use", "Compact commercial van for business users who need an enclosed loading area."),
                Car("VIOS", "Sedan", 60.9m, 67.5m, 5, "Gasoline", false, false, false, "Entry-level sedan, city commuting, value-focused buyers", "Affordable compact sedan for daily transportation and first-time car buyers."),
                Car("YARiS CROSS", "Compact SUV", 69.5m, 79.5m, 5, "Gasoline", false, true, false, "City driving, easy parking, first-time buyers, affordable SUV needs", "Compact gasoline SUV with strong city practicality."),
                Car("ALTIS", "Sedan", 73.5m, 89.5m, 5, "Gasoline / Hybrid", true, false, false, "Daily commute, practical family sedan, value-focused buyers", "Mainstream sedan with gasoline and hybrid choices for practical commuting."),
                Car("COROLLA CROSS", "SUV", 80.9m, 98.9m, 5, "Gasoline / Hybrid", true, true, false, "Small families, SUV preference, balanced budget and space", "Popular compact SUV with gasoline and hybrid choices for families and daily use."),
                Car("ALTIS GR SPORT", "Sedan", 91.5m, 91.9m, 5, "Gasoline / Hybrid", true, false, false, "Drivers who want Altis practicality with sportier styling", "Sport-styled Altis variant for buyers who want sedan practicality with a more dynamic look."),
                Car("COROLLA CROSS GR SPORT", "SUV", 91.5m, 103.5m, 5, "Gasoline / Hybrid", true, true, false, "Drivers who want Corolla Cross practicality with sportier styling", "Sport-styled Corolla Cross variant for buyers who still want an efficient compact SUV."),
                Car("COROLLA SPORT", "Hatchback", 96.9m, 96.9m, 5, "Gasoline", false, false, false, "Hatchback buyers, sporty daily driving, compact practicality", "Compact hatchback for customers who want a more dynamic body style than a sedan."),
                Car("RAV4", "SUV", 104.0m, 149.0m, 5, "Hybrid / Plug-in Hybrid", true, true, false, "Family trips, outdoor use, higher budget SUV needs, hybrid SUV buyers", "Larger SUV choice with hybrid and plug-in hybrid options."),
                Car("CAMRY", "Sedan", 110.9m, 125.0m, 5, "Gasoline / Hybrid", true, false, false, "Business use, comfort, highway driving, mature sedan buyers", "Mid-size sedan focused on comfort and a more premium driving experience."),
                Car("URBAN CRUISER", "Electric SUV", 127.0m, 127.0m, 5, "Electric", false, true, true, "Electric SUV buyers, city and family use, zero-tailpipe-emission driving", "Battery-electric SUV option for customers considering Toyota EV ownership."),
                Car("bZ4X", "Electric SUV", 128.0m, 128.0m, 5, "Electric", false, true, true, "Electric SUV buyers, technology-focused users, zero-tailpipe-emission driving", "Toyota battery-electric SUV for customers prioritizing EV driving."),
                Car("PRIUS PHEV", "Plug-in Hybrid", 129.9m, 137.9m, 5, "Plug-in Hybrid", true, false, false, "Fuel economy, technology-focused buyers, daily charging users", "Plug-in hybrid model for customers who prioritize efficiency and electrified driving."),
                Car("RAV4 ADVENTURE", "SUV", 133.0m, 133.0m, 5, "Hybrid", true, true, false, "Outdoor style, family trips, SUV buyers who want a tougher look", "Adventure-styled RAV4 hybrid variant for outdoor-oriented customers."),
                Car("RAV4 GR SPORT", "SUV", 139.0m, 139.0m, 5, "Hybrid", true, true, false, "SUV buyers who want RAV4 practicality with GR Sport styling", "GR Sport RAV4 hybrid variant for customers who want a sportier SUV presentation."),
                Car("CROWN", "Crossover Sedan", 157.0m, 210.0m, 5, "Hybrid", true, false, false, "Premium commuting, business use, comfort, design-focused buyers", "Premium hybrid crossover-style sedan for customers who want comfort and design presence."),
                Car("HILUX", "Pickup", 161.9m, 161.9m, 5, "Diesel", false, false, false, "Work use, pickup needs, cargo and outdoor utility", "Diesel pickup for customers needing work capability and cargo utility."),
                Car("GR86", "Sports Car", 173.0m, 174.0m, 4, "Gasoline", false, false, false, "Driving enjoyment, coupe buyers, sports car use", "Rear-wheel-drive sports coupe for customers prioritizing driving fun."),
                Car("GR YARIS", "Sports Hatchback", 195.0m, 199.0m, 4, "Gasoline", false, false, false, "Performance driving, GR enthusiasts, compact sports car buyers", "High-performance GR hatchback for enthusiast buyers."),
                Car("SIENNA", "MPV", 239.0m, 296.0m, 7, "Hybrid", true, false, false, "Large families, premium family travel, passengers and cargo space", "Hybrid MPV for larger families needing more seating and travel comfort."),
                Car("GR SUPRA", "Sports Car", 266.0m, 280.0m, 2, "Gasoline", false, false, false, "Performance driving, two-seat sports car buyers, GR enthusiasts", "Flagship GR sports car for customers focused on performance."),
                Car("LAND CRUISER", "SUV", 288.0m, 290.0m, 5, "Diesel", false, true, false, "Off-road capability, premium SUV needs, rugged long-distance travel", "Diesel SUV for customers who need stronger rugged capability and premium presence."),
                Car("ALPHARD", "Luxury MPV", 316.0m, 330.0m, 7, "Hybrid / Plug-in Hybrid", true, false, false, "Executive travel, luxury family use, chauffeur-style comfort", "Luxury MPV with hybrid and plug-in hybrid options for premium passenger comfort.")
            ];
        }

        private static ToyotaCar Car(
            string model,
            string category,
            decimal startingPriceWan,
            decimal maxPriceWan,
            int seats,
            string fuelType,
            bool hasHybridOption,
            bool isSuv,
            bool isElectric,
            string bestFor,
            string description)
        {
            return new ToyotaCar
            {
                Model = model,
                Category = category,
                StartingPriceWan = startingPriceWan,
                MaxPriceWan = maxPriceWan,
                Seats = seats,
                FuelType = fuelType,
                HasHybridOption = hasHybridOption,
                IsSuv = isSuv,
                IsElectric = isElectric,
                EngineCc = null,
                HorsePower = "Varies by grade; check official specification page",
                FuelEconomyKmPerLiter = null,
                BestFor = bestFor,
                Description = description,
                SourceUrl = "https://www.toyota.com.tw/offer.aspx",
                SourceCheckedDate = SourceCheckedDate
            };
        }
    }
}

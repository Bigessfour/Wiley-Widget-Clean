using System.Collections.Generic;
using WileyWidget.Models;

namespace WileyWidget.Services;

public static class SampleDataFactory
{
    public static IReadOnlyList<Enterprise> CreateSampleEnterprises()
    {
        return new List<Enterprise>
        {
            new()
            {
                Id = 1,
                Name = "Water Utility",
                Description = "Primary water supply and distribution for Wiley residents",
                Type = "Water",
                CurrentRate = 26.50m,
                CitizenCount = 2400,
                MonthlyExpenses = 52000m,
                TotalBudget = 780000m,
                Notes = "Includes treatment plant upgrades and main replacements",
                Status = EnterpriseStatus.Active,
                LastModified = DateTime.Today.AddDays(-7)
            },
            new()
            {
                Id = 2,
                Name = "Wastewater Treatment",
                Description = "Collection and treatment services for the sanitation district",
                Type = "Sewer",
                CurrentRate = 34.75m,
                CitizenCount = 2250,
                MonthlyExpenses = 82000m,
                TotalBudget = 984000m,
                Notes = "Plant modernization scheduled for Q4",
                Status = EnterpriseStatus.Active,
                LastModified = DateTime.Today.AddDays(-10)
            },
            new()
            {
                Id = 3,
                Name = "Solid Waste Management",
                Description = "Curbside trash and recycling pickup",
                Type = "Waste",
                CurrentRate = 17.25m,
                CitizenCount = 2300,
                MonthlyExpenses = 35200m,
                TotalBudget = 432000m,
                Notes = "Route optimization pilot underway",
                Status = EnterpriseStatus.Active,
                LastModified = DateTime.Today.AddDays(-5)
            },
            new()
            {
                Id = 4,
                Name = "Renewable Energy Program",
                Description = "Solar and wind initiatives supporting municipal facilities",
                Type = "Electric",
                CurrentRate = 42.50m,
                CitizenCount = 1800,
                MonthlyExpenses = 88000m,
                TotalBudget = 1056000m,
                Notes = "Power purchase agreement renegotiation pending",
                Status = EnterpriseStatus.Active,
                LastModified = DateTime.Today.AddDays(-12)
            },
            new()
            {
                Id = 5,
                Name = "Broadband Infrastructure",
                Description = "Fiber build-out to underserved neighborhoods",
                Type = "Infrastructure",
                CurrentRate = 29.00m,
                CitizenCount = 1500,
                MonthlyExpenses = 41000m,
                TotalBudget = 612000m,
                Notes = "Grant funding covers 35% of capital costs",
                Status = EnterpriseStatus.Active,
                LastModified = DateTime.Today.AddDays(-3)
            },
            new()
            {
                Id = 6,
                Name = "Parks & Recreation",
                Description = "Community programming and facility maintenance",
                Type = "Parks",
                CurrentRate = 12.75m,
                CitizenCount = 1950,
                MonthlyExpenses = 28500m,
                TotalBudget = 378000m,
                Notes = "Seasonal staffing ramping up for summer programs",
                Status = EnterpriseStatus.Active,
                LastModified = DateTime.Today.AddDays(-2)
            }
        };
    }
}

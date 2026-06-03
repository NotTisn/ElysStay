using Bogus;

namespace Tests.Integration
{
    public static class TestDataSeeder
    {
        public static object GenerateProperty()
        {
            var propertyFaker = new Faker<dynamic>()
                .RuleFor("Name", f => f.Company.CompanyName())
                .RuleFor("Description", f => f.Lorem.Paragraph())
                .RuleFor("PricePerNight", f => f.Random.Decimal(50, 500));

            return propertyFaker.Generate();
        }
    }
}

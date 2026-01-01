using Equibles.AgentQL.Demo.Models;

namespace Equibles.AgentQL.Demo;

public static class DataSeeder
{
    public static void Seed(TravelDbContext db)
    {
        if (db.Customers.Any())
            return;

        var customers = new List<Customer>
        {
            new() { Name = "Alice Johnson", Email = "alice@example.com", Phone = "+1-555-0101", LoyaltyTier = LoyaltyTier.Gold },
            new() { Name = "Bob Smith", Email = "bob@example.com", Phone = "+1-555-0102", LoyaltyTier = LoyaltyTier.Silver },
            new() { Name = "Carol Davis", Email = "carol@example.com", Phone = "+1-555-0103", LoyaltyTier = LoyaltyTier.Platinum },
            new() { Name = "David Wilson", Email = "david@example.com", Phone = "+1-555-0104", LoyaltyTier = LoyaltyTier.Bronze },
            new() { Name = "Emma Brown", Email = "emma@example.com", Phone = "+1-555-0105", LoyaltyTier = LoyaltyTier.Gold },
            new() { Name = "Frank Miller", Email = "frank@example.com", Phone = "+1-555-0106", LoyaltyTier = LoyaltyTier.Silver },
            new() { Name = "Grace Lee", Email = "grace@example.com", Phone = "+1-555-0107", LoyaltyTier = LoyaltyTier.Bronze },
            new() { Name = "Henry Taylor", Email = "henry@example.com", Phone = "+1-555-0108", LoyaltyTier = LoyaltyTier.Gold },
            new() { Name = "Ivy Chen", Email = "ivy@example.com", Phone = "+1-555-0109", LoyaltyTier = LoyaltyTier.Platinum },
            new() { Name = "Jack Martin", Email = "jack@example.com", Phone = "+1-555-0110", LoyaltyTier = LoyaltyTier.Bronze },
        };
        db.Customers.AddRange(customers);
        db.SaveChanges();

        var destinations = new List<Destination>
        {
            new() { Name = "Paris", Country = "France", Description = "The City of Light with iconic landmarks and world-class cuisine", PricePerNight = 180m },
            new() { Name = "Tokyo", Country = "Japan", Description = "A vibrant metropolis blending ancient tradition with cutting-edge technology", PricePerNight = 150m },
            new() { Name = "Cancun", Country = "Mexico", Description = "Beautiful Caribbean beaches and Mayan ruins", PricePerNight = 120m },
            new() { Name = "Santorini", Country = "Greece", Description = "Stunning sunsets and whitewashed architecture on a volcanic island", PricePerNight = 220m },
            new() { Name = "Bali", Country = "Indonesia", Description = "Tropical paradise with lush rice terraces and ancient temples", PricePerNight = 95m },
            new() { Name = "New York", Country = "United States", Description = "The city that never sleeps — Broadway, Central Park, and world-class dining", PricePerNight = 250m },
            new() { Name = "Marrakech", Country = "Morocco", Description = "Colorful souks, historic palaces, and vibrant street life", PricePerNight = 85m },
            new() { Name = "Sydney", Country = "Australia", Description = "Iconic harbor, beautiful beaches, and a thriving arts scene", PricePerNight = 200m },
        };
        db.Destinations.AddRange(destinations);
        db.SaveChanges();

        var bookings = new List<Booking>
        {
            new() { CustomerId = 1, DestinationId = 1, CheckIn = new DateTime(2026, 3, 10), CheckOut = new DateTime(2026, 3, 15), TotalPrice = 900m, Status = BookingStatus.Confirmed },
            new() { CustomerId = 1, DestinationId = 4, CheckIn = new DateTime(2026, 6, 1), CheckOut = new DateTime(2026, 6, 8), TotalPrice = 1540m, Status = BookingStatus.Pending },
            new() { CustomerId = 2, DestinationId = 3, CheckIn = new DateTime(2026, 4, 5), CheckOut = new DateTime(2026, 4, 12), TotalPrice = 840m, Status = BookingStatus.Confirmed },
            new() { CustomerId = 3, DestinationId = 6, CheckIn = new DateTime(2026, 5, 20), CheckOut = new DateTime(2026, 5, 25), TotalPrice = 1250m, Status = BookingStatus.Completed },
            new() { CustomerId = 3, DestinationId = 2, CheckIn = new DateTime(2026, 7, 1), CheckOut = new DateTime(2026, 7, 10), TotalPrice = 1350m, Status = BookingStatus.Pending },
            new() { CustomerId = 4, DestinationId = 5, CheckIn = new DateTime(2026, 3, 15), CheckOut = new DateTime(2026, 3, 22), TotalPrice = 665m, Status = BookingStatus.Cancelled },
            new() { CustomerId = 5, DestinationId = 7, CheckIn = new DateTime(2026, 4, 10), CheckOut = new DateTime(2026, 4, 16), TotalPrice = 510m, Status = BookingStatus.Confirmed },
            new() { CustomerId = 5, DestinationId = 8, CheckIn = new DateTime(2026, 8, 1), CheckOut = new DateTime(2026, 8, 7), TotalPrice = 1200m, Status = BookingStatus.Pending },
            new() { CustomerId = 6, DestinationId = 1, CheckIn = new DateTime(2026, 5, 5), CheckOut = new DateTime(2026, 5, 10), TotalPrice = 900m, Status = BookingStatus.Confirmed },
            new() { CustomerId = 7, DestinationId = 3, CheckIn = new DateTime(2026, 6, 15), CheckOut = new DateTime(2026, 6, 20), TotalPrice = 600m, Status = BookingStatus.Pending },
            new() { CustomerId = 8, DestinationId = 2, CheckIn = new DateTime(2026, 4, 1), CheckOut = new DateTime(2026, 4, 5), TotalPrice = 600m, Status = BookingStatus.Completed },
            new() { CustomerId = 9, DestinationId = 4, CheckIn = new DateTime(2026, 3, 20), CheckOut = new DateTime(2026, 3, 27), TotalPrice = 1540m, Status = BookingStatus.Confirmed },
            new() { CustomerId = 9, DestinationId = 6, CheckIn = new DateTime(2026, 9, 1), CheckOut = new DateTime(2026, 9, 5), TotalPrice = 1000m, Status = BookingStatus.Pending },
            new() { CustomerId = 10, DestinationId = 5, CheckIn = new DateTime(2026, 5, 10), CheckOut = new DateTime(2026, 5, 17), TotalPrice = 665m, Status = BookingStatus.Confirmed },
            new() { CustomerId = 10, DestinationId = 7, CheckIn = new DateTime(2026, 7, 20), CheckOut = new DateTime(2026, 7, 25), TotalPrice = 425m, Status = BookingStatus.Cancelled },
        };
        db.Bookings.AddRange(bookings);
        db.SaveChanges();

        var payments = new List<Payment>
        {
            new() { BookingId = 1, Amount = 900m, Method = PaymentMethod.CreditCard, PaidAt = new DateTime(2026, 2, 15) },
            new() { BookingId = 3, Amount = 840m, Method = PaymentMethod.DebitCard, PaidAt = new DateTime(2026, 3, 10) },
            new() { BookingId = 4, Amount = 625m, Method = PaymentMethod.CreditCard, PaidAt = new DateTime(2026, 4, 20) },
            new() { BookingId = 4, Amount = 625m, Method = PaymentMethod.BankTransfer, PaidAt = new DateTime(2026, 5, 1) },
            new() { BookingId = 7, Amount = 510m, Method = PaymentMethod.CreditCard, PaidAt = new DateTime(2026, 3, 20) },
            new() { BookingId = 9, Amount = 900m, Method = PaymentMethod.Cash, PaidAt = new DateTime(2026, 4, 15) },
            new() { BookingId = 11, Amount = 600m, Method = PaymentMethod.DebitCard, PaidAt = new DateTime(2026, 3, 5) },
            new() { BookingId = 12, Amount = 1540m, Method = PaymentMethod.CreditCard, PaidAt = new DateTime(2026, 3, 1) },
            new() { BookingId = 14, Amount = 665m, Method = PaymentMethod.BankTransfer, PaidAt = new DateTime(2026, 4, 25) },
            new() { BookingId = 5, Amount = 675m, Method = PaymentMethod.CreditCard, PaidAt = new DateTime(2026, 6, 10) },
            new() { BookingId = 5, Amount = 675m, Method = PaymentMethod.DebitCard, PaidAt = new DateTime(2026, 6, 15) },
            new() { BookingId = 10, Amount = 600m, Method = PaymentMethod.Cash, PaidAt = new DateTime(2026, 5, 20) },
        };
        db.Payments.AddRange(payments);
        db.SaveChanges();
    }
}

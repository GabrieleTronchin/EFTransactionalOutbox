using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Sample.TransactionalOutbox.Domain.Order;
using Sample.TransactionalOutbox.Domain.Product;

namespace Sample.TransactionalOutbox.Persistence;

public static class SeedDb
{
    public static void Initialize(IApplicationBuilder app)
    {
        using var serviceScope = app
            .ApplicationServices.GetRequiredService<IServiceScopeFactory>()
            .CreateScope();

        var context =
            serviceScope.ServiceProvider.GetService<ShopDbContext>()
            ?? throw new NullReferenceException(
                $"Cannot find any service for {nameof(ShopDbContext)}"
            );

        context.Database.EnsureCreated();

        // Seed products with realistic marketplace data
        var wirelessHeadphones = ProductEntity.Create(
            "Wireless Headphones",
            79.99m,
            "WH-1000",
            50,
            "Premium over-ear wireless headphones with active noise cancellation and 30-hour battery life");
        context.Products.Add(wirelessHeadphones);

        var mechanicalKeyboard = ProductEntity.Create(
            "Mechanical Keyboard",
            129.99m,
            "MK-8700",
            30,
            "Full-size mechanical keyboard with Cherry MX Blue switches and RGB backlighting");
        context.Products.Add(mechanicalKeyboard);

        var usbCHub = ProductEntity.Create(
            "USB-C Hub",
            49.99m,
            "UCH-7100",
            100,
            "7-in-1 USB-C hub with HDMI, USB 3.0, SD card reader, and 100W power delivery");
        context.Products.Add(usbCHub);

        var gamingMouse = ProductEntity.Create(
            "Gaming Mouse",
            59.99m,
            "GM-PRO-500",
            75,
            "Ergonomic gaming mouse with 16000 DPI sensor and programmable buttons");
        context.Products.Add(gamingMouse);

        var portableMonitor = ProductEntity.Create(
            "Portable Monitor",
            249.99m,
            "PM-156IPS",
            20,
            "15.6-inch portable IPS monitor with USB-C connectivity and built-in speakers");
        context.Products.Add(portableMonitor);

        var webcam = ProductEntity.Create(
            "HD Webcam",
            39.99m,
            "WC-1080P",
            120,
            "1080p HD webcam with auto-focus, built-in microphone, and privacy shutter");
        context.Products.Add(webcam);

        // Seed orders in Pending status referencing seeded products
        var order1 = OrderEntity.Create(
            wirelessHeadphones.Id,
            2,
            159.98m,
            "Alice Johnson",
            "742 Evergreen Terrace, Springfield, IL 62704");
        context.Orders.Add(order1);

        var order2 = OrderEntity.Create(
            mechanicalKeyboard.Id,
            1,
            129.99m,
            "Bob Smith",
            "1600 Pennsylvania Ave NW, Washington, DC 20500");
        context.Orders.Add(order2);

        context.SaveChanges();
    }
}

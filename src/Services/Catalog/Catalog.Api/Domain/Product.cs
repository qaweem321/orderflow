using OrderFlow.BuildingBlocks.Errors;

namespace Catalog.Api.Domain;

public class Product
{
    private Product() { }

    public Product(string sku, string name, string description, long priceCents, int stock, DateTimeOffset now)
    {
        Sku = sku.Trim().ToUpperInvariant();
        Update(name, description, priceCents, isActive: true, now);
        SetStock(stock, now);
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public string Sku { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string Description { get; private set; } = "";

    /// <summary>Price in cents (USD). Integers avoid floating-point rounding.</summary>
    public long PriceCents { get; private set; }

    /// <summary>Units available to order. Reserved units are already taken off.</summary>
    public int Stock { get; private set; }

    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public void Update(string name, string description, long priceCents, bool isActive, DateTimeOffset now)
    {
        if (priceCents <= 0)
        {
            throw new RuleException("invalid_price", "Price must be greater than zero.");
        }

        Name = name.Trim();
        Description = description.Trim();
        PriceCents = priceCents;
        IsActive = isActive;
        UpdatedAt = now;
    }

    public void SetStock(int stock, DateTimeOffset now)
    {
        if (stock < 0)
        {
            throw new RuleException("invalid_stock", "Stock cannot be negative.");
        }

        Stock = stock;
        UpdatedAt = now;
    }

    /// <summary>Why this product can't supply <paramref name="quantity"/> units, or null if it can.</summary>
    public string? CannotReserve(int quantity) =>
        !IsActive ? $"{Name} is no longer sold."
        : quantity > Stock ? (Stock == 0 ? $"{Name} is out of stock." : $"Only {Stock} of {Name} left.")
        : null;

    public void Reserve(int quantity, DateTimeOffset now)
    {
        var reason = CannotReserve(quantity);
        if (reason is not null)
        {
            throw new RuleException("insufficient_stock", reason);
        }

        Stock -= quantity;
        UpdatedAt = now;
    }

    public void Release(int quantity, DateTimeOffset now)
    {
        Stock += quantity;
        UpdatedAt = now;
    }
}

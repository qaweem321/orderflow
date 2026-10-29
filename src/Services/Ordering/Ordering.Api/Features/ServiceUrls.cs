using System.ComponentModel.DataAnnotations;

namespace Ordering.Api.Features;

public sealed class ServiceUrls
{
    public const string Section = "Services";

    /// <summary>Catalog's internal address, e.g. <c>http://catalog:8080</c> in Docker.</summary>
    [Required, Url]
    public string Catalog { get; set; } = "";
}

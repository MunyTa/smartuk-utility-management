using System.ComponentModel.DataAnnotations;

namespace UkManagement.Web.Domain;

public sealed class Building
{
    public int Id { get; set; }

    [MaxLength(220)]
    public required string Address { get; set; }

    [MaxLength(80)]
    public required string ManagementDistrict { get; set; }

    public ICollection<Apartment> Apartments { get; set; } = [];
}

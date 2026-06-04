using System.ComponentModel.DataAnnotations;

namespace UkManagement.Web.Domain;

public sealed class Apartment
{
    public int Id { get; set; }

    [MaxLength(24)]
    public required string Number { get; set; }

    public int Floor { get; set; }

    public int BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public ICollection<Resident> Residents { get; set; } = [];
    public ICollection<Meter> Meters { get; set; } = [];
}

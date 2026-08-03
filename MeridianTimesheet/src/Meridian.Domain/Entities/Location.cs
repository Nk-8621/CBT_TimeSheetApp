namespace Meridian.Domain.Entities;

public class Location
{
    public int LocationId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
}

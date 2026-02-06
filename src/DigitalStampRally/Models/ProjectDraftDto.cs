namespace DigitalStampRally.Models;

public class ProjectDraftDto
{
    public string? EventTitle { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
    public List<ProjectDraftSpotDto>? Spots { get; set; }
}

public class ProjectDraftSpotDto
{
    public string? SpotName { get; set; }
    public bool IsRequired { get; set; }
}

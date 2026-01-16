namespace NetSentry.Server.Models;

public class EventLog
{
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; }
    public string Level { get; set; } = null!;
    public string Message { get; set; } = null!;

    // Внешний ключ на Machine
    public int MachineId { get; set; }
    public Machine Machine { get; set; } = null!;
}

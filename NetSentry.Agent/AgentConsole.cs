namespace NetSentry.Agent;

/// <summary>
/// Единая точка вывода в консоль. Отладочные сообщения — только при Debug=true в appsettings.
/// </summary>
public static class AgentConsole
{
    public static bool IsDebug { get; set; }

    public static void DebugLine(string message)
    {
        if (IsDebug)
            Console.WriteLine(message);
    }

    public static void InitLine(string message) => DebugLine(message);
}

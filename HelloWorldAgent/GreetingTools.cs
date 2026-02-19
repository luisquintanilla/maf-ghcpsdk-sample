using System.ComponentModel;

namespace HelloWorldAgent;

/// <summary>
/// Static methods that serve as the agent's tools.
///
/// Design note: Tool implementations live here, separate from agent wiring (Program.cs).
/// Each method is a pure function with no AI dependencies — easy to unit-test and swap out.
/// The [Description] attributes are read by AIFunctionFactory (via reflection) and sent to the
/// LLM as tool metadata, so the model knows when and how to call each tool.
///
/// Future: add more specialist tool classes alongside this one (WeatherTools, SearchTools, etc.)
/// and register them with additional AIAgent instances for a multi-agent architecture.
/// </summary>
internal static class GreetingTools
{
    /// <summary>Greets a person by name.</summary>
    [Description("Greets a person by name with a warm, friendly message")]
    public static string GetGreeting(
        [Description("The name of the person to greet")] string name)
        => $"Hello, {name}! \ud83d\udc4b Great to meet you!";

    /// <summary>Returns the current local date and time.</summary>
    [Description("Returns the current local date and time")]
    public static string GetCurrentTime()
        => $"It's {DateTime.Now:h:mm tt} on {DateTime.Now:dddd, MMMM d, yyyy}.";
}

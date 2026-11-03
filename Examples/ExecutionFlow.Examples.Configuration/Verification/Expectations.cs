namespace ExecutionFlow.Examples.Configuration.Verification;

/// <summary>Prints each expectation as "ok" or "FAIL" and counts the failures.</summary>
public class Expectations
{
    public int Failures { get; private set; }

    /// <summary>Expects <paramref name="actual"/> to be <paramref name="expected"/>.</summary>
    public void Equal<T>(string what, T expected, T actual)
    {
        var ok = EqualityComparer<T>.Default.Equals(expected, actual);
        Print(ok, ok ? $"{what}: {Show(expected)}" : $"{what}: expected {Show(expected)}, got {Show(actual)}");
    }

    /// <summary>Expects <paramref name="condition"/> to hold.</summary>
    public void True(string what, bool condition)
    {
        Print(condition, what);
    }

    private static string Show(object? value) => value?.ToString() ?? "none";

    private void Print(bool ok, string line)
    {
        Console.WriteLine($"  {(ok ? "ok  " : "FAIL")} {line}");
        if (!ok)
            Failures++;
    }
}

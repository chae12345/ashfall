using System;
internal static class Program
{
    private static void Main()
    {
        try { AshfallCoreChecks.Run(); }
        catch (Exception ex) { Console.WriteLine("FAIL: " + ex.Message); Environment.Exit(1); }
    }
}

using PackFrame;

internal static class ModDetailsNavigationChecks
{
    internal static void Run()
    {
        Require(ModDetailsNavigation.ShouldReset(null, null, "one", "/mods/one", false), "first detail selection starts at the beginning");
        Require(ModDetailsNavigation.ShouldReset("one", "/mods/one", "two", "/mods/two", false), "different mod starts at its title rather than the old paragraph");
        Require(!ModDetailsNavigation.ShouldReset("one", "/mods/one", "ONE", "/mods/one", false), "same mod refresh preserves reading position despite package casing");
        Require(ModDetailsNavigation.ShouldReset("one", "/mods/one", "one", "/copy/one", false), "another source of the same package starts at the beginning");
        Require(ModDetailsNavigation.ShouldReset("one", "/mods/one", null, null, false), "clearing selection resets stale detail offset");
        Require(!ModDetailsNavigation.ShouldReset(null, null, null, null, false), "repeated empty details do not request another reset");
        Require(!ModDetailsNavigation.ShouldReset("one", "C:/Mods/One", "one", "c:/mods/one", true), "Windows source path casing preserves reading context");
        Require(ModDetailsNavigation.ShouldReset("one", "/mods/One", "one", "/mods/one", false), "case-sensitive source paths remain distinct");
        Require(ModDetailsNavigation.ShouldReset("one", "/mods/shared", "two", "/mods/shared", false), "changed package in the same directory resets reading context");
    }
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS: " + message); }
}

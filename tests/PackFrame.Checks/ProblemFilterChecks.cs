using System.Reflection;
using PackFrame;

internal static class ProblemFilterChecks
{
    public static void Run()
    {
        var window = typeof(MainWindow);
        var stateType = window.GetNestedType("ProblemFilterState", BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(stateType)!;
        var apply = window.GetMethod("ApplyProblemFilters", BindingFlags.NonPublic | BindingFlags.Static)!;
        var cycle = window.GetMethod("CycleProblemFilter", BindingFlags.NonPublic | BindingFlags.Static)!;
        void Set(string field, string mode)
        {
            var member = stateType.GetField(field)!;
            member.SetValue(state, Enum.Parse(member.FieldType, mode));
        }
        var mods = new[]
        {
            Mod("local", ModSource.Local) with { VersionMismatch = true, SortError = true },
            Mod("workshop", ModSource.Workshop),
            Mod("profile", ModSource.Local) with { IsProfileMods = true, IsDuplicate = true },
            Mod("official", ModSource.Official),
            Mod("unknown", ModSource.Unknown)
        };
        var original = mods.Select(mod => mod.PackageId).ToArray();
        void Check(string message, params string[] expected)
        {
            var result = (List<ModInfo>)apply.Invoke(null, new object[] { mods, state })!;
            if (!result.Select(mod => mod.PackageId).SequenceEqual(expected))
                throw new Exception("FAIL: " + message);
            Console.WriteLine("PASS: " + message);
        }
        Check("footer filters start off", original);
        Set("Local", "Hide");
        Check("excluding local keeps workshop, official and unknown sources", "workshop", "official", "unknown");
        Set("Local", "Only");
        Check("local-only includes instance-local mods in original order", "local", "profile");
        Set("Version", "Hide");
        Check("local combines with version exclusion", "profile");
        Set("Duplicate", "Hide");
        Check("combined footer filters can produce an empty list");
        Set("Version", "Off");
        Set("Duplicate", "Off");
        Set("Sort", "Only");
        Check("local combines with sort filtering", "local");
        Set("Sort", "Off");
        Set("Local", "Off");
        Check("clearing filters restores membership and order", original);
        var local = stateType.GetField("Local")!;
        foreach (var expected in new[] { "Hide", "Only", "Off" })
        {
            local.SetValue(state, cycle.Invoke(null, new[] { local.GetValue(state) }));
            if (local.GetValue(state)!.ToString() != expected) throw new Exception("FAIL: local filter cycle");
        }
        if (!mods.Select(mod => mod.PackageId).SequenceEqual(original)) throw new Exception("FAIL: input list mutated");
        Console.WriteLine("PASS: local cycles Off/Hide/Only/Off without mutating input");
    }

    private static ModInfo Mod(string id, ModSource source) =>
        new(id, "", "", id, "", null, source, "1.7", DateTime.MinValue);
}

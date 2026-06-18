using System;
using System.Collections.Generic;

// Deterministic hiring-candidate generation from archetypes. (Phase 4b.)
// Same seed -> same candidates, so it is fully unit-testable.
public static class CandidateGenerator
{
    public static StaffMember Generate(StaffArchetypeSO archetype, int seed)
    {
        return GenerateWith(archetype, new System.Random(seed));
    }

    public static List<StaffMember> GeneratePool(StaffArchetypeSO[] archetypes, int count, int seed)
    {
        var list = new List<StaffMember>();
        if (archetypes == null || archetypes.Length == 0 || count <= 0) return list;
        var rng = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            var a = archetypes[rng.Next(archetypes.Length)];
            list.Add(GenerateWith(a, rng));
        }
        return list;
    }

    private static StaffMember GenerateWith(StaffArchetypeSO a, System.Random rng)
    {
        string name = (a.namePool != null && a.namePool.Length > 0)
            ? a.namePool[rng.Next(a.namePool.Length)]
            : a.role.ToString();

        int wage = RangeInclusive(rng, a.wageMin, a.wageMax);
        var attr = new StaffAttributes(
            RangeInclusive(rng, a.speedRange.x, a.speedRange.y),
            RangeInclusive(rng, a.qualityRange.x, a.qualityRange.y),
            RangeInclusive(rng, a.staminaRange.x, a.staminaRange.y));
        var traits = PickTraits(a, rng);
        return new StaffMember(a.role, name, wage, attr, a.educationLevel, traits);
    }

    private static int RangeInclusive(System.Random rng, int min, int max)
    {
        if (max < min) { int t = min; min = max; max = t; }
        return rng.Next(min, max + 1);
    }

    private static List<StaffTrait> PickTraits(StaffArchetypeSO a, System.Random rng)
    {
        var result = new List<StaffTrait>();
        if (a.possibleTraits == null || a.possibleTraits.Length == 0) return result;
        int lo = Math.Max(0, a.minTraits);
        int hi = Math.Max(lo, a.maxTraits);
        int n = Math.Min(RangeInclusive(rng, lo, hi), a.possibleTraits.Length);
        var pool = new List<StaffTrait>(a.possibleTraits);
        for (int i = 0; i < n; i++)
        {
            int idx = rng.Next(pool.Count);
            result.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return result;
    }
}

using UnityEngine;

// Template for generating hiring-pool candidates of a given role. (Phase 4b.)
[CreateAssetMenu(fileName = "StaffArchetype", menuName = "Old Town Hotel/Staff Archetype", order = 103)]
public sealed class StaffArchetypeSO : ScriptableObject
{
    public StaffRole role = StaffRole.Housekeeper;
    public string[] namePool = new[] { "Bob", "Liz", "Ann", "Mae", "Sam", "Joe" };

    [Header("Daily wage range (inclusive)")]
    public int wageMin = 30;
    public int wageMax = 60;

    [Header("Attribute ranges (x=min, y=max, 0-100)")]
    public Vector2Int speedRange = new Vector2Int(30, 80);
    public Vector2Int qualityRange = new Vector2Int(30, 80);
    public Vector2Int staminaRange = new Vector2Int(30, 80);

    public int educationLevel = 0;

    [Header("Traits")]
    public StaffTrait[] possibleTraits;
    public int minTraits = 1;
    public int maxTraits = 2;
}

using UnityEngine;

[System.Serializable]
public class Room2DAttribute
{
    public Room2DAttributeType type;
    [Range(0, 100)] public int condition = 100;
    public string note;

    public bool HasProblem()
    {
        return condition < 50;
    }

    public string GetDisplayName()
    {
        return type + ": " + condition;
    }
}

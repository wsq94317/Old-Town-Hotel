using UnityEngine;

public static class RoomStateUiMap
{
    public static Color GetColor(Room2DState state, UITheme theme)
    {
        if (theme == null) return Color.magenta;
        switch (state)
        {
            case Room2DState.Ready:              return theme.stateReady;
            case Room2DState.Dirty:              return theme.stateDirty;
            case Room2DState.Cleaning:           return theme.stateCleaning;
            case Room2DState.AwaitingInspection: return theme.stateInsp;
            case Room2DState.Occupied:           return theme.stateOccupied;
            case Room2DState.Blocked:            return theme.stateBlocked;
            default:                             return Color.magenta;
        }
    }

    public static string GetLabel(Room2DState state)
    {
        switch (state)
        {
            case Room2DState.Ready:              return "READY";
            case Room2DState.Dirty:              return "DIRTY";
            case Room2DState.Cleaning:           return "CLEAN";
            case Room2DState.AwaitingInspection: return "INSP";
            case Room2DState.Occupied:           return "OCC";
            case Room2DState.Blocked:            return "BLOCK";
            default:                             return "?";
        }
    }

    public static string GetBadgeLetter(Room2DState state)
    {
        switch (state)
        {
            case Room2DState.Ready:              return "R";
            case Room2DState.Dirty:              return "D";
            case Room2DState.Cleaning:           return "C";
            case Room2DState.AwaitingInspection: return "I";
            case Room2DState.Occupied:           return "O";
            case Room2DState.Blocked:            return "B";
            default:                             return "?";
        }
    }
}

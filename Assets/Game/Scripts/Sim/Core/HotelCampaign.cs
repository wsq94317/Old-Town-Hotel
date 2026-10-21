using System;

[Serializable]
public sealed class HotelPresentationState
{
    public bool tutorialCompleted, reopeningCelebrated;
    public bool hasManagerPosition;
    public float managerX, managerY, managerZ;
    public int viewControlMode; // 0 = director, 1 = walk with the manager. Old saves default to director.
    public bool hasCameraView;
    public float cameraX, cameraY, cameraZ, cameraYaw, cameraPitch, cameraZoom;
    public bool soundEnabled = true, musicEnabled = true;
}

public static class HotelCampaign
{
    public static bool CanReopen(int debt, int totalRooms, int ruined, int blocked, int construction,
        int renovated, int unpaidWages, float stars)
        => debt <= 0 && totalRooms >= 100 && ruined == 0 && blocked == 0 && construction == 0
            && renovated >= totalRooms && unpaidWages <= 0 && stars >= 3f;
}

using UnityEngine;

public class RoomController : MonoBehaviour
{
    public RoomState currentState;

    public Material dirtyMaterial;
    public Material cleaningMaterial;
    public Material awaitingInspectionMaterial;
    public Material readyMaterial;

    private Renderer roomRenderer;

    private void Start()
    {
        roomRenderer = GetComponent<Renderer>();
        ApplyStateVisual();
    }

    public void ApplyStateVisual()
    {
        if (roomRenderer == null)
        {
            roomRenderer = GetComponent<Renderer>();
        }

        if (roomRenderer == null)
        {
            return;
        }

        switch (currentState)
        {
            case RoomState.Dirty:
                roomRenderer.material = dirtyMaterial;
                break;
            case RoomState.Cleaning:
                roomRenderer.material = cleaningMaterial;
                break;
            case RoomState.AwaitingInspection:
                roomRenderer.material = awaitingInspectionMaterial;
                break;
            case RoomState.Ready:
                roomRenderer.material = readyMaterial;
                break;
        }
    }
}

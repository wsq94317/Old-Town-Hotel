using UnityEngine;

public class RoomController : MonoBehaviour
{
    public RoomState currentState;

    public Material dirtyMaterial;
    public Material cleaningMaterial;
    public Material awaitingInspectionMaterial;
    public Material readyMaterial;

    private Renderer roomRenderer;

    private void Awake()
    {
        Debug.Log($"{gameObject.name} Awake");
    }

    private void Start()
    {
        Debug.Log($"{gameObject.name} Start");

        roomRenderer = GetComponent<Renderer>();
        ApplyStateVisual();
    }

    private void OnMouseDown()
    {
        Debug.Log($"{gameObject.name} OnMouseDown triggered");
        CycleState();
    }

    public void CycleState()
    {
        switch (currentState)
        {
            case RoomState.Dirty:
                currentState = RoomState.Cleaning;
                break;
            case RoomState.Cleaning:
                currentState = RoomState.AwaitingInspection;
                break;
            case RoomState.AwaitingInspection:
                currentState = RoomState.Ready;
                break;
            case RoomState.Ready:
                currentState = RoomState.Dirty;
                break;
        }

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
            Debug.LogWarning($"{gameObject.name} has no Renderer.");
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
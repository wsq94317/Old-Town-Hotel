using UnityEngine;

public class Room2DPrototypeNotes : MonoBehaviour
{
    // Purpose:
    // This scene is the first test bed for a fake-3D 2D hotel room view.
    // Keep it simple: one room tile, one controller, and a few visible states.

    // Suggested scene setup:
    // 1. Use a portrait Game view.
    // 2. Use an Orthographic camera.
    // 3. Build one room from flat 2D sprites or UI Images.
    // 4. Fake depth with layered sprites, shadows, scale, and sorting order.
    // 5. Attach Room2DController to the room root object.
    // 6. Drag the room SpriteRenderer or Image into the controller.
    // 7. Optionally create one child object per state and assign them to the visual slots.
    // 8. Add simple UI Buttons that call SetDirty, SetCleaning, SetAwaitingInspection, SetReady, or CycleToNextState.
    // 9. Add UI labels for roomName, current state, and next action.
    // 10. Assign TMP labels to the TextMeshPro fields if using Text (TMP).

    // First visual target:
    // Dirty = messy room color or overlay.
    // Cleaning = cleaning marker or blue tint.
    // AwaitingInspection = yellow warning/check marker.
    // Ready = clean green/neutral room.

    // Do not add front desk, lounge, random events, or old 3D click logic here yet.
}

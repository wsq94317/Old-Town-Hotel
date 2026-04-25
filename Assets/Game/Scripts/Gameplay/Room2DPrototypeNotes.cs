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
    // 4. Attach Room2DEntity to the room root object for room data.
    // 5. Attach Room2DController to the room root object for visuals and UI.
    // 6. Drag the Room2DEntity component into the controller's roomEntity field.
    // 7. Attach Room2DFakeDepthVisual to the room root object.
    // 8. Create simple sprite children: Shadow, Floor, BackWall, LeftWall, RightWall.
    // 9. Assign those SpriteRenderers to Room2DFakeDepthVisual.
    // 10. Use the component context menu Apply Simple Prototype Layout for a fast starting layout.
    // 11. Drag the room SpriteRenderer or Image into the controller.
    // 12. Optionally create one child object per state and assign them to the visual slots.
    // 13. Add simple UI Buttons that call PerformNextAction or the direct state methods.
    // 14. Add a Room2DLabelView child object under each room entity.
    // 15. Put roomName, current state, next action, and action count TMP labels under that child.
    // 16. Assign those TMP labels to Room2DLabelView.
    // 17. Add Room2DOverview to a scene UI object for summary counts across rooms.

    // First visual target:
    // Dirty = messy room color or overlay.
    // Cleaning = cleaning marker or blue tint.
    // AwaitingInspection = yellow warning/check marker.
    // Ready = clean green/neutral room.

    // Fake-3D target:
    // BackWall = rear layer and main state tint.
    // Floor = middle layer.
    // Side walls = middle layer while using square prototype sprites.
    // Shadow = front layer for the current prototype.
    // Furniture = simple sprite blocks above floor/back wall.
    // True angled side walls should wait until we have real sprites or polygon art.

    // Room overview target:
    // Count how many rooms are Dirty, Cleaning, AwaitingInspection, and Ready.
    // This becomes useful once the first room is duplicated into multiple room entities.

    // Prefab target:
    // Once Room_A_2D contains entity, controller, visuals, and label view, drag it into Assets/Game/Prefabs.
    // Unity Prefabs are the reusable room "class instances" for building many rooms.

    // Do not add front desk, lounge, random events, or old 3D click logic here yet.
}

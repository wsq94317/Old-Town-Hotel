using UnityEngine;

public class RoomClickManager : MonoBehaviour
{
    private void Update()
    {
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }

        Debug.Log("Mouse clicked.");

        if (Camera.main == null)
        {
            Debug.LogWarning("Camera.main not found.");
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (!Physics.Raycast(ray, out hit))
        {
            Debug.Log("Raycast did not hit anything.");
            return;
        }

        GameObject hitObject = hit.collider.gameObject;
        Debug.Log("Hit GameObject: " + hitObject.name);

        RoomController roomController = hitObject.GetComponent<RoomController>();

        if (roomController == null)
        {
            Debug.Log("Hit object does not have a RoomController.");
            return;
        }

        roomController.CycleState();
        Debug.Log("Cycled room state for: " + hitObject.name);
    }
}

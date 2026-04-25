using TMPro;
using UnityEngine;

public class Room2DLabelView : MonoBehaviour
{
    public TMP_Text roomNameLabel;
    public TMP_Text stateLabel;
    public TMP_Text nextActionLabel;
    public TMP_Text actionCountLabel;

    public void Refresh(Room2DEntity roomEntity)
    {
        if (roomEntity == null)
        {
            return;
        }

        if (roomNameLabel != null)
        {
            roomNameLabel.text = roomEntity.roomName;
        }

        if (stateLabel != null)
        {
            stateLabel.text = roomEntity.GetStateDisplayName();
        }

        if (nextActionLabel != null)
        {
            nextActionLabel.text = roomEntity.GetNextActionDisplayName();
        }

        if (actionCountLabel != null)
        {
            actionCountLabel.text = roomEntity.GetActionCountDisplayName();
        }
    }
}

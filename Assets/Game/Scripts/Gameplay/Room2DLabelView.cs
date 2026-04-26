using TMPro;
using UnityEngine;

// 房间自己的文字显示。
// 这些字段都是可选的；不绑定也不会影响房间逻辑。
public class Room2DLabelView : MonoBehaviour
{
    public TMP_Text roomNameLabel;
    public TMP_Text stateLabel;
    public TMP_Text nextActionLabel;
    public TMP_Text actionCountLabel;
    public TMP_Text cleaningPriorityLabel;

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

        if (cleaningPriorityLabel != null)
        {
            cleaningPriorityLabel.text = roomEntity.GetCleaningPriorityDisplayName();
        }
    }
}

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
        Refresh(roomEntity, false, false);
    }

    public void Refresh(Room2DEntity roomEntity, bool isSelected, bool isAssignedToHousekeeper)
    {
        if (roomEntity == null)
        {
            return;
        }

        if (roomNameLabel != null)
        {
            // 选中的房间直接在房号前加标记，方便在 Game 窗口里定位当前操作对象。
            string selectedMarker = isSelected ? "> " : "";
            roomNameLabel.text = selectedMarker + roomEntity.roomName;
        }

        if (stateLabel != null)
        {
            stateLabel.text = BuildStateText(roomEntity, isSelected, isAssignedToHousekeeper);
        }

        if (nextActionLabel != null)
        {
            nextActionLabel.text = roomEntity.GetShowcaseNextActionShortText();
        }

        if (actionCountLabel != null)
        {
            actionCountLabel.text = roomEntity.GetShowcaseWaitText();
        }

        if (cleaningPriorityLabel != null)
        {
            cleaningPriorityLabel.text = roomEntity.markedCleaningPriority || roomEntity.markedInspectionPriority
                ? roomEntity.preparationPriorityLabel
                : roomEntity.GetCleaningPriorityDisplayName();
        }
    }

    private string BuildStateText(Room2DEntity roomEntity, bool isSelected, bool isAssignedToHousekeeper)
    {
        string stateText = roomEntity.GetShowcaseTileStateText();

        if (isAssignedToHousekeeper)
        {
            // HSK = Housekeeping，原型阶段先用短文字避免房间标签太挤。
            stateText += "\nHSK Cleaning";
        }

        if (isSelected)
        {
            stateText += "\nSELECTED";
        }

        return stateText;
    }
}

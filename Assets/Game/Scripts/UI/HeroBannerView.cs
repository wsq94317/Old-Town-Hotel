using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class HeroBannerView : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Image bannerImage;

    [Header("Tab → sprite map")]
    [SerializeField] private Sprite frontDeskSprite;
    [SerializeField] private Sprite roomsSprite;
    [SerializeField] private Sprite loungeSprite;

    [Header("Editor preview")]
    [SerializeField] private HotelTab previewTab = HotelTab.FrontDesk;

    public void SetTab(HotelTab tab)
    {
        if (bannerImage == null) return;
        bannerImage.sprite = SpriteFor(tab);
    }

    private Sprite SpriteFor(HotelTab tab)
    {
        switch (tab)
        {
            case HotelTab.FrontDesk: return frontDeskSprite;
            case HotelTab.Rooms:     return roomsSprite;
            case HotelTab.Lounge:    return loungeSprite;
            default:                 return frontDeskSprite;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        SetTab(previewTab);
    }
#endif
}

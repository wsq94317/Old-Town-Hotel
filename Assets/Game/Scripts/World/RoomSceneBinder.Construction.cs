using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class RoomSceneBinder
{
    private GameObject _constructionRoot;
    private RectTransform _constructionSign;
    private Image _constructionFill;
    private TMP_Text _constructionText;
    private Transform _workBeacon;
    private Material _constructionGold;
    private Material _constructionDark;

    private static string ConstructionWord(SimRenovationJob job) => GameText.UseChinese
        ? (job.isReclaim ? "修复中" : "装修中")
        : (job.isReclaim ? "RESTORING" : "RENOVATING");

    private void UpdateConstructionView(SimRenovationJob job)
    {
        if (job == null)
        {
            if (_constructionRoot != null) _constructionRoot.SetActive(false);
            return;
        }
        if (_constructionRoot == null) BuildConstructionView();
        _constructionRoot.SetActive(true);
        _constructionFill.rectTransform.anchorMax = new Vector2(job.Progress, 1f);
        int percent = Mathf.RoundToInt(job.Progress * 100f);
        _constructionText.text = roomNumber + "  " + ConstructionWord(job) + "\n"
            + percent + "%  " + (GameText.UseChinese ? "剩余 " + job.daysRemaining + " 天" : job.daysRemaining + " DAYS LEFT");
    }

    private void BuildConstructionView()
    {
        _constructionRoot = new GameObject("ConstructionFeedback");
        _constructionRoot.transform.SetParent(transform, false);
        float side = transform.localPosition.z >= 0f ? -1f : 1f;
        // Outside the door darkness mask. No colliders: movement and room taps stay usable.
        _constructionRoot.transform.localPosition = new Vector3(0f, 0f, side * 2.7f);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        _constructionGold = new Material(shader) { color = new Color(1f, .66f, .12f) };
        _constructionDark = new Material(shader) { color = new Color(.13f, .16f, .18f) };
        for (int i = 0; i < 10; i++)
        {
            var stripe = ConstructionPiece("BarrierStripe", new Vector3(-1.26f + i * .28f, .5f, 0f),
                new Vector3(.28f, .24f, .12f), i % 2 == 0 ? _constructionGold : _constructionDark);
            stripe.localRotation = Quaternion.Euler(0f, 0f, -12f);
        }
        for (int i = -1; i <= 1; i += 2)
            ConstructionPiece("BarrierLeg", new Vector3(i * 1.12f, .26f, 0f), new Vector3(.09f, .52f, .13f), _constructionDark);
        _workBeacon = ConstructionPiece("AnimatedWorkBeacon", new Vector3(1.32f, .82f, 0f),
            new Vector3(.18f, .18f, .18f), _constructionGold);

        var worksite = HotelCharacterArt.Joint(_constructionRoot.transform, "RenovationCrew", new Vector3(.65f, 0, -side * 1.5f));
        var crew = HotelCharacterArt.Create(worksite, StaffRole.Manager, false, roomNumber, true);
        worksite.localScale = Vector3.one * .88f;
        var gold = HotelVisualStyle.C("#D9A54C");
        var dark = HotelVisualStyle.C("#4D5D59");
        var tools = HotelCharacterArt.Joint(_constructionRoot.transform,"SiteEquipment",new Vector3(-.85f,0,-side*1.15f));
        HotelVisualStyle.Box(tools,"Toolbox",new Vector3(0,.17f,0),new Vector3(.60f,.32f,.35f),dark);
        HotelVisualStyle.Box(tools,"ToolboxHandle",new Vector3(0,.37f,0),new Vector3(.28f,.10f,.055f),gold);
        for(int s=-1;s<=1;s+=2)HotelVisualStyle.Box(tools,"LadderRail",new Vector3(.8f+s*.22f,.8f,.5f),new Vector3(.07f,1.65f,.07f),gold,new Vector3(12,0,0));
        for(int i=0;i<5;i++)HotelVisualStyle.Box(tools,"LadderStep",new Vector3(.8f,.22f+i*.29f,.61f-i*.06f),new Vector3(.5f,.055f,.075f),dark);
        var dust=HotelCharacterArt.Joint(worksite,"WorkDust",new Vector3(0,.05f,.45f));
        for(int i=0;i<4;i++)HotelVisualStyle.Shape(dust,"DustPuff",PrimitiveType.Sphere,new Vector3((i-1.5f)*.13f,0,0),Vector3.one*.12f,HotelVisualStyle.C("#E3D3B1"));
        dust.gameObject.AddComponent<HotelConstructionDust>();

        var sign = new GameObject("ConstructionProgress", typeof(RectTransform), typeof(Canvas));
        sign.transform.SetParent(_constructionRoot.transform, false);
        var canvas = sign.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        _constructionSign = (RectTransform)sign.transform;
        _constructionSign.sizeDelta = new Vector2(260f, 72f);
        _constructionSign.localScale = Vector3.one * .012f;
        _constructionSign.localPosition = new Vector3(0f, 3.4f, 0f);
        var back = ConstructionImage("Background", _constructionSign, new Color(.10f, .13f, .16f, .96f));
        Stretch(back.rectTransform, Vector2.zero, Vector2.one);
        var textObject = new GameObject("StatusAndRemainingDays", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(_constructionSign, false);
        _constructionText = textObject.GetComponent<TextMeshProUGUI>();
        _constructionText.font = WorldManagementHud.PresentationFont;
        _constructionText.fontSize = 23f;
        _constructionText.color = new Color(1f, .81f, .36f);
        _constructionText.alignment = TextAlignmentOptions.Center;
        _constructionText.raycastTarget = false;
        Stretch(_constructionText.rectTransform, new Vector2(0f, .21f), Vector2.one);
        var track = ConstructionImage("ProgressTrack", _constructionSign, new Color(.28f, .31f, .34f));
        Stretch(track.rectTransform, new Vector2(.06f, .08f), new Vector2(.94f, .17f));
        _constructionFill = ConstructionImage("CompletedWork", track.transform, new Color(1f, .66f, .12f));
        Stretch(_constructionFill.rectTransform, Vector2.zero, Vector2.one);
    }

    private Transform ConstructionPiece(string name, Vector3 position, Vector3 scale, Material material)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(_constructionRoot.transform, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        Destroy(part.GetComponent<Collider>());
        part.GetComponent<Renderer>().sharedMaterial = material;
        return part.transform;
    }

    private static Image ConstructionImage(string name, Transform parent, Color color)
    {
        var item = new GameObject(name, typeof(RectTransform), typeof(Image));
        item.transform.SetParent(parent, false);
        var image = item.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void TickConstructionAnimation()
    {
        if (_constructionRoot == null || !_constructionRoot.activeSelf) return;
        var camera = Camera.main;
        if (camera != null) _constructionSign.rotation = camera.transform.rotation;
        var bridge = HotelSimSceneBridge.Instance;
        if (bridge != null && (bridge.AwaitingMorningReport || bridge.IsSuspended)) return;
        _workBeacon.localRotation = Quaternion.Euler(0f, Time.time * 100f, 45f);
        _workBeacon.localScale = Vector3.one * (.18f + .035f * Mathf.Sin(Time.time * 5f));
    }

    private void DisposeConstructionView()
    {
        if (_constructionGold != null) Destroy(_constructionGold);
        if (_constructionDark != null) Destroy(_constructionDark);
    }
}

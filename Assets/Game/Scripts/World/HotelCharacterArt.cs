using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Articulated toy-like hotel cast. Navigation and picking stay on the actor root.</summary>
public sealed class HotelCharacterArt : MonoBehaviour
{
    public StaffRole role;
    public bool guest, construction;
    private Transform _body, _head, _leftArm, _rightArm, _leftLeg, _rightLeg, _eyes, _tool;
    private NavMeshAgent _nav;
    private StaffAgent _staff;
    private Vector3 _previous;
    private float _phase, _speed;
    public string MotionState { get; private set; } = "Idle";

    private void OnEnable()
    {
        _body=transform.Find("Body");_leftLeg=transform.Find("LeftLeg");_rightLeg=transform.Find("RightLeg");
        if(_body!=null){_head=_body.Find("Head");_leftArm=_body.Find("LeftArm");_rightArm=_body.Find("RightArm");}
        if(_head!=null)_eyes=_head.Find("Eyes");if(_rightArm!=null)_tool=_rightArm.Find("WorkTool");
    }

    public static HotelCharacterArt Create(Transform actor, StaffRole role, bool guest = false, int variation = 0, bool worker = false)
    {
        var old = actor.Find("Visual");
        if (old != null) { old.gameObject.SetActive(false); if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }
        if (Application.isPlaying && !guest && !worker) {
            var prefab=Resources.Load<GameObject>("HotelArt/Revival/Cast_"+role);
            var source=prefab!=null?prefab.transform.Find("Visual"):null;
            if(source!=null){var instance=Instantiate(source.gameObject,actor,false);instance.name="Visual";return instance.GetComponent<HotelCharacterArt>();}
        }
        var root = new GameObject("Visual"); root.transform.SetParent(actor, false);
        var art = root.AddComponent<HotelCharacterArt>();
        art.role = role; art.guest = guest; art.construction = worker; art.Build(variation);
        return art;
    }

    private void Start()
    {
        _nav = GetComponentInParent<NavMeshAgent>(); _staff = GetComponentInParent<StaffAgent>();
        _previous = transform.parent.position;
    }

    public void Build(int variation = 0)
    {
        Color skin = variation % 3 == 1 ? HotelVisualStyle.C("#BB805A") : HotelVisualStyle.C("#EFC29C");
        Color hair = variation % 3 == 2 ? HotelVisualStyle.C("#A45F2D") : HotelVisualStyle.C("#332B28");
        Color coat = guest ? (variation % 2 == 0 ? HotelVisualStyle.C("#C7803C") : HotelVisualStyle.C("#557C93"))
            : role == StaffRole.Housekeeper ? HotelVisualStyle.C("#548C97")
            : role == StaffRole.Inspector ? HotelVisualStyle.C("#665B83")
            : construction ? HotelVisualStyle.C("#D49835")
            : (guest && variation % 4 == 1) ? HotelVisualStyle.C("#B84F43") : HotelVisualStyle.C("#28655B");
        Color white = HotelVisualStyle.C("#FFF0D4"), dark = HotelVisualStyle.C("#30383B"), gold = HotelVisualStyle.C("#E9B85F");
        _body = Joint(transform, "Body", new Vector3(0, .63f, 0));
        Ball(_body, "Jacket", new Vector3(0, .28f, 0), new Vector3(.57f, .69f, .36f), coat);
        HotelVisualStyle.Box(_body, "Shirt", new Vector3(0, .35f, .177f), new Vector3(.22f, .47f, .035f), white);
        for (int s = -1; s <= 1; s += 2)
            HotelVisualStyle.Box(_body, "Lapel", new Vector3(s * .13f, .46f, .20f), new Vector3(.11f, .29f, .045f), coat, new Vector3(0, 0, s * 19));
        HotelVisualStyle.Box(_body, "Tie", new Vector3(0, .40f, .21f), new Vector3(.065f, .28f, .035f), guest ? coat : gold);
        Ball(_body, "Badge", new Vector3(-.18f, .34f, .18f), new Vector3(.09f, .065f, .025f), gold);
        _head = Joint(_body, "Head", new Vector3(0, .94f, 0));
        Ball(_head, "Face", Vector3.zero, new Vector3(.71f, .73f, .64f), skin);
        Ball(_head, "HairCap", new Vector3(0, .16f, -.045f), new Vector3(.75f, .50f, .66f), hair);
        for (int i = 0; i < 5; i++) Ball(_head, "Fringe", new Vector3(-.25f + .125f * i, .20f - .028f * i, .245f), new Vector3(.20f, .23f, .13f), hair);
        for (int s = -1; s <= 1; s += 2) {
            Ball(_head, "Ear", new Vector3(s * .35f, -.04f, 0), new Vector3(.12f, .19f, .13f), skin);
            Ball(_head, "Cheek", new Vector3(s * .22f, -.14f, .25f), new Vector3(.14f, .065f, .025f), HotelVisualStyle.C("#E99F86"));
        }
        _eyes = Joint(_head, "Eyes", new Vector3(0, -.035f, .285f));
        for (int s = -1; s <= 1; s += 2) {
            Ball(_eyes, "Eye", new Vector3(s * .135f, 0, 0), new Vector3(.105f, .145f, .065f), dark);
            Ball(_eyes, "EyeLight", new Vector3(s * .135f - .015f, .034f, .031f), new Vector3(.035f, .045f, .015f), white);
            HotelVisualStyle.Box(_head, "Brow", new Vector3(s * .135f, .085f, .29f), new Vector3(.12f, .028f, .025f), hair, new Vector3(0, 0, s * -8));
        }
        Ball(_head, "Nose", new Vector3(0, -.12f, .31f), new Vector3(.085f, .09f, .075f), skin);
        Ball(_head, "Smile", new Vector3(0, -.22f, .267f), new Vector3(.10f, .035f, .025f), HotelVisualStyle.C("#9F6251"));
        if (!guest && ((guest && variation % 4 == 1) || construction)) {
            Ball(_head, "UniformCap", new Vector3(0, .36f, 0), new Vector3(.77f, .22f, .68f), coat);
            HotelVisualStyle.Box(_head, "CapBand", new Vector3(0, .29f, .29f), new Vector3(.55f, .055f, .12f), gold);
        }
        if (role == StaffRole.Housekeeper && !guest) {
            Ball(_head, "HairBun", new Vector3(0, .23f, -.31f), Vector3.one * .28f, hair);
            HotelVisualStyle.Box(_body, "Apron", new Vector3(0, .02f, .19f), new Vector3(.39f, .40f, .035f), white);
        }
        _leftArm = Arm(_body, -1, coat, skin); _rightArm = Arm(_body, 1, coat, skin);
        _leftLeg = Leg(transform, -1, dark); _rightLeg = Leg(transform, 1, dark);
        _tool = Joint(_rightArm, "WorkTool", new Vector3(0, -.48f, .1f));
        if (guest) {
            HotelVisualStyle.Box(_tool, "Suitcase", new Vector3(.15f, -.38f, -.1f), new Vector3(.36f, .51f, .24f), coat);
            HotelVisualStyle.Box(_tool, "Handle", new Vector3(.15f, -.08f, -.1f), new Vector3(.24f, .12f, .05f), gold);
            for (int s=-1;s<=1;s+=2) Ball(_tool,"Wheel",new Vector3(.15f+s*.13f,-.66f,-.1f),Vector3.one*.085f,dark);
        } else if (role == StaffRole.Housekeeper) {
            HotelVisualStyle.Box(_tool, "MopHandle", new Vector3(0, -.2f, .12f), new Vector3(.045f, 1.05f, .045f), gold);
            Ball(_tool, "MopHead", new Vector3(0, -.73f, .12f), new Vector3(.40f, .10f, .25f), white);
        } else if (construction) {
            HotelVisualStyle.Box(_tool, "HammerHandle", new Vector3(0, -.08f, 0), new Vector3(.055f, .35f, .055f), gold);
            HotelVisualStyle.Box(_tool, "HammerHead", new Vector3(0, .10f, 0), new Vector3(.27f, .11f, .12f), dark);
        } else if (role == StaffRole.Inspector) {
            HotelVisualStyle.Box(_tool, "Clipboard", new Vector3(0, 0, .07f), new Vector3(.25f, .34f, .045f), gold);
            HotelVisualStyle.Box(_tool, "InspectionSheet", new Vector3(0, 0, .10f), new Vector3(.20f, .27f, .015f), white);
            for(int i=0;i<3;i++) HotelVisualStyle.Box(_tool,"Notes",new Vector3(0,.08f-i*.065f,.112f),new Vector3(.14f,.013f,.008f),coat);
        } else Ball(_tool, "Keyring", new Vector3(0, -.06f, 0), new Vector3(.12f, .12f, .04f), gold);
    }

    private static Transform Arm(Transform p, int side, Color coat, Color skin)
    {
        var j = Joint(p, side < 0 ? "LeftArm" : "RightArm", new Vector3(side * .31f, .49f, 0));
        Ball(j, "Sleeve", new Vector3(0, -.20f, 0), new Vector3(.20f, .44f, .22f), coat);
        Ball(j, "Cuff", new Vector3(0, -.39f, 0), new Vector3(.17f, .09f, .19f), HotelVisualStyle.C("#FFF0D4"));
        Ball(j, "Hand", new Vector3(0, -.47f, 0), new Vector3(.18f, .18f, .17f), skin); return j;
    }
    private static Transform Leg(Transform p, int side, Color dark)
    {
        var j = Joint(p, side < 0 ? "LeftLeg" : "RightLeg", new Vector3(side * .15f, .65f, 0));
        Ball(j, "Trouser", new Vector3(0, -.23f, 0), new Vector3(.23f, .48f, .24f), dark);
        Ball(j, "Shoe", new Vector3(0, -.52f, .065f), new Vector3(.27f, .18f, .39f), HotelVisualStyle.C("#29282C")); return j;
    }
    public static Transform Joint(Transform p, string name, Vector3 position) { var go = new GameObject(name); go.layer=2;go.transform.SetParent(p,false);go.transform.localPosition=position;return go.transform; }
    private static void Ball(Transform p,string n,Vector3 pos,Vector3 scale,Color c) => HotelVisualStyle.Shape(p,n,PrimitiveType.Sphere,pos,scale,c);

    private void LateUpdate()
    {
        if (_body == null) return;
        float dt = Time.deltaTime;
        if (dt <= 0 || Time.timeScale <= 0) return;
        Vector3 delta = transform.parent.position - _previous; _previous = transform.parent.position;
        float measured = new Vector2(delta.x, delta.z).magnitude / dt;
        _speed = Mathf.Lerp(_speed, Mathf.Min(measured, 5), dt * 12);
        bool walking = _speed > .12f;
        bool working = construction || (_staff != null && (_staff.State == StaffAgent.AgentState.Working || _staff.ProvidesFrontDeskCoverage));
        MotionState = walking ? "Walk" : working ? "Work" : "Idle";
        _phase += dt * (walking ? 10f : working ? 5f : 1.8f);
        float swing = walking ? Mathf.Sin(_phase) * Mathf.Min(_speed / 2, 1) * 32 : 0;
        _leftLeg.localRotation = Quaternion.Euler(swing, 0, 0); _rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
        _leftArm.localRotation = Quaternion.Euler(-swing * .7f, 0, 5);
        _rightArm.localRotation = Quaternion.Euler(swing * .7f, 0, -5);
        _body.localPosition = new Vector3(0, .63f + (walking ? Mathf.Abs(Mathf.Sin(_phase))*.04f : Mathf.Sin(_phase)*.008f), 0);
        _body.localRotation = Quaternion.Euler(walking ? 5 : 0, 0, walking ? Mathf.Sin(_phase)*2 : 0);
        if (working && !walking) {
            float stroke = Mathf.Sin(_phase);
            _rightArm.localRotation = Quaternion.Euler(construction ? -65+stroke*45 : -30+stroke*25, stroke*12, -8);
            _leftArm.localRotation = Quaternion.Euler(-22, 0, 12);
            _head.localRotation = Quaternion.Euler(8+stroke*3, stroke*7, 0);
        } else _head.localRotation=Quaternion.Euler(Mathf.Sin(_phase*.55f)*2,Mathf.Sin(_phase*.3f)*7,0);
        float blink = Mathf.Repeat(Time.time + GetInstanceID()*.137f, 4.6f);
        _eyes.localScale = new Vector3(1, blink > 4.45f ? .1f : 1, 1);
        if (!walking && Camera.main != null) {
            Vector3 toCamera = Camera.main.transform.position - transform.position; toCamera.y=0;
            if(toCamera.sqrMagnitude>.01f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toCamera),dt*3);
        } else if(walking) transform.localRotation=Quaternion.Slerp(transform.localRotation,Quaternion.identity,dt*8);
    }
}

public static class HotelVisualStyle
{
    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    public static Color C(string hex) { ColorUtility.TryParseHtmlString(hex,out Color c); return c; }
    public static Material Mat(Color color)
    {
        string key=ColorUtility.ToHtmlStringRGB(color);
        if(Materials.TryGetValue(key,out var found)&&found!=null)return found;
        var mat=Resources.Load<Material>("HotelArt/Revival/Mat_"+key);
        if(mat==null) { mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Revival_"+key,color=color};mat.SetFloat("_Smoothness",.25f); }
        Materials[key]=mat;return mat;
    }
    public static GameObject Box(Transform p,string n,Vector3 pos,Vector3 size,Color c,Vector3 euler=default) => Shape(p,n,PrimitiveType.Cube,pos,size,c,euler);
    public static GameObject Shape(Transform p,string n,PrimitiveType primitive,Vector3 pos,Vector3 size,Color c,Vector3 euler=default)
    {
        var go=GameObject.CreatePrimitive(primitive);go.name=n;go.layer=2;go.transform.SetParent(p,false);go.transform.localPosition=pos;go.transform.localScale=size;go.transform.localEulerAngles=euler;
        var col=go.GetComponent<Collider>();if(Application.isPlaying)Object.Destroy(col);else Object.DestroyImmediate(col);
        var mesh=HotelCraftMesh.For(n,size,primitive);
        if(mesh!=null){go.GetComponent<MeshFilter>().sharedMesh=mesh;go.transform.localScale=Vector3.one;}
        go.GetComponent<Renderer>().sharedMaterial=HotelSurfaceLibrary.For(n,c);return go;
    }
}

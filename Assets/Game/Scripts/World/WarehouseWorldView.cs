using TMPro;
using UnityEngine;

public sealed class WarehouseWorldView : MonoBehaviour
{
    private GameObject[] _shelves = new GameObject[4], _crates = new GameObject[12];
    private TMP_Text _label;
    private int _stock = -1, _capacity = -1;

    public static void Install()
    {
        var world = GameObject.Find("World"); var floor = world != null ? world.transform.Find("Floor1") : null;
        if (floor == null || floor.Find("MaterialsWarehouse") != null) return;
        var root = new GameObject("MaterialsWarehouse"); root.transform.SetParent(floor,false);
        root.transform.localPosition = new Vector3(-35f,0,-16f); root.AddComponent<WarehouseWorldView>();
    }

    private void Awake()
    {
        for (int side=-1;side<=1;side+=2)
            Piece("RackPost",new Vector3(side*1.2f,1.35f,.15f),new Vector3(.1f,2.7f,.8f),HotelArtMaterial.MetalDark);
        for (int i=0;i<4;i++) {
            _shelves[i]=Piece("Shelf_"+i,new Vector3(0,.12f+i*.65f,.15f),new Vector3(2.5f,.1f,.85f),HotelArtMaterial.WoodWarm);
            for (int j=0;j<3;j++)
                _crates[i*3+j]=Piece("MaterialCrate",new Vector3((j-1)*.73f,.4f+i*.65f,.15f),new Vector3(.6f,.46f,.58f),HotelArtMaterial.FabricMustard);
        }
        var text = new GameObject("WarehouseCapacity");text.transform.SetParent(transform,false);
        text.transform.localPosition = new Vector3(0,2.85f,-.1f);
        _label=text.AddComponent<TextMeshPro>(); _label.font=WorldManagementHud.PresentationFont;
        _label.fontSize=3;_label.alignment=TextAlignmentOptions.Center;
        _label.rectTransform.sizeDelta=new Vector2(3.5f,1);
        _label.color=new Color(1,.85f,.5f);
    }

    private void LateUpdate()
    {
        var bridge=HotelSimSceneBridge.Instance;if(bridge==null || bridge.Sim==null)return;
        if(Camera.main!=null)_label.transform.rotation=Camera.main.transform.rotation;
        int stock=bridge.Sim.Materials.Stock, capacity=bridge.Sim.Warehouse.Capacity;
        if(stock==_stock && capacity==_capacity)return;
        _stock=stock;_capacity=capacity;
        int levels=capacity<=40 && capacity>0 ? 1 : capacity<=80 && capacity>0 ? 2 : capacity<=160 && capacity>0 ? 3 : 4;
        for(int i=0;i<4;i++)_shelves[i].SetActive(i<levels);
        for(int i=0;i<12;i++)_crates[i].SetActive(i<Mathf.Min(levels*3,Mathf.CeilToInt(stock/10f)));
        _label.text=(GameText.UseChinese?"材料仓库":"MATERIALS")+"\n"+stock+" / "+(capacity>0?capacity.ToString():"--");
    }

    private GameObject Piece(string name,Vector3 position,Vector3 scale,HotelArtMaterial material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(transform,false);
        go.transform.localPosition=position;go.transform.localScale=scale;
        Destroy(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=LowPolyHotelKit.MaterialFor(material);
        return go;
    }
}

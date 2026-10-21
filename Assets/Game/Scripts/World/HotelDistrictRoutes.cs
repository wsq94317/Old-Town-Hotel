using UnityEngine;
using UnityEngine.UI;

/// <summary>Dashed ownership routes over the illustrated map, in the same normalized coordinates as its pins.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class HotelDistrictRoutes : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var bounds=rectTransform.rect;
        foreach(var site in HotelDistrictCatalog.Sites) foreach(string prerequisite in site.Prerequisites) {
            var prior=HotelDistrictCatalog.Find(prerequisite); if(prior==null) continue;
            Vector2 a=bounds.min+Vector2.Scale(prior.MapPosition,bounds.size);
            Vector2 b=bounds.min+Vector2.Scale(site.MapPosition,bounds.size);
            float length=Vector2.Distance(a,b); if(length<1) continue;
            var normal=new Vector2(-(b-a).y,(b-a).x).normalized*1.35f;
            bool owned=HotelEmpireProgress.IsOwned(site.Id), available=HotelEmpireProgress.IsUnlocked(site);
            Color tint=owned ? new Color(.18f,.54f,.43f,.9f) : available ? new Color(1,.84f,.40f,.95f) : new Color(.94f,.91f,.77f,.5f);
            for(float d=26;d<length-26;d+=11) {
                Vector2 from=Vector2.Lerp(a,b,d/length), to=Vector2.Lerp(a,b,Mathf.Min(d+6,length-26)/length);
                int index=vh.currentVertCount;
                vh.AddVert(from-normal,tint,Vector2.zero); vh.AddVert(from+normal,tint,Vector2.zero);
                vh.AddVert(to+normal,tint,Vector2.zero); vh.AddVert(to-normal,tint,Vector2.zero);
                vh.AddTriangle(index,index+1,index+2); vh.AddTriangle(index,index+2,index+3);
            }
        }
    }
}

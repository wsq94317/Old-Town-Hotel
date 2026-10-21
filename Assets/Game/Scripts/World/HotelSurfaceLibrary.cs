using System.Collections.Generic;
using UnityEngine;

public static class HotelSurfaceLibrary
{
    private static readonly Dictionary<string,Material> Cache=new Dictionary<string,Material>();
    public static string SurfaceFor(string name)
    {
        string n=name.ToLowerInvariant();
        if(n.Contains("glass")||n.Contains("screen"))return "Glass";
        if(n.Contains("brass")||n.Contains("gold")||n.Contains("pull")||n.Contains("handle")||n.Contains("bell")||n.Contains("rod")||n.Contains("rail")||n.Contains("stem")||n.Contains("key"))return "Brass";
        if(n.Contains("shade")||n.Contains("bulb"))return "Glow";
        if(n.Contains("fabric")||n.Contains("pillow")||n.Contains("duvet")||n.Contains("curtain")||n.Contains("upholster")||n.Contains("cushion")||n.Contains("softback")||n.Contains("rolledarm")||n.Contains("rug")||n.Contains("carpet")||n.Contains("jacket")||n.Contains("sleeve")||n.Contains("apron")||n.Contains("trouser")||n.Contains("runner"))return "Linen";
        if(n.Contains("wall")||n.Contains("plaster"))return n.Contains("panel")||n.Contains("skirt")?"Walnut":"Plaster";
        if(n.Contains("tile")||n.Contains("marble")||n.Contains("countertop")||n.Contains("basin")||n.Contains("porcelain"))return "Stone";
        if(n.Contains("wood")||n.Contains("oak")||n.Contains("frame")||n.Contains("drawer")||n.Contains("cabinet")||n.Contains("plank")||n.Contains("desk")||n.Contains("panel")||n.Contains("headboard")||n.Contains("leg")||n.Contains("foot")||n.Contains("counterfront")||n.Contains("cornice"))return "Walnut";
        return "Plain";
    }
    public static Material For(string part,Color color)
    {
        string surface=SurfaceFor(part),key="Craft_"+surface+"_"+ColorUtility.ToHtmlStringRGB(color);
        if(Cache.TryGetValue(key,out var found)&&found!=null)return found;
        var mat=Resources.Load<Material>("HotelArt/Surfaces/"+key);
        if(mat==null){
            mat=new Material(Shader.Find("OldTownHotel/Crafted Surface")){name=key,color=color};
            string image=surface=="Walnut"?"Walnut":surface=="Linen"?"Linen":surface=="Plaster"||surface=="Stone"?"Plaster":null;
            if(image!=null){mat.SetTexture("_BaseMap",Resources.Load<Texture2D>("HotelArt/Surfaces/"+image));mat.SetVector("_TextureBalance",image=="Walnut"?new Vector4(2.6f,3.7f,5,0):new Vector4(1.25f,1.3f,1.4f,0));}
            mat.SetFloat("_TextureStrength",image==null?0:surface=="Walnut"?.85f:.7f);
            mat.SetFloat("_TextureScale",surface=="Linen"?3:surface=="Walnut"?.8f:1.1f);
            mat.SetFloat("_Relief",surface=="Linen"?.024f:surface=="Plaster"?.015f:.009f);
            mat.SetFloat("_Smoothness",surface=="Brass"?.72f:surface=="Stone"?.62f:surface=="Walnut"?.4f:surface=="Glass"?.85f:surface=="Plain"?.28f:.18f);
            mat.SetFloat("_Metallic",surface=="Brass"?.75f:0);
            if(surface=="Glow")mat.SetColor("_EmissionColor",new Color(1,.58f,.18f)*1.8f);
            if(surface=="Glass")mat.SetColor("_EmissionColor",new Color(.16f,.25f,.28f)*.4f);
            mat.enableInstancing=true;
        }
        Cache[key]=mat;return mat;
    }
}

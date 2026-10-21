using System.Collections.Generic;
using UnityEngine;

/// <summary>Small reusable manufactured shapes with actual curved profiles and edge normals.</summary>
public static class HotelCraftMesh
{
    private static readonly Dictionary<string,Mesh> Cache=new Dictionary<string,Mesh>();
    public static Mesh For(string name,Vector3 size,PrimitiveType primitive)
    {
        bool curtain=name=="Curtain",shade=name=="WarmShade",leaf=name.StartsWith("Leaf"),cushion=name.Contains("Pillow")||name.Contains("Cushion");
        if(!curtain&&!shade&&!leaf&&!cushion&&primitive!=PrimitiveType.Cube)return null;
        if(!curtain&&!shade&&!cushion&&Mathf.Min(size.x,size.y,size.z)<.025f)return null;
        string key=(curtain?"Pleated":shade?"Shade":leaf?"Leaf":"Rounded")+size.ToString("F4");
        if(Cache.TryGetValue(key,out var cached)&&cached!=null)return cached;
        Mesh mesh=curtain?Curtain(size):shade?Shade(size):leaf?Leaf(size):RoundedBox(size,cushion?.46f:.22f);mesh.name=key;Cache[key]=mesh;return mesh;
    }
    private static Mesh Leaf(Vector3 size)
    {
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();const int segments=12;
        for(int side=0;side<2;side++) {
            int start=vertices.Count;
            for(int y=0;y<=segments;y++)for(int x=0;x<5;x++) {
                float t=y/(float)segments,u=(x-2)/2f,spread=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.8f);
                vertices.Add(new Vector3(u*size.x*.5f*spread,Mathf.Sin(t*Mathf.PI)*size.y*(.45f-.55f*Mathf.Abs(u))+.06f*size.y*t,(t-.5f)*size.z));uv.Add(new Vector2(x/4f,t));
            }
            for(int y=0;y<segments;y++)for(int x=0;x<4;x++){int a=start+y*5+x,b=a+5;if(side==0)triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});else triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
        }
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
    }
    private static Mesh RoundedBox(Vector3 size,float rounding)
    {
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
        Vector3 half=size*.5f;float radius=Mathf.Min(.065f,Mathf.Min(size.x,size.y,size.z)*rounding);Vector3 inner=half-Vector3.one*radius;
        Vector3[] face={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
        Vector3[] horizontal={Vector3.back,Vector3.forward,Vector3.right,Vector3.right,Vector3.right,Vector3.left};
        Vector3[] vertical={Vector3.up,Vector3.up,Vector3.back,Vector3.forward,Vector3.up,Vector3.up};
        for(int f=0;f<6;f++){
            Vector3 n=face[f],u=horizontal[f],v=vertical[f];float hu=Vector3.Scale(u,half).magnitude,hv=Vector3.Scale(v,half).magnitude,hn=Vector3.Scale(n,half).magnitude;
            float[] us={-hu,-hu+radius*.3f,-hu+radius,hu-radius,hu-radius*.3f,hu};float[] vs={-hv,-hv+radius*.3f,-hv+radius,hv-radius,hv-radius*.3f,hv};int start=vertices.Count;
            for(int y=0;y<6;y++)for(int x=0;x<6;x++){
                Vector3 p=n*hn+u*us[x]+v*vs[y],c=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));Vector3 normal=(p-c).normalized;
                vertices.Add(c+normal*radius);normals.Add(normal);uv.Add(new Vector2(us[x]/size.magnitude+.5f,vs[y]/size.magnitude+.5f));
            }
            for(int y=0;y<5;y++)for(int x=0;x<5;x++){int a=start+y*6+x;triangles.AddRange(new[]{a,a+1,a+7,a,a+7,a+6});}
        }
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
    }
    private static Mesh Curtain(Vector3 size)
    {
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();const int columns=24,rows=10;
        for(int side=0;side<2;side++){
            int start=vertices.Count;
            for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++){
                float fx=x/(float)columns,fy=y/(float)rows;float gather=1-.24f*Mathf.Exp(-Mathf.Pow((fy-.4f)*5,2));
                vertices.Add(new Vector3((fx-.5f)*size.x*gather,(fy-.5f)*size.y+Mathf.Cos(fx*Mathf.PI*6)*.012f*(1-fy),Mathf.Cos(fx*Mathf.PI*8)*size.z*.46f+side*.009f));uv.Add(new Vector2(fx,fy));
            }
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++){int a=start+y*(columns+1)+x,b=a+columns+1;if(side==0)triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});else triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
        }
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
    }
    private static Mesh Shade(Vector3 size)
    {
        var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();const int count=32;
        for(int ring=0;ring<4;ring++)for(int i=0;i<=count;i++){
            float angle=i*Mathf.PI*2/count;float height=ring==0?-.5f:ring==1?-.45f:ring==2?.45f:.5f;float radius=ring<2?.5f:.30f;
            vertices.Add(new Vector3(Mathf.Cos(angle)*size.x*radius,height*size.y,Mathf.Sin(angle)*size.z*radius));uv.Add(new Vector2(i/(float)count,ring/3f));
        }
        for(int r=0;r<3;r++)for(int i=0;i<count;i++){int a=r*(count+1)+i,b=a+count+1;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
        var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
    }
}

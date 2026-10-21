using UnityEngine;

public sealed class HotelConstructionDust : MonoBehaviour
{
    private void Update()
    {
        for(int i=0;i<transform.childCount;i++){
            float phase=Mathf.Repeat(Time.time*1.1f+i*.24f,1);
            var p=transform.GetChild(i);p.localPosition=new Vector3((i-1.5f)*.14f+phase*.10f,phase*.45f,0);
            p.localScale=Vector3.one*(.05f+Mathf.Sin(phase*Mathf.PI)*.15f);
        }
    }
}

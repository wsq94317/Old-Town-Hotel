using UnityEngine;

/// <summary>Unscaled feedback keeps placement responsive while the studio pauses the hotel.</summary>
public sealed class RoomDesignObjectMotion:MonoBehaviour
{
    private float _time;
    private void OnEnable(){_time=0;transform.localScale=Vector3.one*.82f;}
    private void Update(){_time+=Time.unscaledDeltaTime;float t=Mathf.Clamp01(_time/.24f);transform.localScale=Vector3.one*(1-Mathf.Pow(1-t,3)*.18f);if(t>=1)enabled=false;}
}

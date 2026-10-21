using UnityEngine;

public sealed class HotelRoomAtmosphere : MonoBehaviour
{
    private Transform _ruins;private int _number;private float _tick;
    private void OnEnable(){_ruins=transform.Find("RevivalRuins");_number=RoomSceneBinder.ParseRoomNumber(name);_tick=0;}
    private void Update()
    {
        _tick-=Time.unscaledDeltaTime;if(_tick>0)return;_tick=.5f;
        var sim=HotelSimSceneBridge.Instance?.Sim;if(sim==null||_ruins==null)return;
        _number=GetComponent<RoomSceneBinder>().RoomNumber;
        if(!sim.Rooms.Contains(_number))return;
        _ruins.gameObject.SetActive(sim.Rooms.At(_number).state==RoomSimState.Ruined);
    }
}

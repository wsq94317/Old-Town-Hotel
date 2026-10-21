using UnityEngine;

/// <summary>Orbit, pan and zoom around a chosen floor; following the manager is an explicit option.</summary>
[DefaultExecutionOrder(100)]
public class ManagerCameraRig : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private FloorVisibilityController floors;
    [SerializeField] private float followLerp=8f;
    [SerializeField] private Vector3 cameraOffset=new Vector3(-8,10,-8);
    [SerializeField] private Vector2 worldMinXZ=new Vector2(-10,-6), worldMaxXZ=new Vector2(10,6);
    private Camera _cam;
    private Vector3 _focusPoint;
    private bool _focusInitialized, _followingTarget, _dragging, _snapNextFrame;
    private float _yaw=45, _pitch=50, _renderYaw=45, _renderPitch=50, _zoom=7.5f;
    public bool IsFollowingTarget=>_followingTarget;
    public Vector3 FocusPoint=>_focusPoint;
    public int ViewedFloor=>floors!=null ? floors.CurrentFloor : FloorMath.FloorIndexForY(_focusPoint.y);
    public float Yaw=>_yaw;
    public float Pitch=>_pitch;
    public float Zoom=>_zoom;
    public const float MinimumZoom=3.5f, MaximumZoom=24f;
    private void Awake()
    {
        _cam=GetComponent<Camera>();
        if(floors==null) floors=FindFirstObjectByType<FloorVisibilityController>();
        if(floors!=null) floors.OnFloorChanged+=HandleFloorChanged;
        if(Application.isPlaying && GetComponent<HotelAdaptiveCutaway>()==null) gameObject.AddComponent<HotelAdaptiveCutaway>();
    }
    private void Start() { EnsureFocusInitialized(); _snapNextFrame=true; }
    private void OnDestroy() { if(floors!=null) floors.OnFloorChanged-=HandleFloorChanged; }
    public void SetWorldBounds(Vector2 minXZ,Vector2 maxXZ)
    { worldMinXZ=Vector2.Min(minXZ,maxXZ);worldMaxXZ=Vector2.Max(minXZ,maxXZ);if(_focusInitialized)_focusPoint=ClampFocus(_focusPoint); }
    private void HandleFloorChanged(int floor)
    {
        EnsureFocusInitialized();
        if(_followingTarget && target!=null) _focusPoint=ClampFocus(target.position);
        else { _focusPoint.y=FloorMath.BaseYFor(floor);_focusPoint=ClampFocus(_focusPoint); }
        _snapNextFrame=true;
    }
    public void BeginFreeLook() { EnsureFocusInitialized();_followingTarget=false;_dragging=true; }
    public void EndFreeLook() { _dragging=false; }
    public void ApplyScreenDrag(Vector2 from,Vector2 to)
    {
        if(_cam==null)_cam=GetComponent<Camera>();if(_cam==null)return;
        EnsureFocusInitialized();
        var plane=new Plane(Vector3.up,new Vector3(0,_focusPoint.y,0));
        var a=_cam.ScreenPointToRay(from);var b=_cam.ScreenPointToRay(to);
        if(plane.Raycast(a,out float da)&&plane.Raycast(b,out float db)) {
            var delta=a.GetPoint(da)-b.GetPoint(db);delta.y=0;
            _focusPoint=ClampFocus(_focusPoint+delta);
        }
    }
    public void OrbitBy(float yawDegrees,float pitchDegrees=0)
    {
        EnsureFocusInitialized();_followingTarget=false;
        if(float.IsNaN(yawDegrees)||float.IsInfinity(yawDegrees)||float.IsNaN(pitchDegrees)||float.IsInfinity(pitchDegrees))return;
        _yaw=Mathf.Repeat(_yaw+yawDegrees,360);_pitch=Mathf.Clamp(_pitch+pitchDegrees,28,82);
    }
    public void ZoomBy(float amount)
    {
        if(float.IsNaN(amount)||float.IsInfinity(amount))return;
        SetZoom(_zoom+amount);
    }
    public void SetZoom(float value)
    {
        if(float.IsNaN(value)||float.IsInfinity(value))return;
        _zoom=Mathf.Clamp(value,MinimumZoom,MaximumZoom);
        if(_cam==null)_cam=GetComponent<Camera>();if(_cam!=null)_cam.orthographicSize=_zoom;
    }
    public void PanBy(Vector2 direction,float seconds)
    {
        BeginFreeLook();
        var right=Quaternion.Euler(0,_yaw,0)*Vector3.right;
        var forward=Quaternion.Euler(0,_yaw,0)*Vector3.forward;
        _focusPoint=ClampFocus(_focusPoint+(right*direction.x+forward*direction.y)*(_zoom*.9f*seconds));EndFreeLook();
    }
    public void SetOverhead(bool overhead) { _pitch=overhead ? 80 : 50;_followingTarget=false; }
    public void ResetView() { _yaw=45;_pitch=50;SetZoom(7.5f);_followingTarget=false; }
    public void ViewFloor(int floor)
    {
        EnsureFocusInitialized();_followingTarget=false;_dragging=false;
        if(floors!=null) floors.ShowFloor(floor);
        _focusPoint.y=FloorMath.BaseYFor(floor);_focusPoint=ClampFocus(_focusPoint);_snapNextFrame=true;
    }
    public void ReturnToTarget(bool instant=false)
    {
        if(target==null)return;
        _followingTarget=false;ShowFloorForPoint(target.position);
        _focusPoint=ClampFocus(target.position);_focusInitialized=true;_followingTarget=true;_dragging=false;_snapNextFrame=instant;
    }
    public void FocusOnPoint(Vector3 point,bool instant=false)
    {
        EnsureFocusInitialized();_followingTarget=false;ShowFloorForPoint(point);
        _focusPoint=ClampFocus(point);_focusInitialized=true;_dragging=false;_snapNextFrame=instant;
    }
    private void ShowFloorForPoint(Vector3 point)
    {
        if(floors==null)floors=FindFirstObjectByType<FloorVisibilityController>();
        if(floors!=null)floors.ShowFloor(FloorMath.FloorIndexForY(point.y));
    }
    public void CaptureView(HotelPresentationState state)
    {
        if(state==null)return;
        state.hasCameraView=true;state.cameraX=_focusPoint.x;state.cameraY=_focusPoint.y;state.cameraZ=_focusPoint.z;
        state.cameraYaw=_yaw;state.cameraPitch=_pitch;state.cameraZoom=_zoom;
    }
    public void RestoreView(HotelPresentationState state)
    {
        if(state==null||!state.hasCameraView) { BeginFreeLook();EndFreeLook();return; }
        if(float.IsNaN(state.cameraX)||float.IsInfinity(state.cameraX)||float.IsNaN(state.cameraZ)||float.IsInfinity(state.cameraZ))return;
        _yaw=Mathf.Repeat(state.cameraYaw,360);_pitch=Mathf.Clamp(state.cameraPitch,28,82);SetZoom(state.cameraZoom);
        FocusOnPoint(new Vector3(state.cameraX,state.cameraY,state.cameraZ),true);
        if(state.viewControlMode==1) ReturnToTarget(true);
    }
    private void LateUpdate()
    {
        EnsureFocusInitialized();if(!_focusInitialized)return;
        if(_followingTarget&&target!=null)_focusPoint=ClampFocus(target.position);
        float blend=1-Mathf.Exp(-followLerp*Time.unscaledDeltaTime);
        _renderYaw=_snapNextFrame ? _yaw : Mathf.LerpAngle(_renderYaw,_yaw,blend*1.6f);
        _renderPitch=_snapNextFrame ? _pitch : Mathf.Lerp(_renderPitch,_pitch,blend*1.6f);
        var rotation=Quaternion.Euler(_renderPitch,_renderYaw,0);
        cameraOffset=rotation*(Vector3.back*Mathf.Max(20,_zoom*2.5f));
        var desired=_focusPoint+cameraOffset;
        transform.position=_snapNextFrame||_dragging ? desired : Vector3.Lerp(transform.position,desired,blend);
        transform.rotation=rotation;
        if(_cam!=null){_cam.orthographic=true;_cam.orthographicSize=_zoom;_cam.farClipPlane=Mathf.Max(_cam.farClipPlane,400);}
        _snapNextFrame=false;
    }
    private void EnsureFocusInitialized()
    {
        if(_focusInitialized)return;
        _focusPoint=target!=null ? ClampFocus(target.position) : ClampFocus(transform.position-cameraOffset);_focusInitialized=true;
    }
    private Vector3 ClampFocus(Vector3 focus)
    {
        bool guest=ViewedFloor==1||ViewedFloor==2;
        focus.x=Mathf.Clamp(focus.x,guest ? worldMinXZ.x : Mathf.Max(-11,worldMinXZ.x),guest ? worldMaxXZ.x : Mathf.Min(11,worldMaxXZ.x));
        focus.z=Mathf.Clamp(focus.z,worldMinXZ.y,worldMaxXZ.y);return focus;
    }
}
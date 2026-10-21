using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public partial class WorldInputController
{
    private bool _viewRestored, _cameraGesture, _suppressUntilRelease, _orbitMouse;
    private Vector2 _touchA, _touchB, _orbitPosition;
    public bool IsDirectorMode=>HotelSessionState.Current.viewControlMode==0;
    public void SetDirectorMode(bool director)
    {
        HotelSessionState.Current.viewControlMode=director ? 0 : 1;
        if(cameraRig==null)cameraRig=FindFirstObjectByType<ManagerCameraRig>();
        if(cameraRig!=null) {
            if(director){cameraRig.BeginFreeLook();cameraRig.EndFreeLook();}
            else cameraRig.ReturnToTarget();
        }
        WorldManagementHud.NotifyMessage(director
            ? (GameText.UseChinese ? "经营指挥：点击整间房下达指令，拖动查看酒店。" : "Director: select rooms to issue orders; drag to explore.")
            : (GameText.UseChinese ? "亲自巡视：点击地面行走，镜头跟随经理。" : "Walk: tap the floor to move your manager."));
    }
    private bool TickCameraInput()
    {
        if(cameraRig==null)return false;
        if(!_viewRestored && HotelSimSceneBridge.Instance!=null && HotelSimSceneBridge.Instance.Sim!=null) {
            _viewRestored=true;cameraRig.RestoreView(HotelSessionState.Current);
            if(cameraRig.GetComponent<HotelSceneSelection>()==null)cameraRig.gameObject.AddComponent<HotelSceneSelection>();
        }
        if(WorldManagementHud.SceneCameraInputBlocked) {
            if(_cameraGesture||_orbitMouse)CancelCameraPointer();
            _cameraGesture=false;_orbitMouse=false;return false;
        }
        TouchControl a=null,b=null;
        if(Touchscreen.current!=null) foreach(var touch in Touchscreen.current.touches)
            if(touch.press.isPressed){if(a==null)a=touch;else{b=touch;break;}}
        if(a!=null&&b!=null) {
            Vector2 pa=a.position.ReadValue(),pb=b.position.ReadValue();
            if(!_cameraGesture) {
                if(WorldManagementHud.ContainsScreenPoint(pa)||WorldManagementHud.ContainsScreenPoint(pb)) { CancelCameraPointer();_suppressUntilRelease=true;return true; }
                CancelCameraPointer();_cameraGesture=true;_touchA=pa;_touchB=pb;
            }
            var before=_touchB-_touchA;var after=pb-pa;
            if(before.magnitude>15&&after.magnitude>15) {
                cameraRig.SetZoom(cameraRig.Zoom*before.magnitude/after.magnitude);
                cameraRig.OrbitBy(-Vector2.SignedAngle(before,after));
                cameraRig.BeginFreeLook();cameraRig.ApplyScreenDrag((_touchA+_touchB)*.5f,(pa+pb)*.5f);cameraRig.EndFreeLook();
            }
            _touchA=pa;_touchB=pb;_suppressUntilRelease=true;return true;
        }
        if(_cameraGesture){_cameraGesture=false;CancelCameraPointer();}
        if(_suppressUntilRelease) { if(a==null)_suppressUntilRelease=false;return true; }
        var mouse=Mouse.current;
        if(mouse!=null) {
            Vector2 position=mouse.position.ReadValue();
            bool orbit=mouse.rightButton.isPressed||mouse.middleButton.isPressed;
            if(orbit) {
                if(!_orbitMouse && WorldManagementHud.ContainsScreenPoint(position))return true;
                if(_orbitMouse){var delta=position-_orbitPosition;cameraRig.OrbitBy(delta.x*.25f,-delta.y*.16f);}
                else CancelCameraPointer();
                _orbitMouse=true;_orbitPosition=position;return true;
            }
            _orbitMouse=false;
            if(!WorldManagementHud.ContainsScreenPoint(position)) {
                float scroll=mouse.scroll.ReadValue().y;
                if(Mathf.Abs(scroll)>.01f)cameraRig.ZoomBy(-scroll/120f*.7f);
            }
        }
        var keys=Keyboard.current;
        if(keys!=null) {
            float dt=Time.unscaledDeltaTime;
            Vector2 pan=new Vector2((keys.dKey.isPressed||keys.rightArrowKey.isPressed ? 1:0)-(keys.aKey.isPressed||keys.leftArrowKey.isPressed ? 1:0),
                (keys.wKey.isPressed||keys.upArrowKey.isPressed ? 1:0)-(keys.sKey.isPressed||keys.downArrowKey.isPressed ? 1:0));
            if(pan!=Vector2.zero)cameraRig.PanBy(pan,dt);
            if(keys.qKey.isPressed)cameraRig.OrbitBy(-75*dt);
            if(keys.eKey.isPressed)cameraRig.OrbitBy(75*dt);
            if(keys.fKey.wasPressedThisFrame)cameraRig.ReturnToTarget();
            if(keys.tabKey.wasPressedThisFrame)SetDirectorMode(!IsDirectorMode);
        }
        return false;
    }
    private void CancelCameraPointer()
    {
        _pressedLastFrame=false;_classifier=null;
        if(cameraRig!=null)cameraRig.EndFreeLook();
    }
    public static bool TryFindDirectorRoom(Ray ray,int viewedFloor,out RoomSceneBinder selected)
    {
        selected=null;
        if(!TryProjectRayToFloor(ray,FloorMath.BaseYFor(viewedFloor),out var point))return false;
        foreach(var binder in FindObjectsByType<RoomSceneBinder>(FindObjectsInactive.Exclude,FindObjectsSortMode.None)) {
            if(FloorMath.FloorIndexForY(binder.transform.position.y)!=viewedFloor)continue;
            var local=binder.transform.InverseTransformPoint(point);
            if(Mathf.Abs(local.x)<=2.5f&&Mathf.Abs(local.z)<=2.5f){selected=binder;return true;}
        }
        return false;
    }
}

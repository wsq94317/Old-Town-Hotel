using System.Collections.Generic;
using UnityEngine;

public class ManagerInteraction : MonoBehaviour
{
    [SerializeField] private ManagerController manager;
    [SerializeField] private Room2DPrototypeDemandLoop demandLoop;
    [SerializeField] private EconomySystem economy;

    private StaffAgent _caughtAgent;
    private StaffAgent _panelAgent;
    private string _lastMessage = "";
    private float _messageUntil;

    private StaffAgent _commandAgent;
    private TaskDispatcher _dispatcher;
    private GUIStyle _center;

    public bool PanelOpen => _caughtAgent != null || _panelAgent != null;
    public bool InCommandMode => _commandAgent != null;

    public void CommandTarget(Vector3 worldPoint)
    {
        var agent = _commandAgent;
        _commandAgent = null;
        if (agent == null || demandLoop == null || demandLoop.rooms == null) return;
        if (_dispatcher == null) _dispatcher = FindFirstObjectByType<TaskDispatcher>();
        if (_dispatcher == null) return;

        Room2DEntity best = FindCommandRoom(demandLoop.rooms, worldPoint);
        if (best == null)
        {
            Say("No room there.");
            return;
        }

        Say(_dispatcher.ForceAssign(agent, best)
            ? $"{agent.Member?.DisplayName} -> Room {best.roomNumber}. Chop chop."
            : $"Room {best.roomNumber} doesn't need {agent.Member?.Role} right now.");
    }

    public static Room2DEntity FindCommandRoom(IReadOnlyList<Room2DEntity> rooms, Vector3 worldPoint, float maxDistance = 3f)
    {
        if (rooms == null) return null;

        int targetFloor = FloorMath.FloorIndexForY(worldPoint.y);
        Room2DEntity best = null;
        float bestDistSq = maxDistance * maxDistance;

        foreach (var room in rooms)
        {
            if (room == null) continue;
            if (FloorMath.FloorIndexForY(room.transform.position.y) != targetFloor) continue;

            Vector3 delta = room.transform.position - worldPoint;
            delta.y = 0f;
            float distSq = delta.sqrMagnitude;
            if (distSq > bestDistSq) continue;
            if (best == null || distSq < bestDistSq ||
                (Mathf.Approximately(distSq, bestDistSq) && room.roomNumber < best.roomNumber))
            {
                best = room;
                bestDistSq = distSq;
            }
        }

        return best;
    }

    private void Awake()
    {
        if (manager == null) manager = FindFirstObjectByType<ManagerController>();
        if (demandLoop == null) demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        StaffAgent.OnAnyCaught += HandleCaught;
    }

    private void OnDestroy() => StaffAgent.OnAnyCaught -= HandleCaught;

    private void HandleCaught(StaffAgent agent) => _caughtAgent = agent;

    public void OnStaffTapped(StaffAgent agent)
    {
        if (agent == null || manager == null) return;
        Vector3 delta = agent.transform.position - manager.transform.position;
        delta.y = 0f;
        if (FloorMath.FloorIndexForY(agent.transform.position.y) == FloorMath.FloorIndexForY(manager.transform.position.y)
            && delta.magnitude <= SupervisionTuning.CatchRadius + 1f)
        {
            _panelAgent = agent;
        }
        else
        {
            manager.MoveTo(agent.transform.position);
        }
    }

    private void Say(string msg)
    {
        _lastMessage = msg;
        _messageUntil = Time.time + 3f;
    }

    private Room2DEntity FlawedRoomNearby()
    {
        if (manager == null || demandLoop == null || demandLoop.rooms == null) return null;
        int managerFloor = FloorMath.FloorIndexForY(manager.transform.position.y);
        foreach (var room in demandLoop.rooms)
        {
            if (room == null || RoomFlaw.Get(room) == null) continue;
            if (FloorMath.FloorIndexForY(room.transform.position.y) != managerFloor) continue;
            Vector3 delta = room.transform.position - manager.transform.position;
            delta.y = 0f;
            if (delta.magnitude < 2.2f) return room;
        }
        return null;
    }

    private void OnGUI()
    {
        Vector2 view = GuiScale.Begin();
        float w = view.x;
        float h = view.y;

        if (Time.time < _messageUntil)
            GUI.Label(new Rect(w * 0.5f - 200, h * 0.15f, 400, 30), _lastMessage, CenterStyle());

        if (_caughtAgent != null)
        {
            var caught = _caughtAgent;
            GUI.Box(new Rect(w * 0.5f - 170, h * 0.35f, 340, 130),
                $"CAUGHT SLACKING!  {caught.Member?.DisplayName} ({caught.Member?.Role})");
            if (GuiInput.Button(new Rect(w * 0.5f - 150, h * 0.35f + 40, 300, 24), "Urge back to work (morale -5, speed up)"))
                ResolveCatch(caught, CatchChoice.Urge, "Back to work!");
            else if (GuiInput.Button(new Rect(w * 0.5f - 150, h * 0.35f + 68, 300, 24), "Scold hard (morale -15, faster)"))
                ResolveCatch(caught, CatchChoice.Scold, "Scolded.");
            else if (GuiInput.Button(new Rect(w * 0.5f - 150, h * 0.35f + 96, 300, 24), "Look away (morale +3, slacking spreads)"))
                ResolveCatch(caught, CatchChoice.Ignore, "You saw nothing.");
            return;
        }

        if (_panelAgent != null)
        {
            var agent = _panelAgent;
            var member = agent.Member;
            GUI.Box(new Rect(w * 0.5f - 150, h * 0.4f, 300, 150),
                $"{member?.DisplayName} ({member?.Role})  morale:{member?.Morale}" + (agent.IsGrudging ? "  GRUDGING" : ""));

            bool canInterrogate = agent.HasDelayMark;
            if (GuiInput.Button(new Rect(w * 0.5f - 130, h * 0.4f + 34, 260, 24), "Hurry up! (speed up, morale -3)"))
            {
                agent.Hurry();
                Say("Hurried.");
                _panelAgent = null;
            }
            else if (InterrogateButton(new Rect(w * 0.5f - 130, h * 0.4f + 62, 260, 24), canInterrogate))
            {
                _panelAgent = null;
                if (agent.Interrogate() == InterrogationVerdict.Caught) _caughtAgent = agent;
                else Say($"WRONG ACCUSATION! {agent.Member?.DisplayName} is furious (morale {SupervisionTuning.WrongAccusationMoraleDelta}).");
            }
            else if (GuiInput.Button(new Rect(w * 0.5f - 130, h * 0.4f + 90, 260, 24), "Command -> tap a room"))
            {
                _commandAgent = agent;
                _panelAgent = null;
                Say("Now tap the room you want them on.");
            }
            else if (GuiInput.Button(new Rect(w * 0.5f - 130, h * 0.4f + 118, 125, 24), "FIRE THEM"))
            {
                _panelAgent = null;
                if (economy != null && agent.Member != null)
                {
                    FloatingTextFx.Spawn(agent.transform.position, "FIRED!", new Color(1f, 0.25f, 0.2f), 1.2f);
                    economy.FireStaff(agent.Member);
                    Say($"{agent.Member.DisplayName} is packing. The stapler goes with them.");
                }
            }
            else if (GuiInput.Button(new Rect(w * 0.5f + 5, h * 0.4f + 118, 125, 24), "Close"))
            {
                _panelAgent = null;
            }
            return;
        }

        var flawed = FlawedRoomNearby();
        if (flawed != null)
        {
            var flawRect = new Rect(w * 0.5f - 150, h - 90, 300, 30);
            GuiInput.ReserveZone(flawRect);
            if (GuiInput.Button(flawRect, $"Room {flawed.roomNumber}: flaw found -> send back to cleaning"))
            {
                RoomFlaw.Clear(flawed);
                flawed.SetState(Room2DState.Dirty);
                Say($"Room {flawed.roomNumber} sent back to cleaning.");
            }
        }
    }

    private void ResolveCatch(StaffAgent agent, CatchChoice choice, string message)
    {
        if (agent == null || _caughtAgent != agent) return;
        _caughtAgent = null;
        agent.ApplyCatchChoice(choice);
        Say(message);
    }

    private static bool InterrogateButton(Rect rect, bool enabled)
    {
        bool prev = GUI.enabled;
        GUI.enabled = enabled;
        bool clicked = GuiInput.Button(rect, enabled ? "Interrogate the delay" : "Interrogate (no delay mark)");
        GUI.enabled = prev;
        return clicked;
    }

    private GUIStyle CenterStyle()
    {
        if (_center == null)
            _center = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 15 };
        return _center;
    }
}

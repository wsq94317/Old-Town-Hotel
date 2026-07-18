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
    private bool _angryToiletGuest;
    private TaskDispatcher _dispatcher;
    private GUIStyle _center;
    private GUIStyle _wrappedLabel;
    private Vector2 _commandRoomScroll;

    public bool PanelOpen => _caughtAgent != null || _panelAgent != null || _commandAgent != null || _angryToiletGuest;
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

    public static List<Room2DEntity> GetCommandRoomCandidates(IReadOnlyList<Room2DEntity> rooms, StaffRole role)
    {
        var candidates = new List<Room2DEntity>();
        if (rooms == null) return candidates;

        foreach (var room in rooms)
        {
            if (room == null || !CanCommandRoleToRoom(role, room)) continue;
            candidates.Add(room);
        }

        candidates.Sort((left, right) =>
        {
            int floorCompare = FloorMath.FloorIndexForY(left.transform.position.y)
                .CompareTo(FloorMath.FloorIndexForY(right.transform.position.y));
            if (floorCompare != 0) return floorCompare;
            return left.roomNumber.CompareTo(right.roomNumber);
        });

        return candidates;
    }

    private static bool CanCommandRoleToRoom(StaffRole role, Room2DEntity room)
    {
        if (room == null) return false;

        switch (role)
        {
            case StaffRole.Housekeeper:
                return room.currentState == Room2DState.Dirty;
            case StaffRole.Inspector:
                return room.currentState == Room2DState.AwaitingInspection;
            default:
                return false;
        }
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

    public void HandleToiletInspection(ToiletInspectionResult result)
    {
        switch (result.Kind)
        {
            case ToiletInspectionKind.StaffHiding:
                Say($"Caught {result.Staff?.Member?.DisplayName} hiding in the public toilet.");
                break;
            case ToiletInspectionKind.StaffLegitimate:
                Say($"Wrong door. {result.Staff?.Member?.DisplayName} was using the toilet normally (morale -4).");
                break;
            case ToiletInspectionKind.Guest:
                _angryToiletGuest = true;
                break;
            default:
                Say("The public toilet is empty.");
                break;
        }
    }

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

        DrawRoleLegend(view);

        if (_angryToiletGuest)
        {
            var guestRect = new Rect(w * 0.5f - 190f, h * 0.32f, 380f, 170f);
            GuiInput.ReserveZone(guestRect);
            GUI.Box(guestRect, "FURIOUS GUEST - You barged into the public toilet");
            GUI.Label(new Rect(guestRect.x + 18f, guestRect.y + 28f, guestRect.width - 36f, 40f),
                "The guest demands an apology. How do you handle it?", WrappedLabelStyle());
            if (GuiInput.Button(new Rect(guestRect.x + 18f, guestRect.y + 72f, guestRect.width - 36f, 26f),
                "Sincere apology + $30 compensation"))
                ResolveToiletGuestIncident(30, 2, 0, "Guest accepts the apology and compensation.");
            else if (GuiInput.Button(new Rect(guestRect.x + 18f, guestRect.y + 102f, guestRect.width - 36f, 26f),
                "Apologize without compensation"))
                ResolveToiletGuestIncident(0, -1, 0, "Guest leaves annoyed.");
            else if (GuiInput.Button(new Rect(guestRect.x + 18f, guestRect.y + 132f, guestRect.width - 36f, 26f),
                "Argue that managers must inspect"))
                ResolveToiletGuestIncident(0, -5, -2, "The argument becomes a terrible review.");
            return;
        }

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
                $"{member?.DisplayName} ({member?.Role})  morale:{member?.Morale}\n{agent.ShiftState} / {agent.ActivityState}"
                + (agent.IsGrudging ? "  GRUDGING" : ""));

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
            else if (GuiInput.Button(new Rect(w * 0.5f - 130, h * 0.4f + 90, 260, 24), "Assign room..."))
            {
                _commandAgent = agent;
                _panelAgent = null;
                Say("Pick a room from the list.");
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

        if (_commandAgent != null)
        {
            DrawCommandRoomPicker(view, _commandAgent);
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

    private void ResolveToiletGuestIncident(int compensation, int satisfaction, int prestige, string message)
    {
        if (!_angryToiletGuest) return;

        if (compensation > 0 && (economy == null || !economy.TrySpend(compensation)))
        {
            Say("You cannot afford the promised compensation.");
            return;
        }

        _angryToiletGuest = false;
        demandLoop?.ApplyExternalSatisfaction(satisfaction, "Public toilet privacy incident");
        ManagerReputation.Add(prestige);
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

    private GUIStyle WrappedLabelStyle()
    {
        if (_wrappedLabel == null)
        {
            _wrappedLabel = new GUIStyle(GUI.skin.label)
            {
                wordWrap = true,
                richText = true,
                fontSize = 14
            };
        }
        return _wrappedLabel;
    }

    private void DrawCommandRoomPicker(Vector2 view, StaffAgent agent)
    {
        var member = agent != null ? agent.Member : null;
        var role = member != null ? member.Role : StaffRole.Manager;
        var candidates = GetCommandRoomCandidates(demandLoop != null ? demandLoop.rooms : null, role);
        float width = Mathf.Min(560f, view.x - 40f);
        float height = Mathf.Min(420f, view.y - 120f);
        var panelRect = new Rect((view.x - width) * 0.5f, Mathf.Max(48f, view.y * 0.16f), width, height);
        var listRect = new Rect(panelRect.x + 16f, panelRect.y + 54f, panelRect.width - 32f, panelRect.height - 108f);
        float rowHeight = 54f;
        float contentHeight = Mathf.Max(listRect.height - 4f, candidates.Count * (rowHeight + 8f));
        var contentRect = new Rect(0f, 0f, listRect.width - 18f, contentHeight);

        GuiInput.ReserveZone(panelRect);
        GUI.Box(panelRect, $"Assign Room - {member?.DisplayName ?? "Staff"} ({role})");

        string helperText = candidates.Count > 0
            ? "Pick a room from the live list. This no longer depends on clicking the right floor in the world."
            : $"No rooms currently need {role}.";
        GUI.Label(
            new Rect(panelRect.x + 16f, panelRect.y + 24f, panelRect.width - 32f, 28f),
            helperText,
            WrappedLabelStyle());

        _commandRoomScroll = GUI.BeginScrollView(listRect, _commandRoomScroll, contentRect, false, contentHeight > listRect.height);
        for (int i = 0; i < candidates.Count; i++)
        {
            Room2DEntity room = candidates[i];
            float rowY = i * (rowHeight + 8f);
            var rowRect = new Rect(0f, rowY, contentRect.width, rowHeight);
            GUI.Box(rowRect, GUIContent.none);

            int floor = FloorMath.FloorIndexForY(room.transform.position.y) + 1;
            string label = $"Room {room.roomNumber}  |  F{floor}  |  {room.GetStateDisplayName()}";
            if (room.markedCleaningPriority) label += "  |  CLEAN PRIO";
            if (room.markedInspectionPriority) label += "  |  INSP PRIO";
            GUI.Label(new Rect(12f, rowY + 7f, contentRect.width - 144f, rowHeight - 14f), label, WrappedLabelStyle());

            if (GuiInput.Button(new Rect(contentRect.width - 116f, rowY + 10f, 104f, 32f), "Assign"))
            {
                CommandRoom(agent, room);
                GUI.EndScrollView();
                return;
            }
        }
        GUI.EndScrollView();

        if (GuiInput.Button(new Rect(panelRect.x + 16f, panelRect.y + panelRect.height - 40f, panelRect.width - 32f, 28f), "Cancel"))
        {
            _commandAgent = null;
        }
    }

    private void CommandRoom(StaffAgent agent, Room2DEntity room)
    {
        _commandAgent = null;
        if (agent == null || room == null) return;
        if (_dispatcher == null) _dispatcher = FindFirstObjectByType<TaskDispatcher>();
        if (_dispatcher == null) return;

        Say(_dispatcher.ForceAssign(agent, room)
            ? $"{agent.Member?.DisplayName} -> Room {room.roomNumber}. Chop chop."
            : $"Room {room.roomNumber} doesn't need {agent.Member?.Role} right now.");
    }

    private void DrawRoleLegend(Vector2 view)
    {
        float width = Mathf.Min(260f, view.x * 0.42f);
        float height = 108f;
        var legendRect = new Rect(18f, view.y - height - 18f, width, height);
        GUI.Box(legendRect, "Role Legend");
        GUI.Label(new Rect(legendRect.x + 12f, legendRect.y + 12f, legendRect.width - 24f, 16f), "Colored tall blocks in the scene = staff roles.");

        DrawLegendRow(legendRect, 0, StaffAgentSpawner.RoleColor(StaffRole.Housekeeper), "Green block = Housekeeper");
        DrawLegendRow(legendRect, 1, StaffAgentSpawner.RoleColor(StaffRole.Inspector), "Blue block = Inspector");
        DrawLegendRow(legendRect, 2, StaffAgentSpawner.RoleColor(StaffRole.Reception), "Purple block = Reception");
    }

    private void DrawLegendRow(Rect legendRect, int rowIndex, Color color, string text)
    {
        float y = legendRect.y + 34f + rowIndex * 22f;
        Color previous = GUI.color;
        GUI.color = color;
        GUI.Box(new Rect(legendRect.x + 12f, y, 18f, 18f), GUIContent.none);
        GUI.color = previous;
        GUI.Label(new Rect(legendRect.x + 38f, y - 1f, legendRect.width - 50f, 20f), text);
    }
}

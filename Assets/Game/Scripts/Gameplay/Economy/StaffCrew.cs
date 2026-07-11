using System.Collections.Generic;
using UnityEngine;

// Bridges the payroll roster to live worker instances in the scene (ADR 0008:
// headcount is data-driven — workers may only enter play through the hiring
// system, never by hand-placing scene objects).
//
// The scene's existing Housekeeper2D acts as the template: the first rostered
// housekeeper binds to it, additional hires clone it, and fired workers despawn
// (deferred until they finish their current job — severance covers the day).
// Inspector stays single-instance for now (late-hire role per economy GDD §3.3).
[DisallowMultipleComponent]
public sealed class StaffCrew : MonoBehaviour
{
    [SerializeField] private EconomySystem economy;
    [SerializeField] private Housekeeper2D housekeeperTemplate;

    private readonly List<Housekeeper2D> _housekeepers = new List<Housekeeper2D>();
    private readonly Dictionary<StaffMember, Housekeeper2D> _byMember =
        new Dictionary<StaffMember, Housekeeper2D>();
    private readonly List<Housekeeper2D> _pendingDespawn = new List<Housekeeper2D>();
    private PayrollLedger _subscribedPayroll;

    public IReadOnlyList<Housekeeper2D> Housekeepers => _housekeepers;

    public bool AnyHousekeeperIdle
    {
        get
        {
            for (int i = 0; i < _housekeepers.Count; i++)
                if (_housekeepers[i] != null && _housekeepers[i].IsAvailableForAssignment())
                    return true;
            return false;
        }
    }

    public Housekeeper2D FindIdleHousekeeper()
    {
        for (int i = 0; i < _housekeepers.Count; i++)
            if (_housekeepers[i] != null && _housekeepers[i].IsAvailableForAssignment())
                return _housekeepers[i];
        return null;
    }

    // The staff member a live worker instance belongs to (UI: name tags, morale).
    public StaffMember MemberOf(Housekeeper2D worker)
    {
        foreach (var kv in _byMember)
            if (kv.Value == worker) return kv.Key;
        return null;
    }

    private void Awake()
    {
        if (economy == null) economy = FindFirstObjectByType<EconomySystem>();
        if (housekeeperTemplate == null) housekeeperTemplate = FindFirstObjectByType<Housekeeper2D>();
    }

    private void Start() => EnsureSubscribed();

    private void OnDestroy() => Unsubscribe();

    private void Update()
    {
        // EconomySystem.RestoreState replaces the PayrollLedger instance wholesale;
        // detect the swap and re-sync instead of holding dead event subscriptions.
        if (economy != null && economy.Payroll != _subscribedPayroll) EnsureSubscribed();
        ProcessPendingDespawns();
    }

    private void EnsureSubscribed()
    {
        if (economy == null || economy.Payroll == null) return;
        Unsubscribe();
        _subscribedPayroll = economy.Payroll;
        _subscribedPayroll.OnHired += HandleHired;
        _subscribedPayroll.OnFired += HandleFired;
        SyncFromRoster();
    }

    private void Unsubscribe()
    {
        if (_subscribedPayroll == null) return;
        _subscribedPayroll.OnHired -= HandleHired;
        _subscribedPayroll.OnFired -= HandleFired;
        _subscribedPayroll = null;
    }

    private void HandleHired(StaffMember member)
    {
        if (member != null && member.Role == StaffRole.Housekeeper) SpawnFor(member);
    }

    private void HandleFired(StaffMember member)
    {
        if (member == null || !_byMember.TryGetValue(member, out var worker)) return;
        _byMember.Remove(member);
        if (worker == null) return;
        // Let a busy worker finish the room they're in (their last shift), then despawn.
        if (worker.IsBusy) _pendingDespawn.Add(worker);
        else Despawn(worker);
    }

    // Full rebuild: bind every rostered housekeeper to an instance (template first).
    private void SyncFromRoster()
    {
        foreach (var worker in _housekeepers)
            if (worker != null && worker != housekeeperTemplate) Destroy(worker.gameObject);
        _housekeepers.Clear();
        _byMember.Clear();
        _pendingDespawn.Clear();
        if (housekeeperTemplate != null) housekeeperTemplate.gameObject.SetActive(false);

        foreach (var member in _subscribedPayroll.Roster)
            if (member.Role == StaffRole.Housekeeper) SpawnFor(member);
    }

    private void SpawnFor(StaffMember member)
    {
        if (_byMember.ContainsKey(member)) return;
        Housekeeper2D worker;
        if (housekeeperTemplate != null && !housekeeperTemplate.gameObject.activeSelf)
        {
            // First hire reuses the hand-placed template instance.
            housekeeperTemplate.gameObject.SetActive(true);
            worker = housekeeperTemplate;
        }
        else if (housekeeperTemplate != null)
        {
            worker = Instantiate(housekeeperTemplate.gameObject, housekeeperTemplate.transform.parent)
                .GetComponent<Housekeeper2D>();
            // Instantiate copies public fields — a clone taken from a busy template
            // must not inherit its work-in-progress.
            worker.currentState = Housekeeper2D.HousekeeperState.Idle;
            worker.assignedRoom = null;
            worker.cleaningTimerSeconds = 0f;
            worker.travelTimerSeconds = 0f;
            worker.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning("StaffCrew: no Housekeeper2D template in scene; cannot spawn workers.");
            return;
        }
        worker.gameObject.name = $"HSK_{member.DisplayName}";
        _housekeepers.Add(worker);
        _byMember[member] = worker;
    }

    private void ProcessPendingDespawns()
    {
        for (int i = _pendingDespawn.Count - 1; i >= 0; i--)
        {
            var worker = _pendingDespawn[i];
            if (worker == null) { _pendingDespawn.RemoveAt(i); continue; }
            if (worker.IsBusy) continue;
            _pendingDespawn.RemoveAt(i);
            Despawn(worker);
        }
    }

    private void Despawn(Housekeeper2D worker)
    {
        _housekeepers.Remove(worker);
        if (worker == housekeeperTemplate) worker.gameObject.SetActive(false); // keep the template
        else Destroy(worker.gameObject);
    }
}

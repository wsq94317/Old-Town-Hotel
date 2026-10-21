using UnityEngine;
using UnityEngine.SceneManagement;

// sceneLoaded runs for slot loads/new games as well as the initial scene.
public static class WorldSceneLifecycle
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= Install;
        SceneManager.sceneLoaded += Install;
    }

    private static void Install(Scene scene, LoadSceneMode mode)
    {
        if (Object.FindFirstObjectByType<HotelSimSceneBridge>() == null) return;
        HotelSceneExpansion.Install();
        HotelArtDirection.Install();
        HotelBusinessHotspotInstaller.Install();
        WorldManagementHud.InstallForWorldScene();
        var manager = Object.FindFirstObjectByType<ManagerController>();
        if (manager != null)
        {
            var agent = manager.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && !agent.enabled) {
                if (UnityEngine.AI.NavMesh.SamplePosition(manager.transform.position,out var hit,.75f,UnityEngine.AI.NavMesh.AllAreas))
                    manager.transform.position = hit.position;
                agent.enabled = true;
            }
            if (manager.GetComponentInChildren<HotelCharacterArt>(true) == null)
                HotelCharacterArt.Create(manager.transform, StaffRole.Manager);
        }
        WarehouseWorldView.Install();
        HotelAudio.Ensure();
        HotelLightingDirector.Install();
    }
}

using UnityEngine;

// 查错房戏剧：经理闯进入住中的房间——
//   受惊客人从床边弹出（❗）→ 扔枕头砸经理（抛物线+WHAM!震屏）→
//   经理被撵出门外（强制走去走廊）→ 满意度 -1 + 随机羞耻台词。
public static class WrongRoomDrama
{
    private static readonly string[] Lines =
    {
        "AAAH!! GET OUT!!",
        "DO YOU MIND?!",
        "I'M CALLING THE FRONT DESK— wait, you ARE the front desk!",
        "THIS IS NOT THE SPA!!",
        "KNOCK. FIRST.",
    };
    private static int _lineIndex;

    public static void Trigger(RoomDoor door, ManagerController manager)
    {
        var host = door.gameObject.AddComponent<WrongRoomDramaRunner>();
        host.Run(door, manager);
    }

    public static string NextLine()
    {
        _lineIndex = (_lineIndex + 1) % Lines.Length;
        return Lines[_lineIndex];
    }
}

// 协程宿主（挂在门上，演完自毁）。
public class WrongRoomDramaRunner : MonoBehaviour
{
    public void Run(RoomDoor door, ManagerController manager)
    {
        StartCoroutine(Drama(door, manager));
    }

    private System.Collections.IEnumerator Drama(RoomDoor door, ManagerController manager)
    {
        Vector3 roomCenter = door.InteriorCenter;

        // ① 受惊客人弹出（黄色纸片 + 红色❗）
        var guest = GuestAgent.Spawn(roomCenter + new Vector3(0.6f, 0f, 0.6f), "startled");
        var emote = EmoteBubble.Attach(guest.transform);
        emote.Show(EmoteBubble.Emote.Alert);
        FloatingTextFx.Spawn(roomCenter, WrongRoomDrama.NextLine(), new Color(1f, 0.55f, 0.2f), 1.1f);
        yield return new WaitForSeconds(0.35f);

        // ② 枕头抛物线砸向经理
        var pillow = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.Destroy(pillow.GetComponent<Collider>());
        pillow.name = "Pillow";
        pillow.transform.localScale = new Vector3(0.45f, 0.16f, 0.3f);
        pillow.GetComponent<Renderer>().sharedMaterial = PillowMat();
        pillow.AddComponent<AgentFloorVisibility>(); // 协程若中途被楼层切换杀掉，枕头至少不会全楼漂浮

        Vector3 from = guest.transform.position + Vector3.up * 1.1f;
        Vector3 to = manager != null ? manager.transform.position + Vector3.up * 0.9f : roomCenter;
        float t = 0f;
        const float flight = 0.45f;
        while (t < flight)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / flight);
            Vector3 pos = Vector3.Lerp(from, to, k);
            pos.y += Mathf.Sin(k * Mathf.PI) * 0.8f; // 抛物线
            pillow.transform.position = pos;
            pillow.transform.Rotate(0f, 0f, 720f * Time.deltaTime);
            yield return null;
        }
        Object.Destroy(pillow);
        CameraShaker.Shake(0.22f, 0.3f);
        if (manager != null)
            FloatingTextFx.Spawn(manager.transform.position, "WHAM!", new Color(1f, 0.3f, 0.25f), 1.2f);

        // ③ 撵出门外：强制走到门口外侧（走廊方向）
        if (manager != null)
        {
            Vector3 doorPos = door.transform.position;
            Vector3 outward = (doorPos - roomCenter).normalized; // 房内→门口方向 = 出门方向
            manager.MoveTo(doorPos + outward * 1.2f);
        }

        // ④ 代价：满意度 -1（客人会到处说的）
        var demandLoop = FindFirstObjectByType<Room2DPrototypeDemandLoop>();
        if (demandLoop != null) demandLoop.prototypeSatisfactionScore -= 1;

        // ⑤ 客人骂骂咧咧缩回房里
        yield return new WaitForSeconds(1.2f);
        if (guest != null) Object.Destroy(guest.gameObject);
        Object.Destroy(this);
    }

    private static Material _pillowMat;
    private static Material PillowMat()
    {
        if (_pillowMat == null)
        {
            _pillowMat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            { color = new Color(0.98f, 0.98f, 0.92f) };
        }
        return _pillowMat;
    }
}

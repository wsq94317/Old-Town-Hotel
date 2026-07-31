#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class FloorPrefabAuthoringTest
{
    private static readonly string[] Paths =
    {
        "Assets/Game/Prefabs/Maps/Floors/Floor1.prefab",
        "Assets/Game/Prefabs/Maps/Floors/Floor2.prefab",
        "Assets/Game/Prefabs/Maps/Floors/Floor3.prefab",
        "Assets/Game/Prefabs/Maps/Floors/Floor4.prefab",
        "Assets/Game/Prefabs/Maps/Floors/Floor5.prefab",
        "Assets/Game/Prefabs/Maps/Floors/Floor6.prefab",
        "Assets/Game/Prefabs/Maps/Floors/Floor7.prefab"
    };

    [Test]
    public void EveryFloorHasAnOriginAuthoredPrefab()
    {
        for (int i = 0; i < Paths.Length; i++)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Paths[i]);
            Assert.That(prefab, Is.Not.Null, Paths[i]);
            Assert.That(prefab.name, Is.EqualTo("Floor" + (i + 1)));
            Assert.That(prefab.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(prefab.transform.Find("Ground"), Is.Not.Null, Paths[i]);
            Assert.That(prefab.transform.Find("ArtPass_LowPoly"), Is.Not.Null, Paths[i]);
            Assert.That(prefab.transform.Find("ElevatorPad"), Is.Not.Null, Paths[i]);
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    public void GuestFloorPrefabsContainTheCompleteEditableRoomLayout(int guestFloorIndex)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Paths[guestFloorIndex]);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<RoomSceneBinder>(true).Length, Is.EqualTo(12));
        Assert.That(prefab.GetComponentsInChildren<RoomDoor>(true).Length, Is.EqualTo(12));
    }
}
#endif

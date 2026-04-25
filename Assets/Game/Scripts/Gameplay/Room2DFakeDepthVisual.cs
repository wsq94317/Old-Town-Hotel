using UnityEngine;

public class Room2DFakeDepthVisual : MonoBehaviour
{
    [Header("Room Layers")]
    public SpriteRenderer shadowRenderer;
    public SpriteRenderer floorRenderer;
    public SpriteRenderer backWallRenderer;
    public SpriteRenderer leftWallRenderer;
    public SpriteRenderer rightWallRenderer;
    public SpriteRenderer[] furnitureRenderers;
    public SpriteRenderer[] stateMarkerRenderers;

    [Header("Sorting Orders")]
    public int shadowOrder = 20;
    public int floorOrder = 10;
    public int backWallOrder = 0;
    public int sideWallOrder = 10;
    public int furnitureOrder = 10;
    public int stateMarkerOrder = 20;

    [Header("Prototype Colors")]
    public Color shadowColor = new Color(0f, 0f, 0f, 0.25f);
    public Color floorColor = new Color(0.45f, 0.34f, 0.28f, 1f);
    public Color sideWallColor = new Color(0.36f, 0.47f, 0.44f, 1f);

    private void Start()
    {
        ApplyFakeDepthVisuals();
    }

    private void OnValidate()
    {
        ApplyFakeDepthVisuals();
    }

    public void ApplyFakeDepthVisuals()
    {
        ApplyRenderer(shadowRenderer, shadowOrder, shadowColor);
        ApplyRenderer(floorRenderer, floorOrder, floorColor);
        ApplyRenderer(backWallRenderer, backWallOrder);
        ApplyRenderer(leftWallRenderer, sideWallOrder, sideWallColor);
        ApplyRenderer(rightWallRenderer, sideWallOrder, sideWallColor);
        ApplyRenderers(furnitureRenderers, furnitureOrder);
        ApplyRenderers(stateMarkerRenderers, stateMarkerOrder);
    }

    [ContextMenu("Apply Simple Prototype Layout")]
    public void ApplySimplePrototypeLayout()
    {
        ApplyTransform(shadowRenderer, new Vector3(0f, -1.5f, 0f), new Vector3(4.7f, 0.35f, 1f));
        ApplyTransform(floorRenderer, new Vector3(0f, -0.95f, 0f), new Vector3(4.1f, 1.15f, 1f));
        ApplyTransform(backWallRenderer, new Vector3(0f, 0.65f, 0f), new Vector3(4.1f, 1.85f, 1f));
        ApplyTransform(leftWallRenderer, new Vector3(-2.15f, -0.75f, 0f), new Vector3(0.3f, 1.2f, 1f));
        ApplyTransform(rightWallRenderer, new Vector3(2.15f, -0.75f, 0f), new Vector3(0.3f, 1.2f, 1f));
        ApplyFakeDepthVisuals();
    }

    private void ApplyRenderer(SpriteRenderer targetRenderer, int sortingOrder)
    {
        if (targetRenderer != null)
        {
            targetRenderer.sortingOrder = sortingOrder;
        }
    }

    private void ApplyRenderer(SpriteRenderer targetRenderer, int sortingOrder, Color color)
    {
        if (targetRenderer != null)
        {
            targetRenderer.sortingOrder = sortingOrder;
            targetRenderer.color = color;
        }
    }

    private void ApplyRenderers(SpriteRenderer[] renderers, int sortingOrder)
    {
        if (renderers == null)
        {
            return;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            ApplyRenderer(renderers[i], sortingOrder);
        }
    }

    private void ApplyTransform(SpriteRenderer targetRenderer, Vector3 localPosition, Vector3 localScale)
    {
        if (targetRenderer != null)
        {
            targetRenderer.transform.localPosition = localPosition;
            targetRenderer.transform.localScale = localScale;
        }
    }
}

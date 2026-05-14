using System.Collections.Generic;
using UnityEngine;

public sealed class ModalManager : MonoBehaviour
{
    public static ModalManager Instance { get; private set; }

    [SerializeField] private Transform modalRoot;
    [SerializeField] private UITheme theme;

    private readonly List<ModalBase> stack = new List<ModalBase>(4);

    public UITheme Theme => theme;
    public int OpenCount => stack.Count;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (modalRoot == null) modalRoot = transform;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public T Show<T>(T prefab) where T : ModalBase
    {
        if (prefab == null)
        {
            Debug.LogError("ModalManager.Show called with null prefab.");
            return null;
        }

        var instance = Instantiate(prefab, modalRoot);
        stack.Add(instance);
        instance.OnOpenedByManager(() => OnModalClosed(instance));
        return instance;
    }

    public void Close(ModalBase modal)
    {
        if (modal == null) return;
        modal.Close();
    }

    public void CloseAll()
    {
        for (int i = stack.Count - 1; i >= 0; i--)
        {
            var modal = stack[i];
            if (modal != null) modal.Close();
        }
    }

    private void OnModalClosed(ModalBase modal)
    {
        stack.Remove(modal);
    }
}

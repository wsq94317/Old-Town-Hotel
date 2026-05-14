using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AchievementsModal : ModalBase
{
    [Header("Modal 7 content")]
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI emptyPlaceholderLabel;
    [SerializeField] private Transform entryRoot;
    [SerializeField] private AchievementEntryView entryPrefab;
    [SerializeField] private Button closeButton;

    public event Action OnClosed;

    private readonly List<AchievementEntryView> spawnedEntries = new List<AchievementEntryView>();

    public void Setup(IList<AchievementEntryInfo> entries)
    {
        if (titleLabel != null) titleLabel.text = "成就";

        ClearEntries();

        bool empty = entries == null || entries.Count == 0;
        if (emptyPlaceholderLabel != null)
        {
            emptyPlaceholderLabel.gameObject.SetActive(empty);
            if (empty) emptyPlaceholderLabel.text = "暂无成就 — 敬请期待";
        }
        if (entryRoot != null) entryRoot.gameObject.SetActive(!empty);

        if (!empty)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entryPrefab == null || entryRoot == null) break;
                var view = Instantiate(entryPrefab, entryRoot);
                view.Setup(entries[i]);
                spawnedEntries.Add(view);
            }
        }
    }

    protected override void OnOpened()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => { OnClosed?.Invoke(); Close(); });
        }
    }

    protected override void OnClosing() => ClearEntries();

    private void ClearEntries()
    {
        foreach (var entry in spawnedEntries)
            if (entry != null && entry.gameObject != null) Destroy(entry.gameObject);
        spawnedEntries.Clear();
    }
}

public sealed class AchievementEntryInfo
{
    public string id;
    public string title;
    public string description;
    public Sprite icon;
    public bool unlocked;
    public float progress; // 0..1
}

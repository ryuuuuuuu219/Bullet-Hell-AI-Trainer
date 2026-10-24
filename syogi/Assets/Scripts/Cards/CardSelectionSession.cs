using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

// Runtime inventory is persisted separately from the catalog asset.
public static class CardSelectionSession
{
    public static CardCatalog Catalog { get; private set; }
    private static CardInventory inventory;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() { Catalog = null; inventory = null; saveBlocked = false; }

    public static CardInventory GetInventory(CardCatalog catalog)
    {
        if (catalog == null) return null;
        if (inventory == null || Catalog != catalog)
        {
            Catalog = catalog;
            saveBlocked = false;
            inventory = new CardInventory(catalog.cards);
            string path = GetSavePath(catalog);
            if (File.Exists(path))
            {
                try
                {
                    inventory = CardInventory.FromSave(
                        JsonUtility.FromJson<CardInventory.SaveData>(File.ReadAllText(path)));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Could not load card save: {ex.Message}");
                    // Preserve unreadable data before subsequent automatic saves.
                    try { File.Copy(path, path + ".invalid-" + DateTime.UtcNow.Ticks, false); }
                    catch (Exception backupError)
                    {
                        Debug.LogWarning($"Could not back up card save: {backupError.Message}");
                        saveBlocked = true;
                    }
                }
            }
        }
        return inventory;
    }

    public static void Save(CardCatalog catalog, IReadOnlyList<CardData> cards)
    {
        GetInventory(catalog)?.SetSelection(cards);
        Persist(catalog);
    }

    private static bool saveBlocked;

    // Catalog names identify save slots; keep them unique and stable.
    public static string GetSavePath(CardCatalog catalog)
    {
        using (SHA256 hash = SHA256.Create())
        {
            string key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(catalog.name)))
                .Replace("-", "").ToLowerInvariant();
            return Path.Combine(Application.persistentDataPath, "cards-" + key + ".json");
        }
    }

    public static void Persist(CardCatalog catalog)
    {
        CardInventory current = GetInventory(catalog);
        if (current == null || saveBlocked) return;
        string path = GetSavePath(catalog);
        try
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(current.CaptureSave(), true));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
            else File.Move(path + ".tmp", path);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Could not save cards: {ex.Message}");
        }
    }

    public static CardData[] Restore(CardCatalog catalog)
    {
        CardInventory current = GetInventory(catalog);
        return current == null ? Array.Empty<CardData>() : new List<CardData>(current.Selected).ToArray();
    }
}

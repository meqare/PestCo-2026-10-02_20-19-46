using System;
using System.Collections.Generic;
using UnityEngine;

// Basit kayıt sistemi (PlayerPrefs + JSON). Şimdilik sadece anomali istatistikleri; menüde gösterilecek (sonra).
public static class SaveSystem
{
    private const string Key = "PESTCO_SAVE";

    [Serializable]
    private class SaveData
    {
        public int totalAnomaliesEncountered;
        public List<string> anomaliesSeen = new List<string>();
    }

    private static SaveData data;

    public static int TotalAnomaliesEncountered
    {
        get { EnsureLoaded(); return data.totalAnomaliesEncountered; }
    }

    // Şimdiye kadar karşılaşılan anomali türleri (kopya).
    public static List<string> AnomaliesSeen
    {
        get { EnsureLoaded(); return new List<string>(data.anomaliesSeen); }
    }

    // Anomaliyle karşılaşmayı kaydeder: sayaç artar, tür ilk kez görüldüyse listeye eklenir.
    public static void RegisterAnomaly(string anomalyName)
    {
        EnsureLoaded();
        data.totalAnomaliesEncountered++;
        if (!string.IsNullOrEmpty(anomalyName) && !data.anomaliesSeen.Contains(anomalyName))
            data.anomaliesSeen.Add(anomalyName);
        Save();
    }

    public static void Save()
    {
        EnsureLoaded();
        try
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("SaveSystem: kaydedilemedi: " + e.Message);
        }
    }

    private static void EnsureLoaded()
    {
        if (data != null) return;
        string json = PlayerPrefs.GetString(Key, "");
        data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
        if (data == null) data = new SaveData();
        if (data.anomaliesSeen == null) data.anomaliesSeen = new List<string>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        data = null;
    }
}

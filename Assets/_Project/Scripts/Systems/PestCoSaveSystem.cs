using System;
using System.IO;
using UnityEngine;

// PlayerData'yı persistentDataPath/pestco_save.json dosyasına kaydeder/yükler.
public class PestCoSaveSystem : MonoBehaviour
{
    public static PestCoSaveSystem Instance { get; private set; }

    private const string FileName = "pestco_save.json";
    private string savePath;

    // Singleton'ı kurar ve kayıt dosyası yolunu belirler.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        savePath = Path.Combine(Application.persistentDataPath, FileName);
    }

    // Verilen PlayerData'yı JSON olarak diske yazar.
    public void Save(PlayerData data)
    {
        try
        {
            File.WriteAllText(savePath, JsonUtility.ToJson(data ?? new PlayerData(), true));
        }
        catch (Exception e)
        {
            Debug.LogError("PestCoSaveSystem: kaydedilemedi: " + e.Message);
        }
    }

    // Kayıt dosyasını okur; dosya yoksa veya bozuksa yeni PlayerData döndürür.
    public PlayerData Load()
    {
        try
        {
            if (!File.Exists(savePath)) return new PlayerData();
            PlayerData data = JsonUtility.FromJson<PlayerData>(File.ReadAllText(savePath));
            return data ?? new PlayerData();
        }
        catch (Exception e)
        {
            Debug.LogError("PestCoSaveSystem: yüklenemedi: " + e.Message);
            return new PlayerData();
        }
    }

    // Kayıt dosyası var mı kontrol eder.
    public bool HasSave()
    {
        return File.Exists(savePath);
    }

    // Kayıt dosyasını siler.
    public void DeleteSave()
    {
        try
        {
            if (File.Exists(savePath)) File.Delete(savePath);
        }
        catch (Exception e)
        {
            Debug.LogError("PestCoSaveSystem: silinemedi: " + e.Message);
        }
    }
}

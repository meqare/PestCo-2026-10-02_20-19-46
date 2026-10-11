using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ev sahnesindeki tüm Door bileşenlerine açılma/kapanma seslerini ve AudioSource ayarlarını toplu atar.
// Tekrar çalıştırmak güvenlidir (idempotent).
public static class DoorAudioSetup
{
    const string DoorsGroup = "DoorsWindows";
    const string OpenClipPath = "Assets/_Project/Audio/Doors/door_open.mp3";
    const string CloseClipPath = "Assets/_Project/Audio/Doors/door_close.mp3";

    [MenuItem("PestCo/Ev Kapılarına Ses Ata")]
    public static void Apply()
    {
        var openClip = AssetDatabase.LoadAssetAtPath<AudioClip>(OpenClipPath);
        var closeClip = AssetDatabase.LoadAssetAtPath<AudioClip>(CloseClipPath);
        if (openClip == null || closeClip == null)
        {
            Debug.LogError("[DoorAudio] Ses klipleri bulunamadı. Önce door_open/door_close mp3'lerini Assets/_Project/Audio/Doors altına koyun.");
            return;
        }

        var group = GameObject.Find(DoorsGroup);
        if (group == null)
        {
            Debug.LogError("[DoorAudio] '" + DoorsGroup + "' grubu sahnede yok.");
            return;
        }

        ConfigureImport(OpenClipPath);
        ConfigureImport(CloseClipPath);

        int count = 0;
        foreach (var door in group.GetComponentsInChildren<Door>(true))
        {
            var src = door.GetComponent<AudioSource>();
            if (src == null) src = door.gameObject.AddComponent<AudioSource>();

            // Kapı ayarları: 3D, otomatik çalma kapalı, uzaklık sınırı.
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 1f;
            src.maxDistance = 12f;
            EditorUtility.SetDirty(src);

            // Door alanlarına klipleri ve AudioSource'u bağla.
            var so = new SerializedObject(door);
            so.FindProperty("openSound").objectReferenceValue = openClip;
            so.FindProperty("closeSound").objectReferenceValue = closeClip;
            so.FindProperty("audioSource").objectReferenceValue = src;
            so.ApplyModifiedPropertiesWithoutUndo();
            count++;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[DoorAudio] " + count + " kapıya ses atandı.");
    }

    // Kısa kapı sesleri belleğe önceden yüklensin (Decompress On Load), Vorbis ile küçük boyut.
    static void ConfigureImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) return;

        var settings = importer.defaultSampleSettings;
        settings.loadType = AudioClipLoadType.DecompressOnLoad;
        settings.compressionFormat = AudioCompressionFormat.Vorbis;
        importer.defaultSampleSettings = settings;
        importer.SaveAndReimport();
    }
}

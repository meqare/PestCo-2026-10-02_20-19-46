using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// "Hazır oda ev" (HazirOdaEv) sistemini kaldırır, gizlenmiş eski evi (EskiEv_Gizli) eski haline getirir.
// Menü: PestCo / Ev / Yeni Evi Kaldır, Eski Evi Geri Getir. Her adım Undo'ludur; sahnenin diskteki hali önce yedeklenir.
public static class EskiEviGeriGetir
{
    private const string EskiEvKok = "EskiEv_Gizli";
    private const string YeniEvKok = "HazirOdaEv";
    private const string NavMeshObjesi = "HouseNavMesh";
    private const string EskiNavMeshYolu = "Assets/Scenes/SampleScene/NavMesh-HouseNavMesh.asset";

    [MenuItem("PestCo/Ev/Yeni Evi Kaldır, Eski Evi Geri Getir")]
    public static void Calistir()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Eski Ev", "Önce Play modundan çık, sonra tekrar dene.", "Tamam");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        GameObject eski = null, yeni = null, navObj = null;
        foreach (var go in scene.GetRootGameObjects())
        {
            if (go.name == EskiEvKok) eski = go;
            else if (go.name == YeniEvKok) yeni = go;
            else if (go.name == NavMeshObjesi) navObj = go;
        }

        if (eski == null)
        {
            EditorUtility.DisplayDialog("Eski Ev", $"'{EskiEvKok}' sahnede yok; eski ev zaten geri getirilmiş olabilir.", "Tamam");
            return;
        }

        string ozet = $"- '{EskiEvKok}' içindeki {eski.transform.childCount} nesne köke geri taşınacak\n" +
                      $"- '{YeniEvKok}' {(yeni != null ? "silinecek" : "(sahnede yok)")}\n" +
                      "- HouseNavMesh eski NavMesh dosyasına bağlanacak\n" +
                      "- Sahne kaydedilecek (önce yedeği alınır)";
        if (!EditorUtility.DisplayDialog("Eski evi geri getir", ozet, "Evet, uygula", "İptal")) return;

        // 1) Yedek: sahnenin diskteki hali.
        string scenePath = scene.path;
        if (!string.IsNullOrEmpty(scenePath) && File.Exists(scenePath))
        {
            string backup = "SampleScene.backup_ev_degisim_script_oncesi.unity";
            File.Copy(scenePath, backup, true);
            Debug.Log($"[EskiEv] Yedek: {Path.GetFullPath(backup)}");
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        // 2) Eski evin çocuklarını köke çıkar (kök kimlik dönüşümlüyse dünya konumu değişmez).
        var t = eski.transform;
        if (t.localPosition != Vector3.zero || t.localRotation != Quaternion.identity || t.localScale != Vector3.one)
            Debug.LogWarning($"[EskiEv] '{EskiEvKok}' kimlik dönüşümünde değil; çocuklar dünya konumunu koruyarak taşındı.");

        var cocuklar = new List<Transform>(t.childCount);
        foreach (Transform c in t) cocuklar.Add(c);
        foreach (var c in cocuklar) Undo.SetTransformParent(c, null, "Eski ev nesnelerini köke taşı");
        Undo.DestroyObjectImmediate(eski);

        // 3) Yeni evi kaldır.
        if (yeni != null) Undo.DestroyObjectImmediate(yeni);

        // 4) NavMesh verisini eski ev dosyasına çevir.
        string navSonuc = NavMeshiAyarla(navObj);

        Undo.CollapseUndoOperations(group);

        // 5) Kalıntı kontrolü: yeni eve ait materyal kullanan renderer kaldı mı?
        int kalinti = 0;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.gameObject.scene != scene) continue;
            foreach (var m in r.sharedMaterials)
                if (m != null && m.name.StartsWith("HazirEv_")) { kalinti++; break; }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        bool kaydedildi = EditorSceneManager.SaveScene(scene);

        string rapor = $"{cocuklar.Count} nesne köke taşındı, '{YeniEvKok}' {(yeni != null ? "silindi" : "yoktu")}.\n" +
                       $"NavMesh: {navSonuc}\n" +
                       $"HazirEv_ materyali kullanan kalan renderer: {kalinti}\n" +
                       $"Sahne kaydedildi: {kaydedildi}";
        Debug.Log("[EskiEv] " + rapor);
        EditorUtility.DisplayDialog("Eski ev geri geldi", rapor, "Tamam");
    }

    // HouseNavMesh üzerindeki NavMeshSurface'in veri dosyasını eski ev dosyasına çevirir (reflection: paket tipine derleme bağımlılığı yok).
    private static string NavMeshiAyarla(GameObject navObj)
    {
        if (navObj == null) return "HouseNavMesh objesi bulunamadı";
        var data = AssetDatabase.LoadAssetAtPath<Object>(EskiNavMeshYolu);
        if (data == null) return $"'{EskiNavMeshYolu}' bulunamadı";

        Component surface = null;
        foreach (var c in navObj.GetComponents<Component>())
            if (c != null && c.GetType().Name == "NavMeshSurface") { surface = c; break; }
        if (surface == null) return "NavMeshSurface bileşeni bulunamadı";

        var type = surface.GetType();
        type.GetMethod("RemoveData")?.Invoke(surface, null);
        var so = new SerializedObject(surface);
        var prop = so.FindProperty("m_NavMeshData");
        if (prop == null) return "m_NavMeshData alanı bulunamadı";
        prop.objectReferenceValue = data;
        so.ApplyModifiedProperties();
        type.GetMethod("AddData")?.Invoke(surface, null);
        EditorUtility.SetDirty(surface);
        return "eski NavMesh-HouseNavMesh.asset bağlandı";
    }
}

#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

// PEST CO editör kurulum motoru. Menü komutları bu yardımcıları kullanarak sahneyi tek tıkla kurar.
// Fonksiyonların hepsi tekrar çalıştırılabilir: var olanı bulur, yoksa oluşturur.
public static class PestCoSetup
{
    public const string MaterialsRoot = "Assets/_Project/Materials";
    public const string PrefabsRoot = "Assets/_Project/Prefabs";

    private const string UrpLitShader = "Universal Render Pipeline/Lit";
    private const int FirstUserLayer = 8;

    // ------------------------------------------------------------------
    // MENÜ
    // ------------------------------------------------------------------

    // Motorun çalıştığını test eder: log yazar, küp koyar, 2 saniye sonra siler.
    [MenuItem("PEST CO/0 - Kurulum Testi", priority = 0)]
    private static void SetupTest()
    {
        Debug.Log("PEST CO kurulum motoru hazir");

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "PESTCO_TestCube";
        cube.transform.position = GetSpawnPointInFrontOfSceneView(3f);
        Selection.activeGameObject = cube;

        DelayCall(2.0, () =>
        {
            // Bu arada kullanıcı silmiş olabilir.
            if (cube != null)
            {
                Object.DestroyImmediate(cube);
                Debug.Log("PEST CO test kupu silindi");
            }
        });
    }

    // ------------------------------------------------------------------
    // OBJE
    // ------------------------------------------------------------------

    // Boş bir obje oluşturur, ebeveynine bağlar, dünya konumuna koyar (Ctrl+Z ile geri alınabilir).
    public static GameObject CreateObject(string ad, Transform ebeveyn, Vector3 konum)
    {
        var obj = new GameObject(ad);
        Undo.RegisterCreatedObjectUndo(obj, "Create " + ad);
        if (ebeveyn != null)
            obj.transform.SetParent(ebeveyn, false);
        obj.transform.position = konum;
        return obj;
    }

    // Bileşen varsa onu döndürür, yoksa ekler (Ctrl+Z ile geri alınabilir).
    public static T AddOrGet<T>(GameObject hedef) where T : Component
    {
        if (hedef.TryGetComponent(out T mevcut))
            return mevcut;
        return Undo.AddComponent<T>(hedef);
    }

    // Objeyi ve sahnesini "kaydedilmedi" olarak işaretler, Ctrl+S ile kaydedilsin.
    public static void MarkDirty(GameObject obj)
    {
        if (obj == null) return;
        EditorUtility.SetDirty(obj);

        if (PrefabUtility.IsPartOfPrefabInstance(obj))
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);

        // Play modunda sahne işaretlenemez (değişiklikler zaten kaybolur).
        if (!Application.isPlaying && obj.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(obj.scene);
    }

    // ------------------------------------------------------------------
    // LAYER / TAG
    // ------------------------------------------------------------------

    // Layer yoksa TagManager'daki ilk boş kullanıcı slotuna (8-31) ekler.
    public static void EnsureLayer(string ad)
    {
        if (string.IsNullOrEmpty(ad) || LayerMask.NameToLayer(ad) >= 0) return;

        SerializedObject tagManager = LoadTagManager();
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = FirstUserLayer; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = ad;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log($"PEST CO: '{ad}' layer'i olusturuldu (index {i})");
                return;
            }
        }

        Debug.LogError($"PEST CO: '{ad}' layer'i eklenemedi, 8-31 arasi bos slot yok.");
    }

    // Tag yoksa TagManager'a ekler.
    public static void EnsureTag(string ad)
    {
        if (string.IsNullOrEmpty(ad)) return;
        if (Array.IndexOf(InternalEditorUtility.tags, ad) >= 0) return;

        SerializedObject tagManager = LoadTagManager();
        SerializedProperty tags = tagManager.FindProperty("tags");

        int index = tags.arraySize;
        tags.InsertArrayElementAtIndex(index);
        tags.GetArrayElementAtIndex(index).stringValue = ad;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
        Debug.Log($"PEST CO: '{ad}' tag'i olusturuldu");
    }

    // ProjectSettings/TagManager.asset dosyasını düzenlenebilir olarak açar.
    private static SerializedObject LoadTagManager()
    {
        Object asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        return new SerializedObject(asset);
    }

    // ------------------------------------------------------------------
    // MATERYAL / PREFAB
    // ------------------------------------------------------------------

    // URP/Lit materyal oluşturur ya da varsa günceller. "Pests/M_Roach" -> Assets/_Project/Materials/Pests/M_Roach.mat
    public static Material CreateURPMaterial(string yol, Color renk, bool emissionAcik)
    {
        string path = ToAssetPath(yol, MaterialsRoot, ".mat");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        bool yeni = mat == null;

        if (yeni)
        {
            Shader shader = Shader.Find(UrpLitShader);
            if (shader == null)
            {
                Debug.LogError($"PEST CO: '{UrpLitShader}' shader'i bulunamadi. URP kurulu mu?");
                return null;
            }
            mat = new Material(shader);
        }
        else
        {
            Undo.RecordObject(mat, "Update material");
        }

        mat.SetColor("_BaseColor", renk);

        if (emissionAcik)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", renk);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        if (yeni)
        {
            EnsureFolder(System.IO.Path.GetDirectoryName(path));
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();
        return mat;
    }

    // Sahnedeki objeyi prefab olarak kaydeder (varsa üzerine yazar) ve sahnedeki objeyi prefaba bağlar.
    // "Pests/Roach" -> Assets/_Project/Prefabs/Pests/Roach.prefab
    public static void SavePrefab(GameObject obj, string yol)
    {
        if (obj == null)
        {
            Debug.LogError("PEST CO: SavePrefab'a bos obje verildi.");
            return;
        }

        string path = ToAssetPath(yol, PrefabsRoot, ".prefab");
        EnsureFolder(System.IO.Path.GetDirectoryName(path));

        GameObject prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(obj, path, InteractionMode.UserAction, out bool ok);
        if (ok && prefab != null)
            Debug.Log($"PEST CO: prefab kaydedildi -> {path}", prefab);
        else
            Debug.LogError($"PEST CO: prefab kaydedilemedi -> {path}");
    }

    // ------------------------------------------------------------------
    // INSPECTOR ALANI YAZMA
    // ------------------------------------------------------------------

    // [SerializeField] private bir alanı Inspector üzerinden yazar (Reflection yok, kaydedilir, Ctrl+Z çalışır).
    // Örnek: SetPrivateField(sprayTool, "range", 4f); SetPrivateField(sprayTool, "cam", Camera.main);
    public static void SetPrivateField(Component hedef, string alanAdi, object deger)
    {
        if (hedef == null)
        {
            Debug.LogError($"PEST CO: SetPrivateField '{alanAdi}' icin hedef bos.");
            return;
        }

        var so = new SerializedObject(hedef);
        SerializedProperty prop = so.FindProperty(alanAdi);
        if (prop == null)
        {
            Debug.LogError($"PEST CO: {hedef.GetType().Name} icinde '{alanAdi}' alani yok. " +
                           "Isim birebir ayni mi ve [SerializeField] var mi?", hedef);
            return;
        }

        if (!TrySetValue(prop, deger))
        {
            Debug.LogError($"PEST CO: '{alanAdi}' ({prop.propertyType}) alanina " +
                           $"{(deger == null ? "null" : deger.GetType().Name)} yazilamadi.", hedef);
            return;
        }

        so.ApplyModifiedProperties();
        MarkDirty(hedef.gameObject);
    }

    // Değeri, alanın türüne göre doğru SerializedProperty alanına yazar. Diziler/listeler de desteklenir.
    private static bool TrySetValue(SerializedProperty prop, object deger)
    {
        // Dizi ve List<T> (string tek değer sayılır)
        if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
        {
            if (deger is not IList liste) return false;
            prop.arraySize = liste.Count;
            for (int i = 0; i < liste.Count; i++)
                if (!TrySetValue(prop.GetArrayElementAtIndex(i), liste[i])) return false;
            return true;
        }

        switch (prop.propertyType)
        {
            case SerializedPropertyType.Integer:
                prop.longValue = Convert.ToInt64(deger);
                return true;
            case SerializedPropertyType.Boolean:
                prop.boolValue = Convert.ToBoolean(deger);
                return true;
            case SerializedPropertyType.Float:
                prop.doubleValue = Convert.ToDouble(deger);
                return true;
            case SerializedPropertyType.String:
                prop.stringValue = deger as string ?? deger?.ToString() ?? string.Empty;
                return true;
            case SerializedPropertyType.Enum:
                prop.enumValueFlag = Convert.ToInt32(deger);
                return true;
            case SerializedPropertyType.LayerMask:
                prop.intValue = deger is LayerMask mask ? mask.value : Convert.ToInt32(deger);
                return true;
            case SerializedPropertyType.ObjectReference:
                if (deger != null && deger is not Object) return false;
                prop.objectReferenceValue = deger as Object;
                return true;
            case SerializedPropertyType.Color:
                if (deger is not Color c) return false;
                prop.colorValue = c;
                return true;
            case SerializedPropertyType.Vector2:
                if (deger is not Vector2 v2) return false;
                prop.vector2Value = v2;
                return true;
            case SerializedPropertyType.Vector3:
                if (deger is not Vector3 v3) return false;
                prop.vector3Value = v3;
                return true;
            case SerializedPropertyType.Vector4:
                if (deger is not Vector4 v4) return false;
                prop.vector4Value = v4;
                return true;
            case SerializedPropertyType.Quaternion:
                if (deger is not Quaternion q) return false;
                prop.quaternionValue = q;
                return true;
            case SerializedPropertyType.Rect:
                if (deger is not Rect r) return false;
                prop.rectValue = r;
                return true;
            case SerializedPropertyType.Bounds:
                if (deger is not Bounds b) return false;
                prop.boundsValue = b;
                return true;
            case SerializedPropertyType.AnimationCurve:
                if (deger is not AnimationCurve curve) return false;
                prop.animationCurveValue = curve;
                return true;
            case SerializedPropertyType.Gradient:
                if (deger is not Gradient g) return false;
                prop.gradientValue = g;
                return true;
            default:
                return false;
        }
    }

    // ------------------------------------------------------------------
    // KÜÇÜK YARDIMCILAR
    // ------------------------------------------------------------------

    // Klasör yolunu (Assets/a/b/c) adım adım oluşturur.
    public static void EnsureFolder(string klasorYolu)
    {
        klasorYolu = klasorYolu.Replace('\\', '/').TrimEnd('/');
        if (string.IsNullOrEmpty(klasorYolu) || AssetDatabase.IsValidFolder(klasorYolu)) return;

        string parent = System.IO.Path.GetDirectoryName(klasorYolu)?.Replace('\\', '/');
        string name = System.IO.Path.GetFileName(klasorYolu);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    // Kısa yolu tam asset yoluna çevirir: "Pests/M_Roach" -> "Assets/_Project/Materials/Pests/M_Roach.mat".
    // "Assets/..." ile başlayan yol olduğu gibi kullanılır.
    public static string ToAssetPath(string yol, string kokKlasor, string uzanti)
    {
        yol = yol.Replace('\\', '/').TrimStart('/');
        if (!yol.StartsWith("Assets/"))
            yol = kokKlasor + "/" + yol;
        if (!yol.EndsWith(uzanti, StringComparison.OrdinalIgnoreCase))
            yol += uzanti;
        return yol;
    }

    // Scene penceresinin baktığı yerde, kameranın 'mesafe' metre önünde bir nokta verir.
    public static Vector3 GetSpawnPointInFrontOfSceneView(float mesafe)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null || view.camera == null) return Vector3.zero;
        Transform cam = view.camera.transform;
        return cam.position + cam.forward * mesafe;
    }

    // Editörde 'saniye' kadar sonra bir işi çalıştırır (Play modu gerekmez).
    public static void DelayCall(double saniye, Action islem)
    {
        double hedefZaman = EditorApplication.timeSinceStartup + saniye;
        EditorApplication.CallbackFunction kontrol = null;
        kontrol = () =>
        {
            if (EditorApplication.timeSinceStartup < hedefZaman) return;
            EditorApplication.update -= kontrol;
            islem?.Invoke();
        };
        EditorApplication.update += kontrol;
    }
}
#endif

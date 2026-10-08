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

    // Adı "PestNest" ile başlayan tüm yuvaları denetler ve eksiklerini tamamlar:
    // layer = Nest, PestNest (+ can/doğurma değerleri), katı collider, UVRevealable (+ referanslar).
    // Spreyin Nest layer'ını vurmasını da sağlar.
    [MenuItem("PEST CO/Fix - Tum Yuvalari Duzelt", priority = 29)]
    [MenuItem("PEST CO/Hafta 3/Fix - Tum Yuvalari Duzelt", priority = 30)]
    private static void FixAllNests()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("PEST CO: Yuva duzeltme Play modunda calismaz, once Play'i durdur.");
            return;
        }

        EnsureLayer("Nest");
        int nestLayer = LayerMask.NameToLayer("Nest");
        if (nestLayer < 0) return;

        Material nestMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialsRoot + "/M_Nest.mat");
        int found = 0, fixedCount = 0;

        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!go.name.StartsWith("PestNest")) continue;
            found++;

            var changes = new System.Collections.Generic.List<string>();
            Collider existingCollider = go.GetComponent<Collider>();
            Debug.Log($"PEST CO: [{go.name}] layer={LayerMask.LayerToName(go.layer)}, " +
                      $"PestNest={go.GetComponent<PestNest>() != null}, " +
                      $"collider={(existingCollider != null ? existingCollider.GetType().Name + (existingCollider.isTrigger ? " (TRIGGER)" : "") : "YOK")}, " +
                      $"UVRevealable={go.GetComponent<UVRevealable>() != null}", go);

            // Layer: spreyin hedef listesinde olacak Nest layer'ı.
            if (go.layer != nestLayer)
            {
                Undo.RecordObject(go, "Fix Nest Layer");
                go.layer = nestLayer;
                changes.Add("layer=Nest");
            }

            // PestNest script'i: can, hasar ve böcek doğurma.
            PestNest nest = go.GetComponent<PestNest>();
            if (nest == null)
            {
                nest = Undo.AddComponent<PestNest>(go);
                changes.Add("PestNest eklendi");
            }

            // Standart yuva değerleri: can 60, 4 sn'de bir böcek, en fazla 12 canlı böcek.
            if (SetNestDefaults(nest))
                changes.Add("can/dogurma degerleri");

            // Collider: yoksa küre ekle; varsa sadece trigger'ı kapat (mevcut capsule vb. korunur).
            if (existingCollider == null)
            {
                var sphere = Undo.AddComponent<SphereCollider>(go);
                sphere.radius = 0.25f;
                sphere.isTrigger = false;
                changes.Add("SphereCollider eklendi");
            }
            else if (existingCollider.isTrigger)
            {
                Undo.RecordObject(existingCollider, "Fix Nest Collider");
                existingCollider.isTrigger = false;
                changes.Add("IsTrigger kapatildi");
            }

            // UVRevealable: UV fenerle parlama. Kendi renderer'ı ve varsa MarkerRoot bağlanır.
            if (go.GetComponent<UVRevealable>() == null)
            {
                var uv = Undo.AddComponent<UVRevealable>(go);
                var so = new SerializedObject(uv);

                Renderer ownRenderer = go.GetComponent<Renderer>();
                // Neon yeşil (#39FF14), HDR: biraz parlasın diye 1.5 ile çarpılır.
                so.FindProperty("glowColor").colorValue = (Color)new Color32(0x39, 0xFF, 0x14, 0xFF) * 1.5f;

                SerializedProperty glow = so.FindProperty("glowRenderers");
                glow.arraySize = ownRenderer != null ? 1 : 0;
                if (ownRenderer != null) glow.GetArrayElementAtIndex(0).objectReferenceValue = ownRenderer;

                Transform marker = go.transform.Find("MarkerRoot");
                SerializedProperty markers = so.FindProperty("showWhenRevealed");
                markers.arraySize = marker != null ? 1 : 0;
                if (marker != null) markers.GetArrayElementAtIndex(0).objectReferenceValue = marker.gameObject;

                so.ApplyModifiedProperties();
                changes.Add("UVRevealable eklendi");
            }

            // Materyal: emission'sız varsayılan "Lit" ile UV parlaması görünmez, M_Nest verilir.
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null && nestMaterial != null && rend.sharedMaterial != null
                && !rend.sharedMaterial.IsKeywordEnabled("_EMISSION"))
            {
                Undo.RecordObject(rend, "Fix Nest Material");
                rend.sharedMaterial = nestMaterial;
                changes.Add("materyal=M_Nest");
            }

            if (changes.Count > 0)
            {
                fixedCount++;
                MarkDirty(go);
                Debug.Log($"PEST CO: [{go.name}] duzeltildi -> {string.Join(", ", changes)}", go);
            }
        }

        // Asıl sebep: sprey sadece targetLayers'taki objeleri tarar. Nest yoksa ekle.
        int sprayFixed = 0;
        foreach (SprayTool spray in Object.FindObjectsByType<SprayTool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(spray);
            SerializedProperty mask = so.FindProperty("targetLayers");
            if ((mask.intValue & (1 << nestLayer)) != 0) continue;

            mask.intValue |= 1 << nestLayer;
            so.ApplyModifiedProperties();
            MarkDirty(spray.gameObject);
            sprayFixed++;
            Debug.Log("PEST CO: SprayTool hedef layer'larina 'Nest' eklendi.", spray);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"PEST CO: {found} yuva bulundu, {fixedCount} adet yuva duzeltildi" +
                  (sprayFixed > 0 ? $", {sprayFixed} SprayTool guncellendi." : ".") +
                  " Ctrl+S ile kaydet.");
    }

    // Yuvanın can ve doğurma değerlerini standart yapar; bir şey değiştiyse true döner.
    private static bool SetNestDefaults(PestNest nest)
    {
        var so = new SerializedObject(nest);
        bool changed = false;
        changed |= SetFloat(so, "maxHealth", 60f);
        changed |= SetFloat(so, "spawnInterval", 4f);

        SerializedProperty maxPests = so.FindProperty("maxActivePests");
        if (maxPests.intValue != 12)
        {
            maxPests.intValue = 12;
            changed = true;
        }

        so.ApplyModifiedProperties();
        return changed;
    }

    // Float alanı farklıysa yazar; değiştiyse true döner.
    private static bool SetFloat(SerializedObject so, string field, float value)
    {
        SerializedProperty prop = so.FindProperty(field);
        if (Mathf.Approximately(prop.floatValue, value)) return false;
        prop.floatValue = value;
        return true;
    }

    // ------------------------------------------------------------------
    // FARE
    // ------------------------------------------------------------------

    private const string MouseRootName = "P_Mouse_Root";
    private const string MousePrefabPath = PrefabsRoot + "/Pests/P_Mouse.prefab";
    // Kök obje zeminde durur (agent Base Offset = 0); model gövde yarıçapı kadar yukarı kaldırılır ki gömülmesin.
    private const float MouseModelLift = 0.075f;

    // Eski/bozuk fareyi siler; primitiflerden yeni bir fare kurar, prefab olarak kaydeder ve sahnede bir tane bırakır.
    [MenuItem("PEST CO/Fix - Fareyi Bastan Yap", priority = 40)]
    private static void RebuildMouse()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("PEST CO: Fare Play modunda kurulamaz, once Play'i durdur.");
            return;
        }

        // Bug agent tipi NavMesh'in pişirildiği tip; yoksa fare yürüyemez.
        int bugAgentId = FindAgentTypeId("Bug");
        if (bugAgentId == int.MinValue)
        {
            Debug.LogError("PEST CO: 'Bug' agent tipi yok. Window > AI > Navigation > Agents'tan ekle " +
                           "(Radius 0.06, Height 0.12, Step Height 0.04, Max Slope 45) ve NavMesh'i yeniden pisir.");
            return;
        }

        EnsureLayer("Pest");
        int pestLayer = LayerMask.NameToLayer("Pest");

        // a) Temizlik.
        int deleted = DeleteMouseLeftovers();

        // b) Kök obje.
        var root = new GameObject(MouseRootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Mouse");
        root.layer = pestLayer;
        root.transform.position = FindMouseSpawnPoint(bugAgentId);
        root.transform.localScale = Vector3.one;

        // c) Gövde collider'ı (sprey bunu bulur).
        var box = root.AddComponent<BoxCollider>();
        box.isTrigger = false;
        box.size = new Vector3(0.20f, 0.15f, 0.30f);
        box.center = new Vector3(0f, 0.05f, 0f);

        // d) NavMeshAgent.
        var agent = root.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agent.agentTypeID = bugAgentId;
        agent.baseOffset = 0f;
        agent.speed = 1.5f;
        agent.angularSpeed = 720f;
        agent.acceleration = 20f;
        agent.stoppingDistance = 0f;
        agent.autoBraking = false;
        agent.radius = 0.06f;
        agent.height = 0.12f;
        agent.obstacleAvoidanceType = UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        agent.avoidancePriority = 50;
        agent.autoTraverseOffMeshLink = true;
        agent.autoRepath = true;
        agent.areaMask = UnityEngine.AI.NavMesh.AllAreas;

        // Adım 2: primitif model (Pest Awake'te renderer'ları topladığı için Pest'ten önce kurulur).
        BuildMouseModel(root.transform, pestLayer);

        // e) Pest script'i ve fare ayarları.
        var pest = root.AddComponent<Pest>();
        ConfigureMousePest(pest);

        // Sprey Pest layer'ını da vursun (hamamböceği Bug layer'ında, fare Pest'te).
        int sprayFixed = AddLayerToSprayTargets(pestLayer);

        // Adım 3: prefab kaydet (varsa üzerine yazar); sahnedeki kopya prefab'a bağlı kalır.
        SavePrefab(root, MousePrefabPath);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log($"✅ P_Mouse prefab olusturuldu - primitiflerle ({deleted} eski obje silindi" +
                  (sprayFixed > 0 ? ", sprey artik 'Pest' layer'ini da vuruyor" : "") + "). Ctrl+S ile kaydet.", root);
    }

    // Adı P_Mouse_Root veya Meshy_AI_Wh ile başlayan tüm kök/alt objeleri siler; kaç tane sildiğini döner.
    private static int DeleteMouseLeftovers()
    {
        int deleted = 0;
        foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go == null) continue; // ebeveyniyle birlikte silinmiş olabilir
            if (!go.name.StartsWith(MouseRootName) && !go.name.StartsWith("Meshy_AI_Wh")) continue;
            Undo.DestroyObjectImmediate(go);
            deleted++;
        }
        return deleted;
    }

    // Verilen isimdeki NavMesh agent tipinin ID'sini döner; yoksa int.MinValue.
    private static int FindAgentTypeId(string ad)
    {
        int count = UnityEngine.AI.NavMesh.GetSettingsCount();
        for (int i = 0; i < count; i++)
        {
            int id = UnityEngine.AI.NavMesh.GetSettingsByIndex(i).agentTypeID;
            if (UnityEngine.AI.NavMesh.GetSettingsNameFromID(id) == ad)
                return id;
        }
        return int.MinValue;
    }

    // (0, 0.1, 0) yakınında Bug NavMesh'i varsa orayı, yoksa oyuncunun yanını döner.
    private static Vector3 FindMouseSpawnPoint(int agentTypeId)
    {
        var filter = new UnityEngine.AI.NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = UnityEngine.AI.NavMesh.AllAreas };
        Vector3 wanted = new Vector3(0f, 0.1f, 0f);
        if (UnityEngine.AI.NavMesh.SamplePosition(wanted, out UnityEngine.AI.NavMeshHit hit, 1f, filter))
            return wanted;

        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player != null)
        {
            Vector3 near = player.transform.position + player.transform.forward * 1.5f;
            if (UnityEngine.AI.NavMesh.SamplePosition(near, out hit, 3f, filter))
                return hit.position;
        }

        Debug.LogWarning("PEST CO: Fare icin yakinda Bug NavMesh'i bulunamadi, (0, 0.1, 0)'a kondu.");
        return wanted;
    }

    // Kökün altına primitiflerden fare modelini kurar: collider'sız, gölgesiz, ortak M_Mouse materyali.
    private static void BuildMouseModel(Transform root, int layer)
    {
        Material mat = CreateURPMaterial("M_Mouse", new Color32(60, 60, 65, 255), false);
        if (mat != null)
        {
            mat.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(mat);
        }

        AddMousePart(root, "Body", PrimitiveType.Sphere, new Vector3(0.20f, 0.15f, 0.30f), new Vector3(0f, 0f, 0f), Vector3.zero, layer, mat);
        AddMousePart(root, "Head", PrimitiveType.Sphere, new Vector3(0.15f, 0.13f, 0.15f), new Vector3(0f, 0f, 0.20f), Vector3.zero, layer, mat);
        AddMousePart(root, "Snout", PrimitiveType.Sphere, new Vector3(0.06f, 0.06f, 0.08f), new Vector3(0f, -0.02f, 0.30f), Vector3.zero, layer, mat);
        AddMousePart(root, "Nose", PrimitiveType.Sphere, new Vector3(0.03f, 0.03f, 0.03f), new Vector3(0f, -0.02f, 0.35f), Vector3.zero, layer, mat);
        AddMousePart(root, "EarL", PrimitiveType.Sphere, new Vector3(0.10f, 0.03f, 0.10f), new Vector3(-0.08f, 0.10f, 0.18f), Vector3.zero, layer, mat);
        AddMousePart(root, "EarR", PrimitiveType.Sphere, new Vector3(0.10f, 0.03f, 0.10f), new Vector3(0.08f, 0.10f, 0.18f), Vector3.zero, layer, mat);
        AddMousePart(root, "EyeL", PrimitiveType.Sphere, new Vector3(0.03f, 0.03f, 0.03f), new Vector3(-0.06f, 0.04f, 0.27f), Vector3.zero, layer, mat);
        AddMousePart(root, "EyeR", PrimitiveType.Sphere, new Vector3(0.03f, 0.03f, 0.03f), new Vector3(0.06f, 0.04f, 0.27f), Vector3.zero, layer, mat);
        AddMousePart(root, "Tail", PrimitiveType.Cylinder, new Vector3(0.02f, 0.25f, 0.02f), new Vector3(0f, 0f, -0.25f), new Vector3(90f, 0f, 0f), layer, mat);
    }

    // Tek bir model parçası: primitif oluşturur, collider'ını siler, gölgeyi kapatır, materyali takar.
    private static void AddMousePart(Transform root, string ad, PrimitiveType type, Vector3 scale, Vector3 pos,
        Vector3 euler, int layer, Material mat)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        Undo.RegisterCreatedObjectUndo(part, "Create " + ad);
        part.name = ad;
        part.layer = layer;
        Object.DestroyImmediate(part.GetComponent<Collider>());

        part.transform.SetParent(root, false);
        part.transform.localPosition = pos + Vector3.up * MouseModelLift;
        part.transform.localRotation = Quaternion.Euler(euler);
        part.transform.localScale = scale;

        var rend = part.GetComponent<MeshRenderer>();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (mat != null) rend.sharedMaterial = mat;
    }

    // Fare için Pest alanlarını yazar; ceset materyalini hamamböceği prefab'ından kopyalar.
    private static void ConfigureMousePest(Pest pest)
    {
        var so = new SerializedObject(pest);
        so.FindProperty("species").enumValueIndex = (int)Pest.PestSpecies.Mouse;
        so.FindProperty("speciesName").stringValue = "Fare";
        so.FindProperty("maxHealth").floatValue = 60f;
        so.FindProperty("wanderRadius").floatValue = 3f;
        so.FindProperty("minIdleWait").floatValue = 0.5f;
        so.FindProperty("maxIdleWait").floatValue = 2f;
        so.FindProperty("moveSpeed").floatValue = 1.5f;
        so.FindProperty("fleeSpeed").floatValue = 3.5f;
        so.FindProperty("detectRadius").floatValue = 4f;
        so.FindProperty("fleeDistance").floatValue = 3f;
        so.FindProperty("calmDownTime").floatValue = 2f;
        so.FindProperty("isSprayResistant").boolValue = true;
        so.FindProperty("spraySlowDuration").floatValue = 2f;
        so.FindProperty("spraySlowAmount").floatValue = 0.3f;
        so.FindProperty("resistGraceTime").floatValue = 0.6f;

        // Sönme için şeffaf ortak materyal (hamamböceği ile aynısı).
        var roach = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsRoot + "/Pest_Hamambocegi.prefab");
        if (roach != null && roach.TryGetComponent(out Pest roachPest))
        {
            var roachSo = new SerializedObject(roachPest);
            so.FindProperty("corpseFadeMaterial").objectReferenceValue =
                roachSo.FindProperty("corpseFadeMaterial").objectReferenceValue;
        }

        so.ApplyModifiedProperties();
    }

    // Sahnedeki tüm SprayTool'ların hedef layer'larına verilen layer'ı ekler; kaç tanesini değiştirdiğini döner.
    private static int AddLayerToSprayTargets(int layer)
    {
        int changed = 0;
        foreach (SprayTool spray in Object.FindObjectsByType<SprayTool>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(spray);
            SerializedProperty mask = so.FindProperty("targetLayers");
            if ((mask.intValue & (1 << layer)) != 0) continue;
            mask.intValue |= 1 << layer;
            so.ApplyModifiedProperties();
            MarkDirty(spray.gameObject);
            changed++;
        }
        return changed;
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
    // KAPILAR (Hafta 5)
    // ------------------------------------------------------------------

    private const string DoorPrefabPath = "Assets/_Project/Prefabs/Interactables/Door.prefab";
    private const string DoorInstancePrefix = "InteractableDoor_";

    // Sahnedeki kapı çerçevelerinin (Door_* + Frame_A/B/Top) boşluğuna E ile açılan kapı yerleştirir.
    [MenuItem("PEST CO/Hafta 5/2 - Kapilari Kur", priority = 52)]
    private static void SetupDoors()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("PEST CO: Kapilari kurmak Play modunda calismaz, once Play'i durdur.");
            return;
        }

        EnsureLayer("Interactable");
        GameObject prefab = EnsureDoorPrefab();
        if (prefab == null) return;
        int layer = LayerMask.NameToLayer("Interactable");

        // Önce eski kapılar silinir (menü tekrar çalıştırılabilsin).
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null && t.name.StartsWith(DoorInstancePrefix))
                Undo.DestroyObjectImmediate(t.gameObject);
        }

        // Çerçeveleri topla: Frame_A, Frame_B ve Frame_Top çocuğu olan "Door_*" grupları.
        var frames = new System.Collections.Generic.List<Transform>();
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (t.name.StartsWith("Door_") && t.Find("Frame_A") != null && t.Find("Frame_B") != null && t.Find("Frame_Top") != null)
                frames.Add(t);
        }

        int placed = 0;
        foreach (Transform group in frames)
        {
            Bounds a = group.Find("Frame_A").GetComponent<Renderer>().bounds;
            Bounds b = group.Find("Frame_B").GetComponent<Renderer>().bounds;
            Bounds top = group.Find("Frame_Top").GetComponent<Renderer>().bounds;

            // Geçiş ekseni: boşluk Z'de ince (duvar X boyunca) ya da X'te ince.
            Vector3 mid = (a.center + b.center) * 0.5f;
            bool alongZ = Mathf.Abs(a.center.x - b.center.x) > Mathf.Abs(a.center.z - b.center.z);
            float span = alongZ ? Mathf.Abs(a.center.x - b.center.x) : Mathf.Abs(a.center.z - b.center.z);
            float frameThickness = alongZ ? a.size.x : a.size.z;
            float width = span - frameThickness - 0.02f;          // çerçeve içi net genişlik
            float height = top.min.y - 0.02f;                      // üst çerçevenin altı

            var door = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(door, "Kapi yerlestir");
            door.name = DoorInstancePrefix + group.name.Replace("Door_", "");
            door.transform.position = new Vector3(mid.x, 0f, mid.z);
            door.transform.rotation = alongZ ? Quaternion.identity : Quaternion.Euler(0f, 90f, 0f);

            // Kapı panelini boşluğa göre boyutlandır; menteşe sol kenarda.
            Transform pivot = door.transform.Find("DoorPivot");
            Transform mesh = pivot.Find("DoorMesh");
            pivot.localPosition = new Vector3(-width * 0.5f, 0f, 0f);
            mesh.localScale = new Vector3(width, height, 0.05f);
            mesh.localPosition = new Vector3(width * 0.5f, height * 0.5f, 0f);

            foreach (Transform t in door.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = layer;
            MarkDirty(door);
            placed++;
        }

        if (placed == 0)
        {
            Debug.LogWarning("PEST CO: Kapi cercevesi bulunamadi (Door_* + Frame_A/Frame_B/Frame_Top).");
            return;
        }
        Debug.Log($"\u2705 {placed} kapi yerlestirildi");
    }

    // Door.prefab yoksa oluşturur: Door (AudioSource + Door.cs) > DoorPivot > DoorMesh (kutu, collider panelde).
    // Collider panelde: kök objede olsaydı kapı açılınca boşluğu kapatmaya devam ederdi.
    private static GameObject EnsureDoorPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
        if (existing != null) return existing;

        int layer = LayerMask.NameToLayer("Interactable");
        Material mat = CreateURPMaterial("Doors/M_Door", new Color(0.29f, 0.19f, 0.12f), false);
        if (mat != null) mat.SetFloat("_Smoothness", 0.2f);

        var root = new GameObject("Door");
        var src = root.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.minDistance = 1f;
        src.maxDistance = 12f;
        src.rolloffMode = AudioRolloffMode.Linear;

        var pivot = new GameObject("DoorPivot").transform;
        pivot.SetParent(root.transform, false);
        pivot.localPosition = new Vector3(-0.74f, 0f, 0f);          // sol kenar (menteşe)

        var meshGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        meshGo.name = "DoorMesh";
        meshGo.transform.SetParent(pivot, false);
        meshGo.transform.localScale = new Vector3(1.48f, 2.2f, 0.05f);
        meshGo.transform.localPosition = new Vector3(0.74f, 1.1f, 0f);
        var r = meshGo.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        if (mat != null) r.sharedMaterial = mat;

        var door = root.AddComponent<Door>();
        var so = new SerializedObject(door);
        so.FindProperty("doorPivot").objectReferenceValue = pivot;
        so.FindProperty("audioSource").objectReferenceValue = src;
        so.ApplyModifiedPropertiesWithoutUndo();

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;

        EnsureFolder("Assets/_Project/Prefabs/Interactables");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DoorPrefabPath);
        Object.DestroyImmediate(root);
        Debug.Log($"PEST CO: prefab kaydedildi -> {DoorPrefabPath}", prefab);
        return prefab;
    }

    // ------------------------------------------------------------------
    // DEBUG HUD (Hafta 6)
    // ------------------------------------------------------------------

    // Canvas altına sağ üst köşede para ve iş sayısı gösteren DebugHUD kurar (tekrar çalıştırılabilir).
    [MenuItem("PEST CO/Hafta 6/2 - DebugHUD Kur", priority = 62)]
    private static void SetupDebugHUD()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("PEST CO: DebugHUD kurmak Play modunda calismaz, once Play'i durdur.");
            return;
        }

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("PEST CO: Sahnede Canvas yok, DebugHUD kurulamadi.");
            return;
        }

        // Eski DebugHUD varsa sil.
        foreach (DebugHUD eski in Object.FindObjectsByType<DebugHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Undo.DestroyObjectImmediate(eski.gameObject);

        var root = new GameObject("DebugHUD", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(root, "DebugHUD kur");
        root.transform.SetParent(canvas.transform, false);
        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var money = CreateDebugText(root.transform, "MoneyText", new Vector2(-20f, -20f));
        var jobs = CreateDebugText(root.transform, "JobsText", new Vector2(-20f, -60f));

        var hud = root.AddComponent<DebugHUD>();
        SetPrivateField(hud, "moneyText", money);
        SetPrivateField(hud, "jobsText", jobs);

        MarkDirty(root);
        Debug.Log("✅ DebugHUD kuruldu (sag ust kose)");
    }

    // Sağ üste yaslı, sağa hizalı bir TextMeshPro yazısı oluşturur.
    private static TMPro.TextMeshProUGUI CreateDebugText(Transform parent, string ad, Vector2 konum)
    {
        var go = new GameObject(ad, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = konum;
        rect.sizeDelta = new Vector2(320f, 36f);

        var text = go.AddComponent<TMPro.TextMeshProUGUI>();
        text.fontSize = 28f;
        text.alignment = TMPro.TextAlignmentOptions.TopRight;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = ad;
        return text;
    }

    // ------------------------------------------------------------------
    // KONTRAT SİSTEMİ (Hafta 6)
    // ------------------------------------------------------------------

    // Geçici bir ContractGenerator ile 3 rastgele kontrat üretip Console'a yazar.
    [MenuItem("PEST CO/Hafta 6/3 - ContractGenerator Test", priority = 63)]
    private static void TestContractGenerator()
    {
        var go = new GameObject("ContractGeneratorTest") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var gen = go.AddComponent<ContractGenerator>();
            foreach (ContractData c in gen.GenerateContracts(3))
            {
                Debug.Log($"📄 {c.contractId} | {c.title} | {c.district} | {c.address} | " +
                          $"zorluk {c.difficulty} | ödül {c.baseReward} | {c.pestSpecies} | " +
                          $"{c.requiredTime}sn | {c.modifier}");
            }
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
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

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Perşembe — İlk oda / beyaz kutu mutfak greybox generator.
///
/// Kullanım:
/// Unity'de bu dosyayı Assets/_Project/Editor/CreateGreyboxKitchen.cs yoluna koy.
/// Menüden: Tools > _Project > Create Greybox Kitchen
///
/// Kurallar:
/// - Sadece Unity Cube primitive kullanır.
/// - Scale ile ezilmiş küplerden oluşur, dış model yoktur.
/// - Tek materyal üretir: Assets/_Project/Materials/Greybox.mat
/// - Üretilen tüm parçalara aynı Greybox materyalini atar.
/// - Zemin 8x8'dir.
/// - 4 duvar vardır, ön duvarda 1 kapı boşluğu bırakılır.
/// </summary>
public static class CreateGreyboxKitchen
{
    private const string RootName = "GREYBOX_Kitchen_8x8";
    private const string MaterialPath = "Assets/_Project/Materials/Greybox.mat";

    private static readonly Color GreyboxColor = new Color(0.55f, 0.55f, 0.55f, 1f);

    [MenuItem("Tools/_Project/Create Greybox Kitchen")]
    public static void CreateKitchen()
    {
        Material greybox = GetOrCreateGreyboxMaterial();

        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
        {
            bool delete = EditorUtility.DisplayDialog(
                "Greybox Kitchen zaten var",
                "Sahnede GREYBOX_Kitchen_8x8 bulundu. Silip yeniden oluşturulsun mu?",
                "Sil ve yeniden oluştur",
                "Vazgeç");

            if (!delete) return;
            Object.DestroyImmediate(existing);
        }

        GameObject root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Create Greybox Kitchen");

        // ------------------------------------------------------------
        // Oda ölçüsü
        // ------------------------------------------------------------
        // Zemin: 8x8, merkez origin.
        // Unity cube default 1x1x1 olduğu için scale doğrudan ölçüdür.
        float roomSize = 8f;
        float half = roomSize / 2f;
        float floorThickness = 0.18f;
        float wallThickness = 0.25f;
        float wallHeight = 3f;
        float wallY = floorThickness + wallHeight / 2f;

        // ------------------------------------------------------------
        // Ana oda: zemin + 4 duvar, ön duvarda kapı boşluğu
        // ------------------------------------------------------------
        AddCube(root, greybox, "Floor_8x8", new Vector3(0, 0, 0), new Vector3(8f, floorThickness, 8f));

        // Arka duvar
        AddCube(root, greybox, "Wall_Back", new Vector3(0, wallY, half), new Vector3(8f, wallHeight, wallThickness));

        // Sol ve sağ duvar
        AddCube(root, greybox, "Wall_Left", new Vector3(-half, wallY, 0), new Vector3(wallThickness, wallHeight, 8f));
        AddCube(root, greybox, "Wall_Right", new Vector3(half, wallY, 0), new Vector3(wallThickness, wallHeight, 8f));

        // Ön duvar: kapı boşluğu ortadan biraz sola alınmış.
        // Kapı boşluğu: yaklaşık 1.4 genişlik, 2.25 yükseklik.
        // Ön duvar üç parçadan oluşur: sol parça, sağ parça, kapı üst lentosu.
        float frontZ = -half;
        AddCube(root, greybox, "Wall_Front_LeftOfDoor", new Vector3(-2.85f, wallY, frontZ), new Vector3(2.3f, wallHeight, wallThickness));
        AddCube(root, greybox, "Wall_Front_RightOfDoor", new Vector3(1.15f, wallY, frontZ), new Vector3(5.7f, wallHeight, wallThickness));
        AddCube(root, greybox, "Wall_Front_DoorHeader", new Vector3(-1.0f, 2.75f, frontZ), new Vector3(1.4f, 0.5f, wallThickness));

        // Kapı pervazı / eşiği hissi için ince küpler. Kapı boşluğunu kapatmaz.
        AddCube(root, greybox, "Door_Frame_Left", new Vector3(-1.75f, 1.25f, frontZ - 0.03f), new Vector3(0.12f, 2.3f, 0.18f));
        AddCube(root, greybox, "Door_Frame_Right", new Vector3(-0.25f, 1.25f, frontZ - 0.03f), new Vector3(0.12f, 2.3f, 0.18f));
        AddCube(root, greybox, "Door_Frame_Top", new Vector3(-1.0f, 2.35f, frontZ - 0.03f), new Vector3(1.62f, 0.12f, 0.18f));
        AddCube(root, greybox, "Door_Threshold", new Vector3(-1.0f, floorThickness + 0.03f, frontZ - 0.15f), new Vector3(1.6f, 0.06f, 0.35f));

        // ------------------------------------------------------------
        // Mutfak detayları: hepsi cube, hepsi greybox materyal.
        // Bu detaylar oynanabilir ölçek hissi verir ama model değildir.
        // ------------------------------------------------------------
        CreateKitchenCounters(root, greybox, floorThickness);
        CreateAppliances(root, greybox, floorThickness);
        CreateWallCabinets(root, greybox, floorThickness);
        CreateDiningBlockout(root, greybox, floorThickness);
        CreateReadableGameplayMarkers(root, greybox, floorThickness);

        // Hafif tepeden bakış için kamera ve ışık oluşturuyoruz; greybox parçaları yine tek materyal.
        CreateViewHelpers(root);

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);
        EditorUtility.SetDirty(root);

        Debug.Log("Greybox kitchen oluşturuldu. Materyal: " + MaterialPath);
    }

    private static void CreateKitchenCounters(GameObject root, Material mat, float floorThickness)
    {
        // L şeklinde tezgah: arka duvar ve sağ duvar boyunca.
        float baseY = floorThickness + 0.45f;
        float counterTopY = floorThickness + 0.93f;

        // Arka duvar alt dolapları
        AddCube(root, mat, "BaseCabinet_Back_Left", new Vector3(-2.75f, baseY, 3.35f), new Vector3(1.35f, 0.9f, 0.75f));
        AddCube(root, mat, "BaseCabinet_Back_Mid", new Vector3(-1.35f, baseY, 3.35f), new Vector3(1.35f, 0.9f, 0.75f));
        AddCube(root, mat, "BaseCabinet_Back_Sink", new Vector3(0.05f, baseY, 3.35f), new Vector3(1.35f, 0.9f, 0.75f));
        AddCube(root, mat, "BaseCabinet_Back_Right", new Vector3(1.45f, baseY, 3.35f), new Vector3(1.35f, 0.9f, 0.75f));

        // Arka tezgah üst tablası
        AddCube(root, mat, "Countertop_Back_Long", new Vector3(-0.65f, counterTopY, 3.35f), new Vector3(5.65f, 0.16f, 0.86f));

        // Sağ duvar alt dolapları
        AddCube(root, mat, "BaseCabinet_Right_Back", new Vector3(3.35f, baseY, 2.15f), new Vector3(0.75f, 0.9f, 1.25f));
        AddCube(root, mat, "BaseCabinet_Right_Mid", new Vector3(3.35f, baseY, 0.85f), new Vector3(0.75f, 0.9f, 1.25f));
        AddCube(root, mat, "BaseCabinet_Right_Front", new Vector3(3.35f, baseY, -0.45f), new Vector3(0.75f, 0.9f, 1.25f));

        // Sağ tezgah üst tablası
        AddCube(root, mat, "Countertop_Right_Long", new Vector3(3.35f, counterTopY, 0.85f), new Vector3(0.86f, 0.16f, 4.05f));

        // Köşe bloğu L birleşimini daha okunur yapar.
        AddCube(root, mat, "Corner_Cabinet_Block", new Vector3(3.35f, baseY, 3.35f), new Vector3(0.8f, 0.9f, 0.8f));
        AddCube(root, mat, "Corner_Countertop_Block", new Vector3(3.35f, counterTopY, 3.35f), new Vector3(0.92f, 0.16f, 0.92f));

        // Dolap kapak çizgileri: çok ince çıkıntılar, yine cube.
        for (int i = 0; i < 4; i++)
        {
            float x = -3.42f + i * 1.4f;
            AddCube(root, mat, "Back_Cabinet_DoorLine_" + (i + 1), new Vector3(x + 0.68f, floorThickness + 0.49f, 2.945f), new Vector3(0.04f, 0.72f, 0.04f));
            AddCube(root, mat, "Back_Cabinet_Handle_" + (i + 1), new Vector3(x + 0.95f, floorThickness + 0.61f, 2.9f), new Vector3(0.38f, 0.06f, 0.06f));
        }

        for (int i = 0; i < 3; i++)
        {
            float z = 2.78f - i * 1.3f;
            AddCube(root, mat, "Right_Cabinet_DoorLine_" + (i + 1), new Vector3(2.945f, floorThickness + 0.49f, z - 0.62f), new Vector3(0.04f, 0.72f, 0.04f));
            AddCube(root, mat, "Right_Cabinet_Handle_" + (i + 1), new Vector3(2.9f, floorThickness + 0.61f, z - 0.3f), new Vector3(0.06f, 0.06f, 0.38f));
        }
    }

    private static void CreateAppliances(GameObject root, Material mat, float floorThickness)
    {
        // Buzdolabı: sol arka köşe. Tamamen cube blok.
        AddCube(root, mat, "Fridge_Tall_Block", new Vector3(-3.3f, floorThickness + 1.05f, 2.65f), new Vector3(0.85f, 2.1f, 1.15f));
        AddCube(root, mat, "Fridge_Door_Split_Line", new Vector3(-2.86f, floorThickness + 1.13f, 2.65f), new Vector3(0.04f, 1.75f, 0.04f));
        AddCube(root, mat, "Fridge_Handle", new Vector3(-2.83f, floorThickness + 1.05f, 2.25f), new Vector3(0.06f, 1.0f, 0.08f));

        // Ocak: sağ tezgah üstünde, oyuncuya yakın. Dört göz ince küpler.
        AddCube(root, mat, "Stove_Surface", new Vector3(3.02f, floorThickness + 1.04f, -0.45f), new Vector3(0.08f, 0.05f, 0.95f));
        AddCube(root, mat, "Stove_Burner_1", new Vector3(2.96f, floorThickness + 1.09f, -0.75f), new Vector3(0.05f, 0.04f, 0.22f));
        AddCube(root, mat, "Stove_Burner_2", new Vector3(2.96f, floorThickness + 1.09f, -0.20f), new Vector3(0.05f, 0.04f, 0.22f));
        AddCube(root, mat, "Stove_Burner_3", new Vector3(2.96f, floorThickness + 1.09f, 0.15f), new Vector3(0.05f, 0.04f, 0.18f));
        AddCube(root, mat, "Stove_KnobStrip", new Vector3(2.92f, floorThickness + 1.0f, -0.45f), new Vector3(0.06f, 0.1f, 0.9f));

        // Davlumbaz: ocak üstünde cube form.
        AddCube(root, mat, "Hood_Main", new Vector3(3.0f, floorThickness + 2.15f, -0.45f), new Vector3(0.45f, 0.28f, 1.05f));
        AddCube(root, mat, "Hood_Chimney", new Vector3(3.65f, floorThickness + 2.55f, -0.45f), new Vector3(0.32f, 0.75f, 0.55f));

        // Lavabo: arka tezgah üstünde çukur hissi için alçak/ince bloklar.
        AddCube(root, mat, "Sink_Basin_Block", new Vector3(0.05f, floorThickness + 1.055f, 2.98f), new Vector3(0.95f, 0.08f, 0.48f));
        AddCube(root, mat, "Sink_Back_Lip", new Vector3(0.05f, floorThickness + 1.13f, 3.24f), new Vector3(1.0f, 0.08f, 0.05f));
        AddCube(root, mat, "Sink_Front_Lip", new Vector3(0.05f, floorThickness + 1.13f, 2.72f), new Vector3(1.0f, 0.08f, 0.05f));
        AddCube(root, mat, "Sink_Left_Lip", new Vector3(-0.47f, floorThickness + 1.13f, 2.98f), new Vector3(0.05f, 0.08f, 0.52f));
        AddCube(root, mat, "Sink_Right_Lip", new Vector3(0.57f, floorThickness + 1.13f, 2.98f), new Vector3(0.05f, 0.08f, 0.52f));
        AddCube(root, mat, "Faucet_Vertical", new Vector3(0.05f, floorThickness + 1.28f, 3.18f), new Vector3(0.08f, 0.35f, 0.08f));
        AddCube(root, mat, "Faucet_Spout", new Vector3(0.05f, floorThickness + 1.45f, 3.0f), new Vector3(0.08f, 0.08f, 0.35f));

        // Mini çöp kutusu / yan blok: oyun içinde referans ölçek objesi gibi.
        AddCube(root, mat, "Trash_Bin_Block", new Vector3(-3.35f, floorThickness + 0.35f, -2.8f), new Vector3(0.55f, 0.7f, 0.55f));
        AddCube(root, mat, "Trash_Bin_Lid", new Vector3(-3.35f, floorThickness + 0.73f, -2.8f), new Vector3(0.62f, 0.08f, 0.62f));
    }

    private static void CreateWallCabinets(GameObject root, Material mat, float floorThickness)
    {
        // Arka duvarda üst dolaplar.
        AddCube(root, mat, "WallCabinet_Back_Left", new Vector3(-2.1f, floorThickness + 2.05f, 3.47f), new Vector3(1.2f, 0.75f, 0.38f));
        AddCube(root, mat, "WallCabinet_Back_Mid", new Vector3(-0.75f, floorThickness + 2.05f, 3.47f), new Vector3(1.2f, 0.75f, 0.38f));
        AddCube(root, mat, "WallCabinet_Back_Right", new Vector3(1.95f, floorThickness + 2.05f, 3.47f), new Vector3(1.2f, 0.75f, 0.38f));

        // Lavabo üstünde pencere gibi boşluk bırak: üst dolap yok, yerine çerçeveli pencere bloğu.
        AddCube(root, mat, "Window_Frame_Top", new Vector3(0.55f, floorThickness + 2.35f, 3.86f), new Vector3(1.25f, 0.08f, 0.08f));
        AddCube(root, mat, "Window_Frame_Bottom", new Vector3(0.55f, floorThickness + 1.62f, 3.86f), new Vector3(1.25f, 0.08f, 0.08f));
        AddCube(root, mat, "Window_Frame_Left", new Vector3(-0.1f, floorThickness + 1.98f, 3.86f), new Vector3(0.08f, 0.75f, 0.08f));
        AddCube(root, mat, "Window_Frame_Right", new Vector3(1.2f, floorThickness + 1.98f, 3.86f), new Vector3(0.08f, 0.75f, 0.08f));
        AddCube(root, mat, "Window_Crossbar", new Vector3(0.55f, floorThickness + 1.98f, 3.87f), new Vector3(1.2f, 0.05f, 0.05f));

        // Sağ duvarda üst dolaplar.
        AddCube(root, mat, "WallCabinet_Right_Back", new Vector3(3.47f, floorThickness + 2.05f, 1.75f), new Vector3(0.38f, 0.75f, 1.2f));
        AddCube(root, mat, "WallCabinet_Right_Mid", new Vector3(3.47f, floorThickness + 2.05f, 0.45f), new Vector3(0.38f, 0.75f, 1.2f));

        // Raflar: sol duvarda oyuncunun okuyabileceği şekilde.
        AddCube(root, mat, "OpenShelf_Left_Upper", new Vector3(-3.86f, floorThickness + 2.25f, 0.85f), new Vector3(0.12f, 0.1f, 1.7f));
        AddCube(root, mat, "OpenShelf_Left_Lower", new Vector3(-3.86f, floorThickness + 1.75f, 0.85f), new Vector3(0.12f, 0.1f, 1.7f));
        AddCube(root, mat, "Shelf_Item_Block_1", new Vector3(-3.77f, floorThickness + 1.92f, 0.25f), new Vector3(0.18f, 0.28f, 0.18f));
        AddCube(root, mat, "Shelf_Item_Block_2", new Vector3(-3.77f, floorThickness + 1.92f, 0.75f), new Vector3(0.18f, 0.35f, 0.18f));
        AddCube(root, mat, "Shelf_Item_Block_3", new Vector3(-3.77f, floorThickness + 2.42f, 1.2f), new Vector3(0.18f, 0.25f, 0.18f));
    }

    private static void CreateDiningBlockout(GameObject root, Material mat, float floorThickness)
    {
        // Ortada küçük masa ve iki sandalye — navigasyon/ölçek için.
        AddCube(root, mat, "Small_Table_Top", new Vector3(-1.25f, floorThickness + 0.78f, -0.65f), new Vector3(1.5f, 0.15f, 1.0f));
        AddCube(root, mat, "Small_Table_Leg_FL", new Vector3(-1.85f, floorThickness + 0.4f, -1.05f), new Vector3(0.12f, 0.75f, 0.12f));
        AddCube(root, mat, "Small_Table_Leg_FR", new Vector3(-0.65f, floorThickness + 0.4f, -1.05f), new Vector3(0.12f, 0.75f, 0.12f));
        AddCube(root, mat, "Small_Table_Leg_BL", new Vector3(-1.85f, floorThickness + 0.4f, -0.25f), new Vector3(0.12f, 0.75f, 0.12f));
        AddCube(root, mat, "Small_Table_Leg_BR", new Vector3(-0.65f, floorThickness + 0.4f, -0.25f), new Vector3(0.12f, 0.75f, 0.12f));

        // Sandalye 1
        AddCube(root, mat, "Chair_A_Seat", new Vector3(-2.45f, floorThickness + 0.48f, -0.65f), new Vector3(0.55f, 0.12f, 0.55f));
        AddCube(root, mat, "Chair_A_Back", new Vector3(-2.75f, floorThickness + 0.9f, -0.65f), new Vector3(0.12f, 0.85f, 0.58f));
        AddCube(root, mat, "Chair_A_Leg_1", new Vector3(-2.25f, floorThickness + 0.26f, -0.45f), new Vector3(0.08f, 0.5f, 0.08f));
        AddCube(root, mat, "Chair_A_Leg_2", new Vector3(-2.25f, floorThickness + 0.26f, -0.85f), new Vector3(0.08f, 0.5f, 0.08f));
        AddCube(root, mat, "Chair_A_Leg_3", new Vector3(-2.65f, floorThickness + 0.26f, -0.45f), new Vector3(0.08f, 0.5f, 0.08f));
        AddCube(root, mat, "Chair_A_Leg_4", new Vector3(-2.65f, floorThickness + 0.26f, -0.85f), new Vector3(0.08f, 0.5f, 0.08f));

        // Sandalye 2
        AddCube(root, mat, "Chair_B_Seat", new Vector3(0.05f, floorThickness + 0.48f, -0.65f), new Vector3(0.55f, 0.12f, 0.55f));
        AddCube(root, mat, "Chair_B_Back", new Vector3(0.35f, floorThickness + 0.9f, -0.65f), new Vector3(0.12f, 0.85f, 0.58f));
        AddCube(root, mat, "Chair_B_Leg_1", new Vector3(-0.15f, floorThickness + 0.26f, -0.45f), new Vector3(0.08f, 0.5f, 0.08f));
        AddCube(root, mat, "Chair_B_Leg_2", new Vector3(-0.15f, floorThickness + 0.26f, -0.85f), new Vector3(0.08f, 0.5f, 0.08f));
        AddCube(root, mat, "Chair_B_Leg_3", new Vector3(0.25f, floorThickness + 0.26f, -0.45f), new Vector3(0.08f, 0.5f, 0.08f));
        AddCube(root, mat, "Chair_B_Leg_4", new Vector3(0.25f, floorThickness + 0.26f, -0.85f), new Vector3(0.08f, 0.5f, 0.08f));
    }

    private static void CreateReadableGameplayMarkers(GameObject root, Material mat, float floorThickness)
    {
        // Oyunda yürüyüş alanını okunur kılmak için zemine ince bloklarla rota/ölçek işaretleri.
        // Bunlar istenmezse sahneden kolayca silinebilir.
        AddCube(root, mat, "Walkable_Area_Center_Marker", new Vector3(0.65f, floorThickness + 0.012f, -1.1f), new Vector3(1.8f, 0.024f, 0.08f));
        AddCube(root, mat, "Walkable_Area_ToDoor_Marker", new Vector3(-1.0f, floorThickness + 0.012f, -2.65f), new Vector3(0.08f, 0.024f, 1.7f));
        AddCube(root, mat, "Counter_Clearance_Marker", new Vector3(1.55f, floorThickness + 0.012f, 1.65f), new Vector3(0.08f, 0.024f, 2.4f));

        // Süpürgelik / base trim: duvar-floors birleşimi daha bitmiş görünür.
        AddCube(root, mat, "Baseboard_Back", new Vector3(0, floorThickness + 0.12f, 3.84f), new Vector3(7.65f, 0.16f, 0.08f));
        AddCube(root, mat, "Baseboard_Left", new Vector3(-3.84f, floorThickness + 0.12f, 0), new Vector3(0.08f, 0.16f, 7.65f));
        AddCube(root, mat, "Baseboard_Right", new Vector3(3.84f, floorThickness + 0.12f, 0), new Vector3(0.08f, 0.16f, 7.65f));
        AddCube(root, mat, "Baseboard_Front_Left", new Vector3(-2.85f, floorThickness + 0.12f, -3.84f), new Vector3(2.1f, 0.16f, 0.08f));
        AddCube(root, mat, "Baseboard_Front_Right", new Vector3(1.25f, floorThickness + 0.12f, -3.84f), new Vector3(5.5f, 0.16f, 0.08f));
    }

    private static void CreateViewHelpers(GameObject root)
    {
        // Bunlar model kuralını bozmaz; sadece sahnede rahat görüntülemek için.
        GameObject lightObj = GameObject.Find("GreyboxKitchen_DirectionalLight");
        if (lightObj == null)
        {
            lightObj = new GameObject("GreyboxKitchen_DirectionalLight");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            lightObj.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
        }

        GameObject camObj = GameObject.Find("GreyboxKitchen_Camera");
        if (camObj == null)
        {
            camObj = new GameObject("GreyboxKitchen_Camera");
            Camera camera = camObj.AddComponent<Camera>();
            camera.fieldOfView = 55f;
            camObj.transform.position = new Vector3(6.7f, 5.0f, -7.2f);
            camObj.transform.LookAt(new Vector3(0f, 1.15f, 0f));
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 100f;
        }
    }

    private static GameObject AddCube(GameObject parent, Material mat, string name, Vector3 position, Vector3 scale)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = name;
        cube.transform.SetParent(parent.transform, true);
        cube.transform.position = position;
        cube.transform.localScale = scale;

        MeshRenderer renderer = cube.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = mat;
        }

        // BoxCollider cube ile birlikte gelir; greybox playtest için kalsın.
        Undo.RegisterCreatedObjectUndo(cube, "Create " + name);
        return cube;
    }

    private static Material GetOrCreateGreyboxMaterial()
    {
        EnsureFolders();

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");

            material = new Material(shader);
            material.name = "Greybox";
            material.color = GreyboxColor;

            AssetDatabase.CreateAsset(material, MaterialPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        else
        {
            material.color = GreyboxColor;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
        }

        return material;
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/_Project"))
        {
            AssetDatabase.CreateFolder("Assets", "_Project");
        }

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials"))
        {
            AssetDatabase.CreateFolder("Assets/_Project", "Materials");
        }

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Editor"))
        {
            AssetDatabase.CreateFolder("Assets/_Project", "Editor");
        }
    }
}

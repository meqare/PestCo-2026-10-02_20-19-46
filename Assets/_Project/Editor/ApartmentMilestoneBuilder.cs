using System;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PestCo.Editor
{
    public static class ApartmentMilestoneBuilder
    {
        private const string ApartmentRootName = "CUMARTESI_4_ODALI_DAIRE";
        private const string GeneratedRoot = "Assets/PestCo/Generated";
        private const string MaterialRoot = GeneratedRoot + "/Materials";
        private const string NavMeshAssetPath = GeneratedRoot + "/Cumartesi_Daire_NavMesh.asset";

        private static Material wallMaterial;
        private static Material floorMaterial;
        private static Material ceilingMaterial;
        private static Material trimMaterial;
        private static Material darkMaterial;
        private static Material kitchenMaterial;
        private static Material salonMaterial;
        private static Material bathroomMaterial;
        private static Material metalMaterial;
        private static Material ceramicMaterial;

        [MenuItem("PestCo/Cumartesi Milestone/Odaları Mevcut Sahneye Ekle veya Güncelle", priority = 10)]
        public static void AddRoomsToCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Odaları eklemeden önce Play Mode'dan çıkın.");
                return;
            }

            BuildApartmentInCurrentScene();
        }

        private static void BuildApartmentInCurrentScene()
        {
            try
            {
                Scene scene = SceneManager.GetActiveScene();
                if (!scene.IsValid())
                {
                    throw new InvalidOperationException("Aktif bir sahne bulunamadı.");
                }

                EditorUtility.DisplayProgressBar("PestCo • Cumartesi", "4 odalı daire mevcut sahneye ekleniyor…", 0.05f);

                Vector3 apartmentPosition = Vector3.zero;
                Quaternion apartmentRotation = Quaternion.identity;
                Vector3 apartmentScale = Vector3.one;
                foreach (GameObject rootObject in scene.GetRootGameObjects())
                {
                    if (rootObject.name == ApartmentRootName)
                    {
                        apartmentPosition = rootObject.transform.position;
                        apartmentRotation = rootObject.transform.rotation;
                        apartmentScale = rootObject.transform.localScale;
                        Object.DestroyImmediate(rootObject);
                        break;
                    }
                }

                if (AssetDatabase.IsValidFolder(GeneratedRoot))
                {
                    AssetDatabase.DeleteAsset(GeneratedRoot);
                }

                EnsureFolder("Assets/PestCo");
                EnsureFolder(GeneratedRoot);
                EnsureFolder(MaterialRoot);
                CreateMaterials();

                GameObject apartmentRoot = new GameObject(ApartmentRootName);
                apartmentRoot.transform.SetPositionAndRotation(apartmentPosition, apartmentRotation);
                apartmentRoot.transform.localScale = apartmentScale;
                GameObject navMeshRoot = CreateChild("NavMeshSurface", apartmentRoot.transform);
                GameObject architectureRoot = CreateChild("Architecture", navMeshRoot.transform);
                Transform floors = CreateChild("Floors_NAVIGATION_STATIC", architectureRoot.transform).transform;
                Transform walls = CreateChild("Walls", architectureRoot.transform).transform;
                Transform ceilings = CreateChild("Ceilings", architectureRoot.transform).transform;
                Transform doorFrames = CreateChild("Connected_Doorways", architectureRoot.transform).transform;
                Transform props = CreateChild("Box_Props", navMeshRoot.transform).transform;

                EditorUtility.DisplayProgressBar("PestCo • Cumartesi", "Mutfak, salon, banyo ve koridor kuruluyor…", 0.25f);
                BuildArchitecture(floors, walls, ceilings, doorFrames);
                BuildProps(props);

                EditorUtility.DisplayProgressBar("PestCo • Cumartesi", "NavMesh pişiriliyor…", 0.75f);
                BuildAndPersistNavMesh(navMeshRoot.transform);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!string.IsNullOrEmpty(scene.path))
                {
                    EditorSceneManager.SaveScene(scene);
                }
                else
                {
                    Debug.LogWarning("Odalar eklendi. İsimsiz sahneyi kaydetmek için Ctrl+S kullanın.");
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Selection.activeGameObject = navMeshRoot;
                SceneView.lastActiveSceneView?.FrameSelected();

                Debug.Log(
                    "Mutfak, salon, banyo ve koridor mevcut sahneye eklendi. " +
                    "Mevcut Player, Main Camera, WASD kontrolü ve el feneri değiştirilmedi.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "PestCo • Kurulum tamamlanamadı",
                    "Console penceresindeki ilk kırmızı hatayı kontrol edin.\n\n" + exception.Message,
                    "Tamam");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void CreateMaterials()
        {
            wallMaterial = CreateMaterial("Wall_Matte", new Color(0.28f, 0.3f, 0.34f), 0f, 0.16f);
            floorMaterial = CreateMaterial("Floor_Charcoal", new Color(0.055f, 0.064f, 0.078f), 0.05f, 0.22f);
            ceilingMaterial = CreateMaterial("Ceiling_Dark", new Color(0.13f, 0.14f, 0.16f), 0f, 0.08f);
            trimMaterial = CreateMaterial("Door_Trim", new Color(0.18f, 0.085f, 0.035f), 0f, 0.28f);
            darkMaterial = CreateMaterial("Prop_Black", new Color(0.018f, 0.022f, 0.03f), 0.1f, 0.28f);
            kitchenMaterial = CreateMaterial("Kitchen_Muted_Green", new Color(0.12f, 0.22f, 0.2f), 0f, 0.2f);
            salonMaterial = CreateMaterial("Salon_Deep_Blue", new Color(0.08f, 0.14f, 0.24f), 0f, 0.3f);
            bathroomMaterial = CreateMaterial("Bathroom_Stone", new Color(0.23f, 0.27f, 0.3f), 0f, 0.4f);
            metalMaterial = CreateMaterial("Metal", new Color(0.22f, 0.24f, 0.28f), 0.72f, 0.68f);
            ceramicMaterial = CreateMaterial("Ceramic", new Color(0.56f, 0.5f, 0.36f), 0.2f, 0.55f);
        }

        private static Material CreateMaterial(string name, Color color, float metallic, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            if (shader == null)
            {
                throw new InvalidOperationException("Standard veya URP/Lit shader bulunamadı.");
            }

            Material material = new Material(shader) { name = name, color = color };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            AssetDatabase.CreateAsset(material, $"{MaterialRoot}/{name}.mat");
            return material;
        }

        private static void BuildArchitecture(Transform floors, Transform walls, Transform ceilings, Transform doorFrames)
        {
            // Four connected areas: corridor, salon, kitchen and bathroom.
            CreateFloor("Floor_Koridor", new Vector3(0f, -0.1f, 0f), new Vector3(12f, 0.2f, 2.5f), floors);
            CreateFloor("Floor_Salon", new Vector3(-3f, -0.1f, 4.25f), new Vector3(6f, 0.2f, 6f), floors);
            CreateFloor("Floor_Mutfak", new Vector3(3f, -0.1f, 4.25f), new Vector3(6f, 0.2f, 6f), floors);
            CreateFloor("Floor_Banyo", new Vector3(4f, -0.1f, -3.25f), new Vector3(4f, 0.2f, 4f), floors);

            CreateBox("Ceiling_Koridor", new Vector3(0f, 3.12f, 0f), new Vector3(12.2f, 0.24f, 2.7f), ceilingMaterial, ceilings, false);
            CreateBox("Ceiling_Salon", new Vector3(-3f, 3.12f, 4.25f), new Vector3(6.2f, 0.24f, 6.2f), ceilingMaterial, ceilings, false);
            CreateBox("Ceiling_Mutfak", new Vector3(3f, 3.12f, 4.25f), new Vector3(6.2f, 0.24f, 6.2f), ceilingMaterial, ceilings, false);
            CreateBox("Ceiling_Banyo", new Vector3(4f, 3.12f, -3.25f), new Vector3(4.2f, 0.24f, 4.2f), ceilingMaterial, ceilings, false);

            const float wallHeight = 3f;
            const float wallY = 1.5f;
            const float thickness = 0.2f;

            // Northern outer wall and the split between salon / kitchen.
            CreateWallX("Wall_North_Salon", -3f, 7.25f, 6f, wallY, wallHeight, thickness, walls);
            CreateWallX("Wall_North_Mutfak", 3f, 7.25f, 6f, wallY, wallHeight, thickness, walls);
            CreateWallZ("Wall_West_Salon", -6f, 4.25f, 6f, wallY, wallHeight, thickness, walls);
            CreateWallZ("Wall_Salon_Mutfak", 0f, 4.25f, 6f, wallY, wallHeight, thickness, walls);
            CreateWallZ("Wall_East_Mutfak", 6f, 4.25f, 6f, wallY, wallHeight, thickness, walls);

            // Salon and kitchen doors connect directly to the corridor.
            CreateWallX("Wall_Corridor_North_01", -4.6f, 1.25f, 2.8f, wallY, wallHeight, thickness, walls);
            CreateWallX("Wall_Corridor_North_02", 0f, 1.25f, 3.6f, wallY, wallHeight, thickness, walls);
            CreateWallX("Wall_Corridor_North_03", 4.6f, 1.25f, 2.8f, wallY, wallHeight, thickness, walls);
            CreateDoorFrame("Door_Salon", new Vector3(-2.5f, 0f, 1.25f), 1.4f, false, doorFrames);
            CreateDoorFrame("Door_Mutfak", new Vector3(2.5f, 0f, 1.25f), 1.4f, false, doorFrames);

            // Corridor shell; its southern wall opens into the bathroom.
            CreateWallZ("Wall_Corridor_West", -6f, 0f, 2.5f, wallY, wallHeight, thickness, walls);
            CreateWallZ("Wall_Corridor_East", 6f, 0f, 2.5f, wallY, wallHeight, thickness, walls);
            CreateWallX("Wall_Corridor_South_Long", -2f, -1.25f, 8f, wallY, wallHeight, thickness, walls);
            CreateWallX("Wall_Bath_North_Left", 2.65f, -1.25f, 1.3f, wallY, wallHeight, thickness, walls);
            CreateWallX("Wall_Bath_North_Right", 5.35f, -1.25f, 1.3f, wallY, wallHeight, thickness, walls);
            CreateDoorFrame("Door_Banyo", new Vector3(4f, 0f, -1.25f), 1.4f, false, doorFrames);

            // Bathroom outer shell.
            CreateWallZ("Wall_Bath_West", 2f, -3.25f, 4f, wallY, wallHeight, thickness, walls);
            CreateWallZ("Wall_Bath_East", 6f, -3.25f, 4f, wallY, wallHeight, thickness, walls);
            CreateWallX("Wall_Bath_South", 4f, -5.25f, 4f, wallY, wallHeight, thickness, walls);
        }

        private static void BuildProps(Transform props)
        {
            Transform salon = CreateChild("Salon", props).transform;
            CreateBox("Sofa_Base", new Vector3(-4.65f, 0.36f, 5.45f), new Vector3(2.15f, 0.72f, 0.78f), salonMaterial, salon);
            CreateBox("Sofa_Back", new Vector3(-4.65f, 0.83f, 5.78f), new Vector3(2.15f, 0.82f, 0.18f), salonMaterial, salon);
            CreateBox("Coffee_Table", new Vector3(-3.25f, 0.3f, 4.3f), new Vector3(1.25f, 0.6f, 0.68f), trimMaterial, salon);
            CreateBox("TV_Console", new Vector3(-0.48f, 0.35f, 4.75f), new Vector3(0.72f, 0.7f, 1.8f), darkMaterial, salon);
            CreateBox("TV_Screen", new Vector3(-0.18f, 1.25f, 4.75f), new Vector3(0.1f, 1.05f, 1.65f), darkMaterial, salon);

            Transform kitchen = CreateChild("Mutfak", props).transform;
            CreateBox("Counter_North", new Vector3(3f, 0.46f, 6.72f), new Vector3(4.5f, 0.92f, 0.72f), kitchenMaterial, kitchen);
            CreateBox("Counter_East", new Vector3(5.55f, 0.46f, 4.65f), new Vector3(0.72f, 0.92f, 3.35f), kitchenMaterial, kitchen);
            CreateBox("Fridge", new Vector3(0.62f, 1.03f, 6.55f), new Vector3(0.92f, 2.06f, 1.02f), metalMaterial, kitchen);
            CreateBox("Island", new Vector3(3.1f, 0.48f, 4.25f), new Vector3(1.55f, 0.96f, 0.82f), trimMaterial, kitchen);
            CreateBox("Cooktop", new Vector3(3.1f, 0.975f, 4.25f), new Vector3(1.15f, 0.03f, 0.62f), darkMaterial, kitchen);

            Transform bathroom = CreateChild("Banyo", props).transform;
            CreateBox("Shower_Base", new Vector3(2.72f, 0.08f, -4.5f), new Vector3(1.12f, 0.16f, 1.12f), bathroomMaterial, bathroom);
            CreateBox("Shower_Glass", new Vector3(3.27f, 1.1f, -4.5f), new Vector3(0.05f, 2.1f, 1.12f), metalMaterial, bathroom);
            CreateBox("Vanity", new Vector3(5.45f, 0.48f, -4.32f), new Vector3(0.72f, 0.96f, 1.1f), bathroomMaterial, bathroom);
            CreateBox("Mirror", new Vector3(5.82f, 1.65f, -4.32f), new Vector3(0.05f, 1.15f, 1.05f), metalMaterial, bathroom);
            CreateBox("Toilet_Base", new Vector3(5.15f, 0.28f, -2.18f), new Vector3(0.62f, 0.56f, 0.82f), ceramicMaterial, bathroom);
            CreateBox("Toilet_Tank", new Vector3(5.62f, 0.72f, -2.18f), new Vector3(0.28f, 0.82f, 0.75f), ceramicMaterial, bathroom);

            Transform corridor = CreateChild("Koridor", props).transform;
            CreateBox("Entry_Bench", new Vector3(-4.35f, 0.32f, -0.88f), new Vector3(1.6f, 0.64f, 0.48f), trimMaterial, corridor);
            CreateBox("Shoe_Box", new Vector3(-5.45f, 0.38f, -0.9f), new Vector3(0.55f, 0.76f, 0.42f), darkMaterial, corridor);
        }

        private static void BuildAndPersistNavMesh(Transform navMeshRoot)
        {
            NavMeshSurface surface = navMeshRoot.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.overrideTileSize = true;
            surface.tileSize = 128;

            AssetDatabase.DeleteAsset(NavMeshAssetPath);
            surface.BuildNavMesh();

            NavMeshData navMeshData = surface.navMeshData;
            if (navMeshData == null)
            {
                throw new InvalidOperationException("NavMeshSurface veri üretmedi.");
            }

            navMeshData.name = "Cumartesi_Daire_NavMesh";
            navMeshData.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(navMeshData, NavMeshAssetPath);
            EditorUtility.SetDirty(surface);
        }

        private static void CreateFloor(string name, Vector3 position, Vector3 scale, Transform parent)
        {
            GameObject floor = CreateBox(name, position, scale, floorMaterial, parent);
            GameObjectUtility.SetStaticEditorFlags(
                floor,
                StaticEditorFlags.NavigationStatic |
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccludeeStatic);
        }

        private static void CreateWallX(
            string name,
            float centerX,
            float z,
            float length,
            float y,
            float height,
            float thickness,
            Transform parent)
        {
            CreateBox(name, new Vector3(centerX, y, z), new Vector3(length, height, thickness), wallMaterial, parent);
        }

        private static void CreateWallZ(
            string name,
            float x,
            float centerZ,
            float length,
            float y,
            float height,
            float thickness,
            Transform parent)
        {
            CreateBox(name, new Vector3(x, y, centerZ), new Vector3(thickness, height, length), wallMaterial, parent);
        }

        private static void CreateDoorFrame(string name, Vector3 floorCenter, float width, bool alongZ, Transform parent)
        {
            Transform frame = CreateChild(name, parent).transform;
            float sideOffset = width * 0.5f + 0.06f;

            if (alongZ)
            {
                CreateBox("Frame_A", floorCenter + new Vector3(0f, 1.15f, -sideOffset), new Vector3(0.28f, 2.3f, 0.12f), trimMaterial, frame, false);
                CreateBox("Frame_B", floorCenter + new Vector3(0f, 1.15f, sideOffset), new Vector3(0.28f, 2.3f, 0.12f), trimMaterial, frame, false);
                CreateBox("Frame_Top", floorCenter + new Vector3(0f, 2.33f, 0f), new Vector3(0.28f, 0.14f, width + 0.24f), trimMaterial, frame, false);
            }
            else
            {
                CreateBox("Frame_A", floorCenter + new Vector3(-sideOffset, 1.15f, 0f), new Vector3(0.12f, 2.3f, 0.28f), trimMaterial, frame, false);
                CreateBox("Frame_B", floorCenter + new Vector3(sideOffset, 1.15f, 0f), new Vector3(0.12f, 2.3f, 0.28f), trimMaterial, frame, false);
                CreateBox("Frame_Top", floorCenter + new Vector3(0f, 2.33f, 0f), new Vector3(width + 0.24f, 0.14f, 0.28f), trimMaterial, frame, false);
            }
        }

        private static GameObject CreateBox(
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            Transform parent,
            bool navigationStatic = true)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            box.transform.localScale = scale;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;

            if (navigationStatic)
            {
                GameObjectUtility.SetStaticEditorFlags(
                    box,
                    StaticEditorFlags.NavigationStatic |
                    StaticEditorFlags.BatchingStatic |
                    StaticEditorFlags.OccluderStatic |
                    StaticEditorFlags.OccludeeStatic);
            }

            return box;
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folderPath)?.Replace('\\', '/');
            string folderName = Path.GetFileName(folderPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(folderName))
            {
                return;
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}

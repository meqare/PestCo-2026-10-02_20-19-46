using System.Collections.Generic;
using PestCo.Truck;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PestCo.Truck.EditorTools
{
    /// <summary>
    /// Laboratuvarı doğrudan kamyonun kasasına kurar (ışınlanma yok).
    /// 1) Kamyon orantılı büyütülür ve eve yer kalsın diye geri çekilir (yalnızca bir kez).
    /// 2) Kasanın iç ölçüleri kamyonun kendi duvar collider'larından okunur; lab bu ölçüye göre dizilir.
    /// 3) İş panosu, alet dolabı, satış alanı ve oyuncu başlangıcı yeni kasaya uyarlanır; arkaya rampa eklenir.
    /// Tekrar çalıştırılabilir: "KamyonLab" silinip yeniden kurulur, kamyon ikinci kez büyütülmez.
    /// Menü: PestCo > Kamyon Lab > Kasaya Kur
    /// </summary>
    public static class KamyonLabKasaKur
    {
        private const float Olcek = 1.35f;
        private const float GeriCekme = 2.0f; // dünya -Z (evden uzağa)
        private const string Mat = "Assets/_Project/TruckLab/Materials/";
        private const string Prof = "Assets/_Project/TruckLab/Profiles/";

        // İç ölçüler (çalışırken hesaplanır)
        private static float HX, HZ, H;

        private static Material celik, tavan, zemin, kablo, koyu, dolap, kati, seritBeyaz, seritTurkuaz, difuzor,
            rafArka, ekran, buton, fanusIc, rafLed, cam, sivi, logoCerceve, lambaMat, uyari, sari;
        private static Mesh fanusMesh;

        [MenuItem("PestCo/Kamyon Lab/Kasaya Kur")]
        public static void Calistir()
        {
            var kamyon = GameObject.Find("Kamyonet_GMC");
            if (kamyon == null) { Debug.LogError("[KamyonLab] Kamyonet_GMC bulunamadı."); return; }
            var kt = kamyon.transform;

            // --- 1) Kamyonu büyüt (bir kez)
            if (kt.localScale.x < 1.01f)
            {
                kt.localScale = Vector3.one * Olcek;
                kt.position += Vector3.back * GeriCekme;
            }
            Physics.SyncTransforms(); // collider sınırları yeni ölçeği görsün

            // Eski kurulumları temizle
            var eskiLab = GameObject.Find("KamyonLab");
            if (eskiLab != null) Object.DestroyImmediate(eskiLab);
            var eskiKapi = kt.Find("LabKapisi");
            if (eskiKapi != null) Object.DestroyImmediate(eskiKapi.gameObject);

            // --- 2) Kasa iç ölçüleri
            var ic = kt.Find("Interior");
            Bounds Bd(string ad) => ic.Find(ad).GetComponent<Collider>().bounds;
            float zeminY = Bd("Floor").max.y;
            float tavanY = Bd("Ceiling").min.y;
            var wl = Bd("WallLeft"); var wr = Bd("WallRight");
            float xMin = Mathf.Min(wl.max.x, wr.max.x), xMax = Mathf.Max(wl.min.x, wr.min.x);
            float onZ = Bd("WallFront").max.z;                                   // kabin tarafı (dünya -Z)
            float arkaZ = kt.Find("ExitDoor/Blocker").GetComponent<Collider>().bounds.min.z; // kapı (dünya +Z)
            HX = (xMax - xMin) * 0.5f;
            HZ = (arkaZ - onZ) * 0.5f;
            H = tavanY - zeminY;

            var labGo = new GameObject("KamyonLab");
            SceneManagerYerlestir(labGo, kamyon);
            var lab = labGo.transform;
            lab.position = new Vector3((xMin + xMax) * 0.5f, zeminY, (onZ + arkaZ) * 0.5f);
            lab.rotation = kt.rotation; // yerel +Z = kabin yönü, -Z = arka kapı
            var icKok = new GameObject("Ic").transform;
            icKok.SetParent(lab, false);

            // Kamyonun düz iç duvarlarını gizle (collider'lar kalır), eski tavan lambasını kapat
            foreach (var r in ic.GetComponentsInChildren<Renderer>()) r.enabled = false;
            var eskiIsik = kt.Find("TruckLight");
            if (eskiIsik != null && eskiIsik.TryGetComponent(out Light el)) el.enabled = false;

            MateryalleriYukle();

            // Ön duvardaki eski bölmeli raf kaldırıldı: böcekler artık sağ duvardaki cam fanuslara konuyor
            var fanusRafi = kt.Find("FanusRafi");
            if (fanusRafi != null) Object.DestroyImmediate(fanusRafi.gameObject);
            float onSinir = HZ - 0.08f;
            const float arkaBosluk = 1.55f; // kapı önü boş alan
            float arkaSinir = -HZ + arkaBosluk;

            // --- 3) Lab
            Kabuk(icKok);
            Raflar(icKok, arkaSinir, onSinir);
            var duzen = SolDuzen(arkaSinir, onSinir);
            var atmosfer = PostProcess(icKok);
            Masa(icKok, duzen.masaZ);
            Panel(icKok, duzen.panelZ, atmosfer);
            Terminal(icKok, duzen.terminalZ);
            Logo(icKok, duzen.masaZ);
            OnDuvarLogo(icKok);
            Isiklar(icKok);
            DuvarDetaylari(icKok);
            var probe = Probe(icKok);

            // --- 4) Kamyon sistemlerini uyarla
            float sinirArka = -HZ + 0.1f;
            YerlestirSinirla(kt.Find("ToolCabinet"), lab, new Vector3(HX - 0.16f, 0.75f, sinirArka + 0.6f), kt);
            // İş panosu kaldırıldı: görevler artık terminaldeki PestOS "Görevler" uygulamasından alınıyor
            var pano = kt.Find("JobBoard");
            if (pano != null) Object.DestroyImmediate(pano.gameObject);
            YoneticileriOnar();
            KapiSesleri(kt);
            SatisAlani(lab);
            Rampa(lab);
            TestEsyalariniKenaraAl(lab);
            OyuncuyuKoy(lab);

            // Yönetici
            var yon = labGo.AddComponent<KamyonLab>();
            var so = new SerializedObject(yon);
            so.FindProperty("icKok").objectReferenceValue = icKok.gameObject;
            so.FindProperty("probe").objectReferenceValue = probe;
            so.FindProperty("icMerkez").vector3Value = new Vector3(0f, H * 0.5f, 0f);
            so.FindProperty("icBoyut").vector3Value = new Vector3(HX * 2f, H, HZ * 2f);
            var kapat = KonturOzellikleri();
            var kp = so.FindProperty("icerdeKapat");
            kp.arraySize = kapat.Count;
            for (int i = 0; i < kapat.Count; i++) kp.GetArrayElementAtIndex(i).objectReferenceValue = kapat[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            // Editörde görünür kalsın; oyunda KamyonLab yaklaşınca açıp kapatır
            icKok.gameObject.SetActive(true);
            EditorSceneManager.MarkSceneDirty(kamyon.scene);
            Debug.Log($"[KamyonLab] Kasaya kuruldu. İç ölçü: {HX * 2f:0.00} x {H:0.00} x {HZ * 2f:0.00} m");
        }

        private static void SceneManagerYerlestir(GameObject go, GameObject referans)
            => UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, referans.scene);

        // ------------------------------------------------------------------ MATERYAL

        private static void MateryalleriYukle()
        {
            Material L(string ad) => AssetDatabase.LoadAssetAtPath<Material>(Mat + ad + ".mat");
            celik = L("M_FircalanmisCelik"); tavan = L("M_TavanPaneli"); zemin = L("M_EpoksiZemin"); kablo = L("M_KabloKanali");
            koyu = L("M_KoyuMetal"); dolap = L("M_LabDolap"); kati = L("M_KatiYuzey"); seritBeyaz = L("M_SeritBeyaz");
            seritTurkuaz = L("M_SeritTurkuaz"); difuzor = L("M_TavanDifuzor"); rafArka = L("M_RafArkaIsik");
            ekran = L("M_TerminalEkran"); buton = L("M_PanelButon"); fanusIc = L("M_FanusIc"); rafLed = L("M_RafAltiLED");
            cam = L("M_FanusCam_Fresnel"); sivi = L("M_NumuneSivi"); logoCerceve = L("M_LogoCerceve");
            lambaMat = L("M_TitreyenLamba"); uyari = L("M_GrafikUyari");
            fanusMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/TruckLab/Meshes/FanusCam.asset");

            // Rampa için sarı uyarı şeridi
            string yol = Mat + "M_UyariSari.mat";
            sari = AssetDatabase.LoadAssetAtPath<Material>(yol);
            if (sari == null) { sari = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(sari, yol); }
            sari.SetColor("_BaseColor", Hex("#D9A400"));
            sari.SetFloat("_Smoothness", 0.35f);
            EditorUtility.SetDirty(sari);
        }

        // ------------------------------------------------------------------ KABUK

        private static void Kabuk(Transform p)
        {
            var t = Grup("Ortam", p);
            const float k = 0.02f; // kamyonun gizlenen duvarlarının hemen önünde ince kaplama
            Kutu("Zemin", t, new Vector3(0, k * 0.5f, 0), new Vector3(HX * 2f, k, HZ * 2f), zemin);
            Kutu("Tavan", t, new Vector3(0, H - k * 0.5f, 0), new Vector3(HX * 2f, k, HZ * 2f), tavan);
            Kutu("Duvar_Sag", t, new Vector3(HX - k * 0.5f, H * 0.5f, 0), new Vector3(k, H, HZ * 2f), celik);
            Kutu("Duvar_Sol", t, new Vector3(-HX + k * 0.5f, H * 0.5f, 0), new Vector3(k, H, HZ * 2f), celik);
            Kutu("Duvar_On", t, new Vector3(0, H * 0.5f, HZ - k * 0.5f), new Vector3(HX * 2f, H, k), celik);

            for (int i = 0; i < 4; i++)
            {
                float z = -HZ + HZ * 0.5f * (i + 0.5f);
                Kutu("TavanPanel", t, new Vector3(0, H - 0.03f, z), new Vector3(HX * 2f - 0.3f, 0.02f, HZ * 0.5f - 0.08f), tavan);
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                Kutu("KabloKanali_Zemin", t, new Vector3(sx * (HX - 0.07f), 0.05f, 0), new Vector3(0.1f, 0.08f, HZ * 2f - 0.05f), kablo);
                Kutu("KabloKanali_Tavan", t, new Vector3(sx * (HX - 0.06f), H - 0.06f, 0), new Vector3(0.08f, 0.07f, HZ * 2f - 0.05f), kablo);
            }
        }

        // ------------------------------------------------------------------ RAFLAR (sağ duvar)

        private static void Raflar(Transform p, float arkaSinir, float onSinir)
        {
            var mob = Grup("Raflar", p);
            var fanuslar = Grup("Fanuslar", p);
            // Kafes rafı: fanuslar BugCage; satış (B tuşu / PC) için BugShelf altındaki bölmeleri toplar
            var raf = fanuslar.gameObject.AddComponent<BugShelf>();
            var rso = new SerializedObject(raf);
            rso.FindProperty("sellRadius").floatValue = 4f; // kasanın her yerinden B ile satılabilsin
            rso.ApplyModifiedPropertiesWithoutUndo();

            const float derinlik = 0.58f;                   // büyük fanus (çap 0.54) sığsın
            float x = HX - 0.02f - derinlik * 0.5f;
            float onKenar = x - derinlik * 0.5f;
            float[] katlar = { 1.15f, 1.85f };              // böcek ayakta 0.4 m + zıplama: kat arası 0.7 m
            const int grupSayisi = 4;

            float uzunluk = onSinir - arkaSinir;
            float grupGen = Mathf.Min(1.2f, (uzunluk - 0.15f * (grupSayisi - 1)) / grupSayisi);
            float bosluk = (uzunluk - grupGen * grupSayisi) / (grupSayisi - 1);
            int sayac = 1;
            for (int g = 0; g < grupSayisi; g++)
            {
                float gz = arkaSinir + grupGen * 0.5f + g * (grupGen + bosluk);
                var grup = Grup($"RafGrubu_{g + 1}", mob);
                Kutu("ArkaPanel", grup, new Vector3(HX - 0.03f, 1.8f, gz), new Vector3(0.02f, 1.4f, grupGen), rafArka);
                foreach (float s in new[] { -1f, 1f })
                    Kutu("Dikme", grup, new Vector3(x, 1.8f, gz + s * (grupGen * 0.5f - 0.015f)), new Vector3(derinlik, 1.4f, 0.03f), koyu);
                Katı("AltDolap", grup, new Vector3(x, 0.55f, gz), new Vector3(derinlik, 1.1f, grupGen), dolap);
                Kutu("DolapDerz", grup, new Vector3(onKenar - 0.001f, 0.55f, gz), new Vector3(0.004f, 1.06f, 0.01f), koyu);
                foreach (int s in new[] { -1, 1 })
                    Kutu("Kulp", grup, new Vector3(onKenar - 0.01f, 0.95f, gz + s * grupGen * 0.25f), new Vector3(0.02f, 0.02f, 0.22f), koyu);
                Kutu("SupurgelikLED", grup, new Vector3(onKenar - 0.02f, 0.012f, gz), new Vector3(0.015f, 0.008f, grupGen - 0.05f), seritTurkuaz);
                Isik("RafDolgu", grup, new Vector3(onKenar - 0.35f, 1.6f, gz), LightType.Point, "#5FD3C3", 0.35f, 1.6f);

                for (int k = 0; k < katlar.Length; k++)
                {
                    float ky = katlar[k];
                    Kutu($"Kat_{k + 1}", grup, new Vector3(x, ky - 0.03f, gz), new Vector3(derinlik, 0.06f, grupGen - 0.04f), koyu);
                    Kutu("RafAltiLED", grup, new Vector3(onKenar + 0.03f, ky - 0.065f, gz), new Vector3(0.03f, 0.01f, grupGen - 0.1f), rafLed);
                    float aralik = grupGen / 2f;
                    foreach (float f in new[] { -0.5f, 0.5f })
                    {
                        float fz = gz + f * aralik;
                        // Etiket kat plakasının ön yüzünde (koridordan okunur); BugCage tür/doluluk yazar
                        var etiket = Yazi("Etiket", fanuslar, new Vector3(onKenar - 0.004f, ky - 0.03f, fz), Quaternion.Euler(0, 90f, 0),
                            new Vector2(aralik - 0.04f, 0.055f), 0.3f, "B-" + sayac.ToString("00"), TextAlignmentOptions.Center, "#9FEDE0");
                        Fanus(fanuslar, new Vector3(x, ky, fz), $"B-{sayac++:00}", etiket);
                    }
                }
            }
        }

        /// <summary>
        /// Böcek konulabilen büyük cam fanus: koyu taban + silindir cam + kubbe.
        /// BugCage (iç hacim trigger'ı), camın collider halkası, E için Interactable katmanında etkileşim kutusu,
        /// ve cama vurma efekti (FanusVurma).
        /// </summary>
        private static void Fanus(Transform p, Vector3 poz, string ad, TMP_Text etiket)
        {
            const float olcek = 2.0f;                 // FanusCam mesh'i: r 0.12, gövde 0.2, kubbe 0.08 → x2
            const float camY = 0.06f;
            float r = 0.12f * olcek, ust = camY + (0.2f + 0.08f) * olcek;

            var kok = Grup("Fanus_" + ad, p);
            kok.localPosition = poz;
            Prim(PrimitiveType.Cylinder, "Taban", kok, new Vector3(0, camY * 0.5f, 0), new Vector3(r * 2f + 0.06f, camY * 0.5f, r * 2f + 0.06f), koyu, Quaternion.identity);
            Prim(PrimitiveType.Cylinder, "IcIsikDiski", kok, new Vector3(0, camY + 0.003f, 0), new Vector3(r * 1.5f, 0.003f, r * 1.5f), fanusIc, Quaternion.identity);

            var c = new GameObject("Cam");
            c.transform.SetParent(kok, false);
            c.transform.localPosition = new Vector3(0, camY, 0);
            c.transform.localScale = Vector3.one * olcek;
            c.AddComponent<MeshFilter>().sharedMesh = fanusMesh;
            var mr = c.AddComponent<MeshRenderer>();
            mr.sharedMaterial = cam;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var isik = Isik("IcIsik", kok, new Vector3(0, ust - 0.12f, 0), LightType.Point, "#6FD6C8", 0.8f, 1.0f);

            // Camın fiziksel duvarı: halka kutular (kalın: atılan böcek ince camdan geçmesin), kapak ve zemin
            var duvar = Grup("CamCollider", kok);
            const int parca = 12;
            const float kalinlik = 0.08f;
            float halkaR = r - 0.005f + kalinlik * 0.5f;
            float parcaGen = 2f * Mathf.PI * halkaR / parca + 0.02f;
            for (int i = 0; i < parca; i++)
            {
                float a = i * Mathf.PI * 2f / parca;
                var yon = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var bc = new GameObject("Duvar").AddComponent<BoxCollider>();
                bc.transform.SetParent(duvar, false);
                bc.transform.localPosition = yon * halkaR + Vector3.up * ((camY + ust) * 0.5f);
                bc.transform.localRotation = Quaternion.LookRotation(yon);
                bc.size = new Vector3(parcaGen, ust - camY, kalinlik);
            }
            var kapak = new GameObject("Kapak").AddComponent<BoxCollider>();
            kapak.transform.SetParent(duvar, false);
            kapak.transform.localPosition = Vector3.up * (ust - 0.06f);
            kapak.size = new Vector3(r * 2f + 0.1f, 0.08f, r * 2f + 0.1f);
            var zeminC = new GameObject("Zemin").AddComponent<BoxCollider>();
            zeminC.transform.SetParent(duvar, false);
            zeminC.transform.localPosition = Vector3.up * (camY - 0.04f);
            zeminC.size = new Vector3(r * 2f + 0.1f, 0.08f, r * 2f + 0.1f);

            // Kafes: iç hacim trigger'ı (BugCage böcekleri bununla sayar)
            var alan = kok.gameObject.AddComponent<BoxCollider>();
            alan.isTrigger = true;
            alan.center = new Vector3(0f, (camY + ust) * 0.5f, 0f);
            alan.size = new Vector3(r * 1.7f, ust - camY - 0.04f, r * 1.7f);
            var kafes = kok.gameObject.AddComponent<BugCage>();
            var so = new SerializedObject(kafes);
            so.FindProperty("capacity").intValue = 2;
            so.FindProperty("label").objectReferenceValue = etiket;
            so.ApplyModifiedPropertiesWithoutUndo();

            // E ile etkileşim (PlayerInteraction Interactable katmanında arar)
            var etk = new GameObject("Etkilesim") { layer = LayerMask.NameToLayer("Interactable") };
            etk.transform.SetParent(kok, false);
            var ebc = etk.AddComponent<BoxCollider>();
            ebc.isTrigger = true;
            ebc.center = alan.center;
            ebc.size = new Vector3(r * 2f + 0.04f, ust + 0.02f, r * 2f + 0.04f);

            var vurma = kok.gameObject.AddComponent<FanusVurma>();
            var vso = new SerializedObject(vurma);
            vso.FindProperty("cam").objectReferenceValue = c.transform;
            vso.FindProperty("icIsik").objectReferenceValue = isik;
            vso.FindProperty("icYaricap").floatValue = r - 0.005f;
            vso.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void OnDuvarLogo(Transform p)
        {
            var v = Grup("OnDuvarLogo", p);
            float z = HZ - 0.03f;
            Kutu("LogoPanel", v, new Vector3(0f, 1.75f, z), new Vector3(1.9f, 0.7f, 0.02f), koyu);
            Kutu("LogoCerceve_Ust", v, new Vector3(0f, 2.1f, z - 0.015f), new Vector3(1.9f, 0.012f, 0.01f), logoCerceve);
            Kutu("LogoCerceve_Alt", v, new Vector3(0f, 1.4f, z - 0.015f), new Vector3(1.9f, 0.012f, 0.01f), logoCerceve);
            Yazi("LogoYazi", v, new Vector3(0f, 1.78f, z - 0.016f), Quaternion.identity,
                new Vector2(1.8f, 0.55f), 1.5f, "<b>PESTCO</b>\n<size=45%>NUMUNE TOPLAMA VE KARANTİNA</size>", TextAlignmentOptions.Center, "#9FEDE0");
            Isik("LogoIsigi", v, new Vector3(0f, 1.75f, z - 0.5f), LightType.Point, "#5FD3C3", 0.5f, 1.8f);
        }

        // ------------------------------------------------------------------ SOL DUVAR

        private struct Duzen { public float terminalZ, masaZ, panelZ; }

        private static Duzen SolDuzen(float arkaSinir, float onSinir)
        {
            // Arkadan öne: terminal (1.0 m) – masa (2.0 m) – kontrol paneli (1.6 m)
            float bos = Mathf.Max(0.04f, (onSinir - arkaSinir - 4.6f) / 2f);
            float z = arkaSinir;
            var d = new Duzen();
            d.terminalZ = z + 0.5f; z += 1.0f + bos;
            d.masaZ = z + 1.0f; z += 2.0f + bos;
            d.panelZ = z + 0.8f;
            return d;
        }

        private static void Masa(Transform p, float mz)
        {
            // 2.0 x 0.8, üst yüzey 0.9 m; sol duvar boyunca, koridor ~1.9 m
            var m = Grup("CalismaMasasi", p);
            m.localPosition = new Vector3(-HX + 0.42f, 0f, mz);
            Katı("Ust", m, new Vector3(0, 0.875f, 0), new Vector3(0.8f, 0.05f, 2.0f), kati);
            Katı("AdaGovde", m, new Vector3(0f, 0.43f, 0f), new Vector3(0.7f, 0.8f, 1.9f), dolap);
            Kutu("MasaLED", m, new Vector3(0.36f, 0.84f, 0), new Vector3(0.01f, 0.012f, 1.9f), rafLed);
            Kutu("AdaLED", m, new Vector3(0.36f, 0.012f, 0f), new Vector3(0.015f, 0.008f, 1.85f), seritTurkuaz);
            const float ust = 0.9f;

            var mik = Grup("Mikroskop", m);
            mik.localPosition = new Vector3(0.05f, ust, 0.55f);
            mik.localRotation = Quaternion.Euler(0f, 90f, 0f); // göz merceği koridora (+X) baksın
            Kutu("Taban", mik, new Vector3(0f, 0.015f, 0f), new Vector3(0.16f, 0.03f, 0.22f), koyu);
            Kutu("Kol", mik, new Vector3(0f, 0.14f, -0.07f), new Vector3(0.04f, 0.24f, 0.05f), koyu);
            Prim(PrimitiveType.Cylinder, "Tup", mik, new Vector3(0f, 0.2f, -0.02f), new Vector3(0.045f, 0.08f, 0.045f), dolap, Quaternion.Euler(-30f, 0f, 0f));
            Kutu("Tabla", mik, new Vector3(0f, 0.09f, 0.01f), new Vector3(0.12f, 0.01f, 0.1f), koyu);
            Kutu("Isik", mik, new Vector3(0f, 0.04f, 0.01f), new Vector3(0.03f, 0.005f, 0.03f), seritBeyaz);

            var tup = Grup("TupRafi", m);
            tup.localPosition = new Vector3(-0.2f, ust, -0.1f);
            Kutu("Raf", tup, new Vector3(0f, 0.03f, 0f), new Vector3(0.1f, 0.06f, 0.36f), dolap);
            for (int i = 0; i < 6; i++)
            {
                float z = -0.15f + i * 0.06f;
                Prim(PrimitiveType.Cylinder, "Tup", tup, new Vector3(0f, 0.09f, z), new Vector3(0.025f, 0.06f, 0.025f), cam, Quaternion.identity);
                Prim(PrimitiveType.Cylinder, "Sivi", tup, new Vector3(0f, 0.06f + (i % 3) * 0.008f, z), new Vector3(0.02f, 0.025f + (i % 3) * 0.008f, 0.02f), sivi, Quaternion.identity);
            }
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector3(0.15f - i * 0.05f, ust + 0.006f, -0.6f + i * 0.13f);
                Prim(PrimitiveType.Cylinder, "Petri", m, pos, new Vector3(0.09f, 0.006f, 0.09f), cam, Quaternion.identity);
                Prim(PrimitiveType.Cylinder, "PetriIc", m, pos + Vector3.up * 0.001f, new Vector3(0.08f, 0.003f, 0.08f), sivi, Quaternion.identity);
            }
            var tablet = Kutu("Tablet", m, new Vector3(0.12f, ust + 0.006f, 0.05f), new Vector3(0.18f, 0.012f, 0.26f), koyu);
            tablet.transform.localRotation = Quaternion.Euler(0f, 10f, 0f);
            Kutu("TabletEkran", tablet.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.1f, 0.9f), ekran);
            // Koridordan (+X) okunur: yazının üstü duvara (-X) baksın
            Yazi("TabletYazi", m, new Vector3(0.12f, ust + 0.0135f, 0.05f), Quaternion.Euler(90f, -90f + 10f, 0f),
                new Vector2(0.22f, 0.15f), 0.12f, "B-07  CANLI\nNABIZ  112\nISI    24.6C\n<color=#E05A4A>! ANORMAL</color>", TextAlignmentOptions.TopLeft);
        }

        private static void Panel(Transform p, float pz, AtmosphereBlender atmosfer)
        {
            float x = -HX + 0.27f;
            var panel = Grup("KontrolPaneli", p);
            Katı("Govde", panel, new Vector3(x, 0.475f, pz), new Vector3(0.5f, 0.95f, 1.6f), dolap);
            Kutu("UstPlaka", panel, new Vector3(x, 0.96f, pz), new Vector3(0.5f, 0.02f, 1.6f), koyu);
            Kutu("SupurgelikLED", panel, new Vector3(x + 0.27f, 0.012f, pz), new Vector3(0.015f, 0.008f, 1.55f), seritTurkuaz);
            for (int i = 0; i < 8; i++)
                Kutu("Buton", panel, new Vector3(x + (i % 2 == 0 ? -0.08f : 0.08f), 0.98f, pz - 0.56f + i * 0.16f),
                    new Vector3(0.05f, 0.02f, 0.05f), i % 3 == 0 ? fanusIc : buton);

            // Duvar monitörleri + içerik
            float mx = -HX + 0.035f;
            Kutu("DuvarMonitor_1", panel, new Vector3(mx, 1.55f, pz - 0.4f), new Vector3(0.03f, 0.45f, 0.7f), ekran);
            Kutu("DuvarMonitor_2", panel, new Vector3(mx, 1.55f, pz + 0.4f), new Vector3(0.03f, 0.45f, 0.7f), ekran);
            float yuz = mx + 0.016f;
            for (int i = 0; i < 12; i++)
            {
                float h = 0.05f + Mathf.PerlinNoise(i * 0.7f, 3.1f) * 0.25f;
                Kutu("Grafik", panel, new Vector3(yuz, 1.38f + h * 0.5f, pz - 0.4f - 0.28f + i * 0.048f), new Vector3(0.004f, h, 0.028f), i == 8 ? uyari : seritTurkuaz);
            }
            Yazi("Monitor1Baslik", panel, new Vector3(yuz + 0.003f, 1.72f, pz - 0.4f), Quaternion.Euler(0f, -90f, 0f),
                new Vector2(0.62f, 0.08f), 0.18f, "AKTIVITE  /  24 SA", TextAlignmentOptions.Left);
            Yazi("Monitor2Yazi", panel, new Vector3(yuz + 0.003f, 1.55f, pz + 0.4f), Quaternion.Euler(0f, -90f, 0f),
                new Vector2(0.62f, 0.4f), 0.17f,
                "<color=#6FD6C8>FANUS DURUMU</color>\nDOLU   19 / 27\nBOS     8\n<color=#E05A4A>UYARI   1  (B-07)</color>\n\nSICAKLIK  24.6C\nNEM       %62",
                TextAlignmentOptions.TopLeft);

            var etk = EtkilesimKutusu("KontrolPaneli_Etkilesim", p, new Vector3(x, 1.05f, pz), new Vector3(0.5f, 0.25f, 1.6f), "Kontrol panelini kullan");
            UnityEventTools.AddVoidPersistentListener(etk.onInteract, atmosfer.PulseScare);
        }

        private static void Terminal(Transform p, float tz)
        {
            float x = -HX + 0.27f;
            var term = Grup("Terminal", p);
            Katı("Masa", term, new Vector3(x, 0.45f, tz), new Vector3(0.5f, 0.9f, 1.0f), dolap);
            Kutu("SupurgelikLED", term, new Vector3(x + 0.27f, 0.012f, tz), new Vector3(0.015f, 0.008f, 0.95f), seritTurkuaz);
            Kutu("MonitorAyak", term, new Vector3(x - 0.1f, 1.0f, tz), new Vector3(0.05f, 0.2f, 0.05f), koyu);
            var kasa = Kutu("MonitorKasa", term, new Vector3(x - 0.1f, 1.32f, tz), new Vector3(0.05f, 0.45f, 0.7f), koyu);
            kasa.transform.localRotation = Quaternion.Euler(0, 0, -8f);
            var ekr = Kutu("Ekran", term, new Vector3(x - 0.07f, 1.32f, tz), new Vector3(0.01f, 0.4f, 0.64f), ekran);
            ekr.transform.localRotation = Quaternion.Euler(0, 0, -8f);
            Kutu("Klavye", term, new Vector3(x + 0.1f, 0.915f, tz), new Vector3(0.18f, 0.02f, 0.45f), kablo);

            var termRot = Quaternion.Euler(0f, 0f, -8f) * Quaternion.Euler(0f, -90f, 0f);
            var yazi = Yazi("TerminalYazi", term, new Vector3(x - 0.07f + 0.0065f, 1.32f, tz), termRot, new Vector2(0.6f, 0.36f), 0.2f,
                "<color=#6FD6C8>PESTCO BIYOLAB // NUMUNE TAKIP</color>\n" +
                "> B-03  Blattella g.    STABIL\n" +
                "> B-07  Periplaneta a.  <color=#E05A4A>HAREKETLI</color>\n" +
                "> B-12  Bilinmeyen      <color=#E0B04A>GOZLEM</color>\n" +
                "> B-19  Cimex l.        STABIL\n\n" +
                "<color=#3E6F7A>kapak kilidi: AKTIF   O2: %20.8\nson tarama 00:03:12 once_</color>", TextAlignmentOptions.TopLeft);

            var ekranIsigi = Isik("EkranIsigi", term, new Vector3(x + 0.3f, 1.3f, tz), LightType.Point, "#3FA0B8", 0.35f, 1.6f);

            var ts = term.gameObject.AddComponent<TerminalScreen>();
            var so = new SerializedObject(ts);
            var ek = so.FindProperty("ekranlar"); ek.arraySize = 1; ek.GetArrayElementAtIndex(0).objectReferenceValue = ekr.GetComponent<Renderer>();
            var ic = so.FindProperty("ekranIcerikleri"); ic.arraySize = 1; ic.GetArrayElementAtIndex(0).objectReferenceValue = yazi.gameObject;
            so.FindProperty("ekranIsigi").objectReferenceValue = ekranIsigi;
            so.ApplyModifiedPropertiesWithoutUndo();

            // Terminal: E ile PestOS masaüstünü açar (görev, market, fanus satışı, hesap)
            var pc = term.gameObject.AddComponent<PestPC>();
            var etk = EtkilesimKutusu("Terminal_Etkilesim", p, new Vector3(x - 0.05f, 1.15f, tz), new Vector3(0.45f, 0.65f, 1.0f), "Bilgisayarı kullan");
            UnityEventTools.AddVoidPersistentListener(etk.onInteract, pc.Open);
        }

        private static void Logo(Transform p, float mz)
        {
            var v = Grup("Logo", p);
            float x = -HX + 0.03f;
            Kutu("LogoPanel", v, new Vector3(x, 1.8f, mz), new Vector3(0.02f, 0.5f, 1.4f), koyu);
            Kutu("LogoCerceve_Ust", v, new Vector3(x + 0.015f, 2.05f, mz), new Vector3(0.01f, 0.012f, 1.4f), logoCerceve);
            Kutu("LogoCerceve_Alt", v, new Vector3(x + 0.015f, 1.55f, mz), new Vector3(0.01f, 0.012f, 1.4f), logoCerceve);
            Yazi("LogoYazi", v, new Vector3(x + 0.012f, 1.82f, mz), Quaternion.Euler(0f, -90f, 0f),
                new Vector2(1.3f, 0.4f), 1.0f, "<b>PESTCO</b>\n<size=50%>MOBIL BIYOLOJIK ARASTIRMA</size>", TextAlignmentOptions.Center, "#9FEDE0");
        }

        // ------------------------------------------------------------------ IŞIK / DETAY

        private static void Isiklar(Transform p)
        {
            var g = Grup("Aydinlatma", p);
            float uz = HZ * 2f - 0.6f;
            foreach (float x in new[] { -0.72f, 0.72f })
            {
                Kutu("TavanKanal_Cerceve", g, new Vector3(x, H - 0.025f, 0f), new Vector3(0.32f, 0.03f, uz + 0.1f), koyu);
                Kutu("TavanKanal_Difuzor", g, new Vector3(x, H - 0.042f, 0f), new Vector3(0.24f, 0.006f, uz), difuzor);
                foreach (float z in new[] { -uz * 0.35f, 0f, uz * 0.35f })
                {
                    var l = Isik("TavanSpot", g, new Vector3(x, H - 0.06f, z), LightType.Spot, "#DCE9F2", 2.2f, 3.8f);
                    l.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    l.spotAngle = 125f; l.innerSpotAngle = 40f;
                }
            }
            foreach (float sx in new[] { -1f, 1f })
                foreach (float z in new[] { -uz * 0.35f, 0f, uz * 0.35f })
                {
                    var l = Isik("DuvarYikama", g, new Vector3(sx * (HX - 0.6f), H - 0.08f, z), LightType.Spot, "#CFE2EE", 0.7f, 3.2f);
                    l.transform.localRotation = Quaternion.LookRotation(new Vector3(sx * 0.35f, -1f, 0f));
                    l.spotAngle = 120f; l.innerSpotAngle = 1f;
                }

            TitreyenLamba(g, "TitreyenLamba_Orta", new Vector3(0f, H - 0.1f, -0.9f), 0.8f, 0.12f);
            TitreyenLamba(g, "TitreyenLamba_Terminal", new Vector3(-0.35f, H - 0.1f, -HZ + 2.1f), 0.55f, 0.3f);
        }

        private static void TitreyenLamba(Transform p, string ad, Vector3 poz, float maxYog, float dropout)
        {
            var govde = Prim(PrimitiveType.Cylinder, ad + "_Govde", p, poz + Vector3.up * 0.045f, new Vector3(0.26f, 0.012f, 0.26f), lambaMat, Quaternion.identity);
            var l = Isik(ad, p, poz, LightType.Point, "#D9E8F0", maxYog, 3.2f);
            l.shadows = LightShadows.Soft;
            var fl = l.gameObject.AddComponent<LightFlicker>();
            var so = new SerializedObject(fl);
            so.FindProperty("hedefIsik").objectReferenceValue = l;
            so.FindProperty("emissiveRenderer").objectReferenceValue = govde.GetComponent<Renderer>();
            so.FindProperty("minIntensity").floatValue = maxYog * 0.55f;
            so.FindProperty("maxIntensity").floatValue = maxYog;
            so.FindProperty("dropoutChance").floatValue = dropout;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DuvarDetaylari(Transform p)
        {
            var d = Grup("DuvarDetay", p);
            for (float z = -HZ + 0.4f; z <= HZ - 0.3f; z += 0.8f)
            {
                Kutu("Derz_Sol", d, new Vector3(-HX + 0.022f, H * 0.5f, z), new Vector3(0.006f, H - 0.1f, 0.012f), koyu);
                Kutu("Derz_Sag", d, new Vector3(HX - 0.022f, H * 0.5f, z), new Vector3(0.006f, H - 0.1f, 0.012f), koyu);
            }
            const float bantY = 2.3f;
            Kutu("Bant_Sol", d, new Vector3(-HX + 0.024f, bantY, 0f), new Vector3(0.008f, 0.012f, HZ * 2f - 0.1f), seritTurkuaz);
            Kutu("Bant_Sag", d, new Vector3(HX - 0.024f, bantY, 0f), new Vector3(0.008f, 0.012f, HZ * 2f - 0.1f), seritTurkuaz);
            foreach (float sx in new[] { -1f, 1f })
                Kutu("TavanKoveLED", d, new Vector3(sx * (HX - 0.13f), H - 0.12f, 0f), new Vector3(0.02f, 0.012f, HZ * 2f - 0.2f), seritBeyaz);
            foreach (float z in new[] { -1.2f, 1.2f })
            {
                Kutu("Menfez", d, new Vector3(0f, H - 0.03f, z), new Vector3(0.5f, 0.02f, 0.5f), koyu);
                for (int i = 0; i < 6; i++)
                    Kutu("Kanatcik", d, new Vector3(0f, H - 0.042f, z - 0.2f + i * 0.08f), new Vector3(0.46f, 0.006f, 0.02f), dolap);
            }
        }

        private static ReflectionProbe Probe(Transform p)
        {
            var probe = new GameObject("ReflectionProbe_Ic").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(p, false);
            probe.transform.localPosition = new Vector3(0, H * 0.5f, 0);
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.boxProjection = true;
            probe.size = new Vector3(HX * 2f, H, HZ * 2f);
            probe.resolution = 256;
            probe.importance = 2;
            return probe;
        }

        private static AtmosphereBlender PostProcess(Transform p)
        {
            Volume V(string ad, string profil, int oncelik)
            {
                var go = new GameObject(ad) { layer = 2 }; // Ignore Raycast
                go.transform.SetParent(p, false);
                go.transform.localPosition = new Vector3(0, H * 0.5f, 0);
                var bc = go.AddComponent<BoxCollider>();
                bc.isTrigger = true;
                bc.size = new Vector3(HX * 2f, H, HZ * 2f);
                var v = go.AddComponent<Volume>();
                v.isGlobal = false;
                v.priority = oncelik;
                v.blendDistance = 0.3f;
                v.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Prof + profil + ".asset");
                return v;
            }
            V("Lab Volume", "VP_TruckLab_Taban", 15);
            var korku = V("Lab Korku Volume", "VP_TruckLab_Korku", 30);
            korku.weight = 0f;
            var blender = korku.gameObject.AddComponent<AtmosphereBlender>();
            var so = new SerializedObject(blender);
            so.FindProperty("korkuVolume").objectReferenceValue = korku;
            so.ApplyModifiedPropertiesWithoutUndo();
            return blender;
        }

        // ------------------------------------------------------------------ KAMYON SİSTEMLERİ

        /// <summary>Kamyon çocuğunu büyütmeden (dünya boyutu korunur) lab içinde verilen merkeze taşır.</summary>
        private static void YerlestirSinirla(Transform t, Transform lab, Vector3 hedefYerel, Transform kamyon)
        {
            if (t == null) return;
            t.localScale = Vector3.one / kamyon.localScale.x;
            Vector3 merkez = DunyaSinirlari(t).center;
            t.position += lab.TransformPoint(hedefYerel) - merkez;
        }

        private static void SatisAlani(Transform lab)
        {
            var zone = GameObject.Find("TruckSaleZone");
            if (zone == null) return;
            zone.transform.position = lab.TransformPoint(new Vector3(0f, 1.0f, 0f));
            var bc = zone.GetComponent<BoxCollider>();
            bc.center = Vector3.zero;
            bc.size = new Vector3(HX * 2f - 0.2f, 2.0f, HZ * 2f - 0.4f);
        }

        private static void Rampa(Transform lab)
        {
            // Arka kapıdan yere yükleme rampası (karakter eğim sınırı 45°)
            var r = Grup("Rampa", lab);
            float yuk = lab.position.y; // kasa zemini yerden yüksekliği
            float boy = Mathf.Max(2.0f, yuk / Mathf.Tan(34f * Mathf.Deg2Rad));
            float aci = Mathf.Atan2(yuk, boy) * Mathf.Rad2Deg;
            float egik = Mathf.Sqrt(boy * boy + yuk * yuk);
            const float kal = 0.05f;
            var rot = Quaternion.Euler(-aci, 0f, 0f);
            Vector3 orta = new Vector3(0f, -yuk * 0.5f, -HZ - boy * 0.5f) + rot * new Vector3(0f, -kal * 0.5f, 0f);
            var plaka = Prim(PrimitiveType.Cube, "Plaka", r, orta, new Vector3(1.7f, kal, egik), koyu, rot, true);
            foreach (float sx in new[] { -1f, 1f })
                Prim(PrimitiveType.Cube, "UyariSeridi", r, orta + rot * new Vector3(sx * 0.78f, kal * 0.5f + 0.003f, 0f), new Vector3(0.1f, 0.006f, egik - 0.05f), sari, rot);
            for (int i = 1; i < 8; i++)
                Prim(PrimitiveType.Cube, "Kaydirmaz", r, orta + rot * new Vector3(0f, kal * 0.5f + 0.006f, -egik * 0.5f + i * egik / 8f), new Vector3(1.4f, 0.012f, 0.03f), kablo, rot);
        }

        private static void TestEsyalariniKenaraAl(Transform lab)
        {
            // Rampanın önündeki test böcekleri ve kabin üstündeki test kutusu
            var kafes = GameObject.Find("KafesBocekleri");
            if (kafes != null)
            {
                var b = DunyaSinirlari(kafes.transform);
                Vector3 rampaUcu = lab.TransformPoint(new Vector3(0f, 0f, -HZ - 2.5f));
                if (Mathf.Abs(b.center.x - rampaUcu.x) < 1.6f && Mathf.Abs(b.center.z - rampaUcu.z) < 3f)
                    kafes.transform.position += new Vector3(3.4f, 0f, 0f); // rampanın yanına
            }
            var p4 = GameObject.Find("PullableDemo/Pullable_4");
            if (p4 != null && p4.transform.position.y > 1.5f) p4.transform.position = new Vector3(37.9f, 0.3f, -15.3f);
        }

        private static void OyuncuyuKoy(Transform lab)
        {
            var oyuncu = GameObject.Find("Player");
            if (oyuncu == null) return;
            oyuncu.transform.SetPositionAndRotation(lab.TransformPoint(new Vector3(0f, 0.96f, -HZ + 0.8f)), lab.rotation);
        }

        /// <summary>
        /// PC uygulamalarının ihtiyaç duyduğu yöneticiler: ContractGenerator objesine sahne bozulmasında yanlış bileşen
        /// (FedBugVisual) yazılmıştı, gerçek bileşen yoktu; ToolManager hiç eklenmemişti.
        /// </summary>
        private static void YoneticileriOnar()
        {
            if (Object.FindFirstObjectByType<ContractGenerator>(FindObjectsInactive.Include) == null)
            {
                var go = GameObject.Find("ContractGenerator") ?? new GameObject("ContractGenerator");
                foreach (var yanlis in go.GetComponents<FedBugVisual>()) Object.DestroyImmediate(yanlis);
                go.AddComponent<ContractGenerator>();
                Debug.Log("[KamyonLab] ContractGenerator onarıldı.");
            }
            if (Object.FindFirstObjectByType<ToolManager>(FindObjectsInactive.Include) == null)
            {
                var go = new GameObject("ToolManager");
                var yon = GameObject.Find("_Managers");
                if (yon != null) go.transform.SetParent(yon.transform, false);
                var tm = go.AddComponent<ToolManager>();
                var araclar = new List<ToolData>();
                foreach (var guid in AssetDatabase.FindAssets("t:ToolData", new[] { "Assets/_Project/Data/Tools" }))
                    araclar.Add(AssetDatabase.LoadAssetAtPath<ToolData>(AssetDatabase.GUIDToAssetPath(guid)));
                araclar.Sort((a, b) => a.price.CompareTo(b.price));
                var so = new SerializedObject(tm);
                var liste = so.FindProperty("allTools");
                liste.arraySize = araclar.Count;
                for (int i = 0; i < araclar.Count; i++) liste.GetArrayElementAtIndex(i).objectReferenceValue = araclar[i];
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[KamyonLab] ToolManager eklendi ({araclar.Count} alet).");
            }
        }

        /// <summary>Arka kapıya rulo kepenk seslerini ve 3B AudioSource'u bağlar.</summary>
        private static void KapiSesleri(Transform kamyon)
        {
            var kapi = kamyon.Find("ExitDoor");
            if (kapi == null || !kapi.TryGetComponent(out RollUpDoor rd)) return;
            var so = new SerializedObject(rd);
            float sure = so.FindProperty("animationDuration").floatValue;
            RuloKepenkSesi.Uret(sure, out var ac, out var kap);

            var kaynak = kapi.GetComponent<AudioSource>();
            if (kaynak == null) kaynak = kapi.gameObject.AddComponent<AudioSource>();
            kaynak.playOnAwake = false;
            kaynak.spatialBlend = 1f;
            kaynak.rolloffMode = AudioRolloffMode.Logarithmic;
            kaynak.minDistance = 1.5f;
            kaynak.maxDistance = 20f;
            kaynak.volume = 0.9f;

            so.FindProperty("openSound").objectReferenceValue = ac;
            so.FindProperty("closeSound").objectReferenceValue = kap;
            so.FindProperty("audioSource").objectReferenceValue = kaynak;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<ScriptableRendererFeature> KonturOzellikleri()
        {
            var liste = new List<ScriptableRendererFeature>();
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                var dl = new SerializedObject(urp).FindProperty("m_RendererDataList");
                for (int i = 0; i < dl.arraySize; i++)
                    if (dl.GetArrayElementAtIndex(i).objectReferenceValue is ScriptableRendererData rd)
                        foreach (var f in rd.rendererFeatures)
                            if (f != null && f.name == "DepthInk") liste.Add(f);
            }
            return liste;
        }

        // ------------------------------------------------------------------ YARDIMCILAR

        private static Bounds DunyaSinirlari(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>(true);
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.zero);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        private static Transform Grup(string ad, Transform p)
        {
            var t = new GameObject(ad).transform;
            t.SetParent(p, false);
            return t;
        }

        private static Interactable EtkilesimKutusu(string ad, Transform p, Vector3 poz, Vector3 boyut, string prompt)
        {
            var go = new GameObject(ad) { layer = LayerMask.NameToLayer("Interactable") };
            go.transform.SetParent(p, false);
            go.transform.localPosition = poz;
            go.AddComponent<BoxCollider>().size = boyut;
            var etk = go.AddComponent<Interactable>();
            etk.SetPrompt(prompt);
            return etk;
        }

        private static GameObject Kutu(string ad, Transform p, Vector3 poz, Vector3 olcek, Material mat)
            => Prim(PrimitiveType.Cube, ad, p, poz, olcek, mat, Quaternion.identity);

        // Oyuncunun içinden geçmemesi gereken mobilya (collider'lı)
        private static GameObject Katı(string ad, Transform p, Vector3 poz, Vector3 olcek, Material mat)
            => Prim(PrimitiveType.Cube, ad, p, poz, olcek, mat, Quaternion.identity, true);

        private static GameObject Prim(PrimitiveType tip, string ad, Transform p, Vector3 poz, Vector3 olcek, Material mat, Quaternion rot, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(tip);
            go.name = ad;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>()); // kasanın collider'ları kamyonda
            go.transform.SetParent(p, false);
            go.transform.localPosition = poz;
            go.transform.localRotation = rot;
            go.transform.localScale = olcek;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            if (mat != null && mat.IsKeywordEnabled("_EMISSION")) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        private static Light Isik(string ad, Transform p, Vector3 poz, LightType tip, string renk, float yogunluk, float menzil)
        {
            var l = new GameObject(ad).AddComponent<Light>();
            l.transform.SetParent(p, false);
            l.transform.localPosition = poz;
            l.type = tip;
            l.color = Hex(renk);
            l.intensity = yogunluk;
            l.range = menzil;
            l.shadows = LightShadows.None;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        private static TextMeshPro Yazi(string ad, Transform p, Vector3 poz, Quaternion rot, Vector2 boyut, float punto,
            string metin, TextAlignmentOptions hiza, string renk = "#8FD8CC")
        {
            var t = new GameObject(ad).AddComponent<TextMeshPro>();
            t.transform.SetParent(p, false);
            t.transform.localPosition = poz;
            t.transform.localRotation = rot;
            t.rectTransform.sizeDelta = boyut;
            t.fontSize = punto;
            t.text = metin;
            t.alignment = hiza;
            t.color = Hex(renk);
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.richText = true;
            return t;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}

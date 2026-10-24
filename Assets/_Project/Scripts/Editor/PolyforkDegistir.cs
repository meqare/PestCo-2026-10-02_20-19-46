using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PestCo.EditorTools
{
    /// <summary>
    /// Sahnedeki küp/silindirden kurulmuş eşyaların görünüşünü polyfork.dev'den indirilen düşük poligonlu modellerle değiştirir.
    /// Eşyanın kendisi (çarpışma, fizik, scriptler) korunur: yalnızca eski "ilkel" parçaların Renderer'ı kapatılır,
    /// yeni model "PF_" önekli çocuk olarak eklenir. Modeller köşe rengi taşır; eski parçanın PestCo/Toon materyali
    /// kopyalanıp _UseVertexColor = 1 yapılır, böylece Courage/çizgi film ışığı aynen uygulanır.
    /// Menü: PestCo/Polyfork/Uygula ve PestCo/Polyfork/Geri Al. Her şey Undo'ya kaydedilir.
    /// </summary>
    public static class PolyforkDegistir
    {
        private const string Klasor = "Assets/_Project/Models/Polyfork";
        private const string MatKlasor = Klasor + "/Materials";
        private const string Onek = "PF_";

        private static readonly string[] IlkelMeshler = { "Box_", "Cube", "Cylinder", "Sphere", "Capsule", "LowPoly", "Ico", "Quad" };

        private static int degisen, eklenen;
        private static readonly List<string> rapor = new List<string>();

        [MenuItem("PestCo/Polyfork/Uygula")]
        public static string Uygula()
        {
            degisen = eklenen = 0;
            rapor.Clear();
            if (!AssetDatabase.IsValidFolder(MatKlasor)) AssetDatabase.CreateFolder(Klasor, "Materials");
            Undo.SetCurrentGroupName("Polyfork modellerini uygula");
            int grup = Undo.GetCurrentGroup();

            // ---- Ev
            foreach (var t in Bul("Armchair")) Degistir(t, "armchair-980c7c");
            foreach (var t in Bul("SideTable")) Degistir(t, "side-table-2ea5c0");
            foreach (var t in Bul("FloorLamp")) Degistir(t, "floor-lamp-f0ba3f");
            foreach (var t in Bul("Plant"))
            {
                var b = YerelSinir(t, IlkelRendererlar(t));
                if (b.size.y <= 0.001f) continue;
                // Dünya yüksekliğine göre: 0.6 m ve üstü yapraklı ev bitkisi, altı küçük saksı (büyütülen saksı kaba bir küpe dönüşüyor)
                float h = b.size.y * t.lossyScale.y;
                Degistir(t, h >= 0.6f ? "floor-houseplant-8fad17" : "planter-f0dab0");
            }
            // Kitaplıklar değiştirilmez: içlerindeki tutulabilir kitaplar eski raflara göre dizili, raf gidince havada kalıyorlar.
            foreach (var t in Bul("Chair")) Degistir(t, "dining-chair-530532");
            foreach (var t in Bul("TVUnit")) TvUnitesi(t);

            // ---- Taşınabilir kutular (kök BoxCollider'a birebir oturur)
            foreach (var t in Bul("Grab_Kutu_Hafif")) Degistir(t, "cardboard-delivery-box-59cc68", tamOtur: true);
            foreach (var t in Bul("Grab_Kutu_Orta")) Degistir(t, "wooden-crate-e46521", tamOtur: true);
            foreach (var t in Bul("Grab_Kutu_Agir")) Degistir(t, "supply-crate-57aa8d", tamOtur: true);

            // ---- Kamyon labı
            foreach (var t in Bul("CalismaMasasi")) CalismaMasasi(t);
            foreach (var t in Bul("Terminal")) Terminal(t);

            // Mutfakta zaten paket modeli bir tost makinesi var; Polyfork tost makinesi eklenmez.

            Undo.CollapseUndoOperations(grup);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            AssetDatabase.SaveAssets();
            string s = $"Değiştirilen: {degisen}, eklenen: {eklenen}\n" + string.Join("\n", rapor);
            Debug.Log("[Polyfork] " + s);
            return s;
        }

        [MenuItem("PestCo/Polyfork/Geri Al")]
        public static string GeriAl()
        {
            int n = 0;
            Undo.SetCurrentGroupName("Polyfork modellerini geri al");
            foreach (var pf in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(x => x != null && x.name.StartsWith(Onek)).ToList())
            {
                var ebeveyn = pf.parent;
                Undo.DestroyObjectImmediate(pf.gameObject);
                n++;
                if (ebeveyn == null) continue;
                foreach (var r in ebeveyn.GetComponentsInChildren<Renderer>(true))
                    if (!r.enabled && Ilkel(r)) { Undo.RecordObject(r, "Polyfork geri al"); r.enabled = true; }
                if (ebeveyn.name == "Terminal") TerminalEkraniEski(ebeveyn);
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            return $"Kaldırılan Polyfork modeli: {n}";
        }

        // ------------------------------------------------------------------ GENEL DEĞİŞTİRME

        private static IEnumerable<Transform> Bul(string ad) =>
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(t => t.name == ad && t.gameObject.scene.IsValid() && t.Find(Onek + "x") == null && !t.Cast<Transform>().Any(c => c.name.StartsWith(Onek)))
                .ToList();

        private static bool Ilkel(Renderer r)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return false;
            string m = mf.sharedMesh.name;
            return IlkelMeshler.Any(p => m.StartsWith(p));
        }

        // Eşyanın kendi ilkel parçaları: tutulabilir alt nesneler (Rigidbody), kitaplar, yazılar ve önceden eklenmiş modeller hariç
        private static List<Renderer> IlkelRendererlar(Transform kok)
        {
            var liste = new List<Renderer>();
            foreach (var r in kok.GetComponentsInChildren<Renderer>(false))
            {
                if (!r.enabled || !Ilkel(r)) continue;
                bool atla = false;
                for (var p = r.transform; p != null && p != kok; p = p.parent)
                {
                    if (p.name.StartsWith(Onek) || p.GetComponent<Rigidbody>() != null || p.name.ToLowerInvariant().Contains("book") || p.name.ToLowerInvariant().Contains("kitap")) { atla = true; break; }
                }
                if (!atla) liste.Add(r);
            }
            return liste;
        }

        // Renderer'ların kök yerel uzayındaki ortak sınırı
        private static Bounds YerelSinir(Transform kok, IEnumerable<Renderer> rs)
        {
            bool ilk = true;
            var b = new Bounds();
            foreach (var r in rs)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var k = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = kok.InverseTransformPoint(r.transform.TransformPoint(k));
                    if (ilk) { b = new Bounds(p, Vector3.zero); ilk = false; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        private static Bounds ModelSiniri(GameObject model)
        {
            bool ilk = true;
            var b = new Bounds();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var k = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = model.transform.InverseTransformPoint(mf.transform.TransformPoint(k));
                    if (ilk) { b = new Bounds(p, Vector3.zero); ilk = false; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        /// <summary>Eşyanın ilkel parçalarını gizler, modeli aynı yere oturtur. tamOtur: eksen başına ölçek (kutular), aksi halde oranı korur.</summary>
        private static GameObject Degistir(Transform hedef, string id, bool tamOtur = false)
        {
            var eski = IlkelRendererlar(hedef);
            if (eski.Count == 0) { rapor.Add($"atlandı (ilkel parça yok): {Yol(hedef)}"); return null; }
            var sinir = YerelSinir(hedef, eski);
            var sablon = Sablon(eski);
            var model = ModelEkle(hedef, id, sablon);
            if (model == null) return null;

            var mb = ModelSiniri(model);
            // Uzun kenarlar aynı eksene gelsin (0 ya da 90 derece)
            bool hedefXUzun = sinir.size.x >= sinir.size.z, modelXUzun = mb.size.x >= mb.size.z;
            float yaw = (Mathf.Abs(sinir.size.x - sinir.size.z) > 0.08f && hedefXUzun != modelXUzun) ? 90f : 0f;
            model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 boy = yaw == 0f ? mb.size : new Vector3(mb.size.z, mb.size.y, mb.size.x);
            Vector3 olcek;
            if (tamOtur)
            {
                olcek = new Vector3(sinir.size.x / boy.x, sinir.size.y / boy.y, sinir.size.z / boy.z);
                if (yaw != 0f) olcek = new Vector3(olcek.z, olcek.y, olcek.x);
            }
            else
            {
                float s = sinir.size.y / boy.y;
                float taban = Mathf.Min(sinir.size.x / boy.x, sinir.size.z / boy.z);
                s = Mathf.Min(s, taban * 1.15f); // taban alanı eskisinden fazla taşmasın
                olcek = Vector3.one * s;
            }
            model.transform.localScale = olcek;
            // Taban ortası eski sınırın taban ortasına
            var mbDonuk = ModelSiniriKok(model, hedef);
            var hedefTaban = new Vector3(sinir.center.x, sinir.min.y, sinir.center.z);
            var modelTaban = new Vector3(mbDonuk.center.x, mbDonuk.min.y, mbDonuk.center.z);
            model.transform.localPosition += hedefTaban - modelTaban;

            Gizle(eski);
            degisen++;
            rapor.Add($"{Yol(hedef)} → {id} (ölçek {olcek.x:0.00}, dönüş {yaw:0})");
            return model;
        }

        // Model sınırının hedef yerel uzayındaki karşılığı (yerleştirmeden sonra)
        private static Bounds ModelSiniriKok(GameObject model, Transform kok)
        {
            bool ilk = true;
            var b = new Bounds();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                var mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var k = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = kok.InverseTransformPoint(mf.transform.TransformPoint(k));
                    if (ilk) { b = new Bounds(p, Vector3.zero); ilk = false; } else b.Encapsulate(p);
                }
            }
            return b;
        }

        private static GameObject ModelEkle(Transform ebeveyn, string id, Material sablon)
        {
            var kaynak = AssetDatabase.LoadAssetAtPath<GameObject>($"{Klasor}/{id}.glb");
            if (kaynak == null) { rapor.Add("BULUNAMADI: " + id); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(kaynak, ebeveyn.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(go, "Polyfork modeli");
            go.name = Onek + id;
            go.transform.SetParent(ebeveyn, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            go.layer = ebeveyn.gameObject.layer;
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = ebeveyn.gameObject.layer;
            var mat = ToonMateryal(sablon);
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            return go;
        }

        private static void Gizle(IEnumerable<Renderer> rs)
        {
            foreach (var r in rs) { Undo.RecordObject(r, "Polyfork gizle"); r.enabled = false; }
        }

        // ------------------------------------------------------------------ MATERYAL

        private static Material Sablon(IEnumerable<Renderer> rs)
        {
            var toon = Shader.Find("PestCo/Toon");
            return rs.Select(r => r.sharedMaterial).Where(m => m != null && m.shader == toon)
                     .GroupBy(m => m).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        }

        private static readonly Dictionary<Material, Material> matOnbellek = new Dictionary<Material, Material>();

        // Eski materyalin çizgi film ayarlarını koruyan, rengi köşe renginden alan kopya (asset olarak kaydedilir)
        private static Material ToonMateryal(Material sablon)
        {
            string ad = "PF_" + (sablon != null ? sablon.name : "Varsayilan");
            string yol = $"{MatKlasor}/{ad}.mat";
            var hazir = AssetDatabase.LoadAssetAtPath<Material>(yol);
            if (hazir != null) return hazir;
            Material m;
            if (sablon != null)
            {
                m = new Material(sablon);
                Color b = sablon.GetColor("_BaseColor"), s = sablon.GetColor("_ShadowColor");
                // Gölge tonu: eski gölge/aydınlık oranı (renk köşe renginden gelecek)
                var oran = new Color(Oran(s.r, b.r), Oran(s.g, b.g), Oran(s.b, b.b), 1f);
                m.SetColor("_BaseColor", Color.white);
                m.SetColor("_ShadowColor", oran);
            }
            else
            {
                m = new Material(Shader.Find("PestCo/Toon"));
                m.SetColor("_BaseColor", Color.white);
                m.SetColor("_ShadowColor", new Color(0.62f, 0.6f, 0.74f));
            }
            m.SetTexture("_BaseMap", null);
            m.SetFloat("_DetailAmt", 0f);
            m.SetFloat("_UseVertexColor", 1f);
            m.name = ad;
            AssetDatabase.CreateAsset(m, yol);
            return m;
        }

        private static float Oran(float s, float b) => b > 0.02f ? Mathf.Clamp(s / b, 0.35f, 1f) : 0.62f;

        // ------------------------------------------------------------------ ÖZEL DURUMLAR

        // TV ünitesi: alçak dolap + üstünde düz ekran TV
        private static void TvUnitesi(Transform t)
        {
            var eski = IlkelRendererlar(t);
            if (eski.Count == 0) return;
            var sinir = YerelSinir(t, eski);
            var sablon = Sablon(eski);
            var unite = ModelEkle(t, "low-media-unit-645e61", sablon);
            var tv = ModelEkle(t, "flat-screen-television-97b1f3", sablon);
            if (unite == null || tv == null) return;
            bool hedefXUzun = sinir.size.x >= sinir.size.z;
            float yaw = hedefXUzun ? 0f : 90f;
            float uzun = Mathf.Max(sinir.size.x, sinir.size.z), kisa = Mathf.Min(sinir.size.x, sinir.size.z);
            float s = Mathf.Min(uzun / 2f, kisa / 0.5f * 1.15f, 1.1f);
            foreach (var m in new[] { unite, tv })
            {
                m.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                m.transform.localScale = Vector3.one * s;
            }
            var taban = new Vector3(sinir.center.x, sinir.min.y, sinir.center.z);
            unite.transform.localPosition = taban;
            tv.transform.localPosition = taban + Vector3.up * (0.45f * s);
            Gizle(eski);
            degisen++;
            rapor.Add($"{Yol(t)} → low-media-unit + flat-screen-television (ölçek {s:0.00})");
        }

        // Kamyon çalışma tezgâhı: masa gövdesi ve LED şeritleri yerine laboratuvar tezgâhı (gerçek ölçek), üstteki eşyalar kalır.
        // Yanına tabure, tezgâh üstüne numune kapları + defter + tür plaketi.
        private static void CalismaMasasi(Transform t)
        {
            var eski = new List<Renderer>();
            foreach (var ad in new[] { "Ust", "AdaGovde", "MasaLED", "AdaLED" })
            {
                var c = t.Find(ad);
                if (c != null && c.GetComponent<Renderer>() != null && c.GetComponent<Renderer>().enabled) eski.Add(c.GetComponent<Renderer>());
            }
            if (eski.Count == 0) return;
            var sinir = YerelSinir(t, eski);
            var sablon = Sablon(eski);
            var tezgah = ModelEkle(t, "lab-bench-740e7b", sablon);
            if (tezgah == null) return;
            // Uzun kenar Z boyunca; tezgâh 2 m, masa 2 m
            tezgah.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            tezgah.transform.localPosition = new Vector3(sinir.center.x, 0f, sinir.center.z);
            Gizle(eski);
            degisen++;
            rapor.Add($"{Yol(t)} → lab-bench (gerçek ölçek)");

            // Süsler (masanın yerel uzayında; üst yüzey 0.9 m)
            Ekle(t, "lab-stool-f405c7", sablon, new Vector3(sinir.max.x + 0.25f, 0f, -0.41f), 0f, 1f); // koridoru tıkamasın, tezgâha yakın
            Ekle(t, "sample-container-c5dc43", sablon, new Vector3(-0.12f, 0.905f, -0.82f), 15f, 0.8f);
            Ekle(t, "sample-container-c5dc43", sablon, new Vector3(-0.02f, 0.905f, -0.86f), -10f, 0.7f);
            Ekle(t, "lab-notebook-e60f7a", sablon, new Vector3(0.12f, 0.905f, 0.78f), -12f, 1f);
            Ekle(t, "species-id-plaque-5f73ed", sablon, new Vector3(-0.22f, 0.905f, 0.3f), 90f, 0.8f);
        }

        // Terminal: kutu monitör kasası + ayağı yerine masaüstü monitör; ışıklı ekran ve PestOS yazısı yeni monitörün camına taşınır
        private static void Terminal(Transform t)
        {
            var kasa = t.Find("MonitorKasa"); var ayak = t.Find("MonitorAyak"); var ekran = t.Find("Ekran"); var yazi = t.Find("TerminalYazi");
            if (kasa == null || ayak == null || ekran == null) return;
            var kr = kasa.GetComponent<Renderer>(); var ar = ayak.GetComponent<Renderer>();
            if (!kr.enabled) return;
            var sablon = Sablon(new[] { kr, ar });
            var monitor = ModelEkle(t, "desk-monitor-32cfce", sablon);
            if (monitor == null) return;
            // Eski ekran +X'e bakıyor; modelin önü +Z → 90 derece
            monitor.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            float s = 1.25f;
            monitor.transform.localScale = Vector3.one * s;
            float masaUst = ayak.localPosition.y - ayak.localScale.y * 0.5f;
            monitor.transform.localPosition = new Vector3(kasa.localPosition.x - 0.02f, masaUst, kasa.localPosition.z);

            // Modelin en büyük ön (+Z) yüzünü ekran camı say: ekranı ve yazıyı oraya oturt
            var cam = OnYuz(monitor, t);
            if (cam.size.y > 0.05f)
            {
                Undo.RecordObject(ekran, "Polyfork terminal");
                float eskiY = ekran.localScale.y;
                // Eski ekran 8° geriye eğikti; yeni monitörün camı dik: plakayı ve yazıyı cama paralel yap
                ekran.localRotation = Quaternion.identity;
                ekran.localPosition = new Vector3(cam.max.x + 0.006f, cam.center.y, cam.center.z);
                ekran.localScale = new Vector3(ekran.localScale.x, cam.size.y * 0.92f, cam.size.z * 0.92f);
                if (yazi != null)
                {
                    Undo.RecordObject(yazi, "Polyfork terminal");
                    float k = ekran.localScale.y / Mathf.Max(0.01f, eskiY);
                    var e = yazi.localEulerAngles;
                    yazi.localRotation = Quaternion.Euler(0f, e.y, 0f);
                    yazi.localPosition = new Vector3(ekran.localPosition.x + ekran.localScale.x * 0.5f + 0.01f, ekran.localPosition.y, ekran.localPosition.z);
                    yazi.localScale *= k;
                }
            }
            Gizle(new[] { kr, ar });
            degisen++;
            rapor.Add($"{Yol(t)} → desk-monitor (ekran camı {cam.size.z:0.00}×{cam.size.y:0.00} m)");
        }

        private static void TerminalEkraniEski(Transform t)
        {
            // Geri alma: ekran ve yazı Undo ile geri döner; menüden geri alındıysa yaklaşık eski yerleri
            var kasa = t.Find("MonitorKasa"); var ekran = t.Find("Ekran"); var yazi = t.Find("TerminalYazi");
            if (kasa == null || ekran == null) return;
            Undo.RecordObject(ekran, "Polyfork geri al");
            ekran.localPosition = new Vector3(kasa.localPosition.x + 0.03f, kasa.localPosition.y, kasa.localPosition.z);
            ekran.localScale = new Vector3(0.01f, 0.40f, 0.64f);
            if (yazi != null) { Undo.RecordObject(yazi, "Polyfork geri al"); yazi.localPosition = ekran.localPosition; }
        }

        // Modelin ön yüzündeki (normal ≈ ekran yönü) en büyük düz bölge: üçgenleri normal yönüne göre süz, en öndeki katmanı al
        private static Bounds OnYuz(GameObject model, Transform kok)
        {
            var ileri = kok.InverseTransformDirection(model.transform.forward).normalized;
            var noktalar = new List<Vector3>();
            float enOn = float.MinValue;
            var adaylar = new List<(Vector3 p, float d)>();
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh; var v = mesh.vertices; var tri = mesh.triangles;
                for (int i = 0; i < tri.Length; i += 3)
                {
                    var a = kok.InverseTransformPoint(mf.transform.TransformPoint(v[tri[i]]));
                    var b = kok.InverseTransformPoint(mf.transform.TransformPoint(v[tri[i + 1]]));
                    var c = kok.InverseTransformPoint(mf.transform.TransformPoint(v[tri[i + 2]]));
                    var n = Vector3.Cross(b - a, c - a);
                    if (n.sqrMagnitude < 1e-10f) continue;
                    if (Vector3.Dot(n.normalized, ileri) < 0.95f) continue;
                    float alan = n.magnitude * 0.5f;
                    if (alan < 0.002f) continue;
                    float d = Vector3.Dot(a, ileri);
                    adaylar.Add((a, d)); adaylar.Add((b, d)); adaylar.Add((c, d));
                    enOn = Mathf.Max(enOn, d);
                }
            }
            bool ilk = true;
            var sonuc = new Bounds();
            foreach (var (p, d) in adaylar)
            {
                if (d < enOn - 0.03f) continue;
                if (ilk) { sonuc = new Bounds(p, Vector3.zero); ilk = false; } else sonuc.Encapsulate(p);
            }
            return sonuc;
        }

        // Mutfak tezgâhına, mikrodalganın yanına tost makinesi
        private static void Tost(Transform mikrodalga)
        {
            var rs = mikrodalga.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToList();
            if (rs.Count == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var sag = mikrodalga.right;
            var konum = new Vector3(b.center.x, b.min.y, b.center.z) + sag * (Mathf.Max(b.extents.x, b.extents.z) + 0.28f);
            var model = ModelEkle(mikrodalga.parent != null ? mikrodalga.parent : mikrodalga, "toaster-0d6c5c", null);
            if (model == null) return;
            model.transform.position = konum;
            model.transform.rotation = mikrodalga.rotation;
            eklenen++;
            rapor.Add($"{Yol(mikrodalga)} yanına → toaster");
        }

        private static void Ekle(Transform ebeveyn, string id, Material sablon, Vector3 yerel, float yaw, float olcek)
        {
            var m = ModelEkle(ebeveyn, id, sablon);
            if (m == null) return;
            m.transform.localPosition = yerel;
            m.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            m.transform.localScale = Vector3.one * olcek;
            eklenen++;
            rapor.Add($"{Yol(ebeveyn)} üzerine → {id}");
        }

        private static string Yol(Transform t) => t.parent != null ? t.parent.name + "/" + t.name : t.name;
    }
}

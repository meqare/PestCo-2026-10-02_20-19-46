using System.IO;
using UnityEditor;
using UnityEngine;

namespace PestCo.Truck.EditorTools
{
    /// <summary>
    /// Kamyonet arka kapısı (rulo kepenk) için açılma/kapanma seslerini sentezleyip WAV olarak yazar.
    /// Ses, RollUpDoor animasyonunun hız profiline (SmoothStep) göre lamel takırtısı + tambur uğultusu + son vuruştan oluşur.
    /// Menü: PestCo > Kamyon Lab > Kapı Seslerini Üret
    /// </summary>
    public static class RuloKepenkSesi
    {
        private const int Hz = 44100;
        private const string Klasor = "Assets/_Project/Audio/Truck";

        [MenuItem("PestCo/Kamyon Lab/Kapı Seslerini Üret")]
        public static void MenudenUret() => Uret(1.2f, out _, out _);

        /// <param name="sure">Kapı animasyon süresi (sn); ses bununla senkron olur.</param>
        public static void Uret(float sure, out AudioClip acilis, out AudioClip kapanis)
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Audio")) AssetDatabase.CreateFolder("Assets/_Project", "Audio");
            if (!AssetDatabase.IsValidFolder(Klasor)) AssetDatabase.CreateFolder("Assets/_Project/Audio", "Truck");

            string a = Klasor + "/RuloKepenk_Acilis.wav", k = Klasor + "/RuloKepenk_Kapanis.wav";
            WavYaz(a, Sentezle(sure, true, 1234));
            WavYaz(k, Sentezle(sure, false, 5678));
            AssetDatabase.ImportAsset(a);
            AssetDatabase.ImportAsset(k);
            acilis = AssetDatabase.LoadAssetAtPath<AudioClip>(a);
            kapanis = AssetDatabase.LoadAssetAtPath<AudioClip>(k);
        }

        private static float[] Sentezle(float sure, bool acilis, int tohum)
        {
            var rnd = new System.Random(tohum);
            float Rnd() => (float)rnd.NextDouble();
            float kuyruk = acilis ? 0.35f : 0.6f;
            int n = Mathf.CeilToInt((sure + kuyruk) * Hz);
            var s = new float[n];

            // 1) Mandal / kilit: başta kısa metalik "klak"
            Darbe(s, 0.0f, 0.9f, 2600f, 0.012f, 0.03f, Rnd);
            Darbe(s, 0.045f, 0.5f, 1700f, 0.01f, 0.025f, Rnd);

            // 2) Lamel takırtıları: hız profili 6u(1-u) (SmoothStep türevi), toplam ~22 takırtı
            const int toplamTik = 22;
            float zaman = 0.06f, birikim = 0f;
            const float adim = 0.001f;
            for (float t = 0.06f; t < sure; t += adim)
            {
                float u = t / sure;
                birikim += toplamTik / sure * 6f * u * (1f - u) * adim;
                if (birikim >= 1f)
                {
                    birikim -= 1f;
                    zaman = t + (Rnd() - 0.5f) * 0.012f;
                    float hiz = 6f * u * (1f - u) / 1.5f; // 0..1
                    float guc = (0.35f + 0.45f * hiz) * (0.7f + 0.3f * Rnd());
                    Darbe(s, zaman, guc, 900f + Rnd() * 1600f, 0.006f, 0.018f + Rnd() * 0.01f, Rnd);
                    // lamelin tambura binişi: hemen arkasından daha kısık ikinci tık
                    Darbe(s, zaman + 0.018f + Rnd() * 0.01f, guc * 0.45f, 2200f + Rnd() * 1200f, 0.004f, 0.012f, Rnd);
                }
            }

            // 3) Tambur/yay uğultusu + alçak gürültü (hızla birlikte yükselir)
            float lp = 0f, lp2 = 0f, faz = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Hz;
                float u = Mathf.Clamp01(t / sure);
                float zarf = t < sure ? Mathf.Pow(6f * u * (1f - u) / 1.5f, 0.8f) : 0f;
                float gurultu = Rnd() * 2f - 1f;
                lp += (gurultu - lp) * 0.035f;   // ~250 Hz
                lp2 += (lp - lp2) * 0.08f;
                faz += 2f * Mathf.PI * (acilis ? 70f + 25f * u : 95f - 25f * u) / Hz;
                float ugultu = Mathf.Sin(faz) * 0.5f + Mathf.Sin(faz * 2.02f) * 0.25f;
                s[i] += (lp2 * 2.2f + ugultu * 0.12f) * zarf * 0.55f;
            }

            // 4) Son: açılışta üstte yığılma "tok"u, kapanışta yere çarpan ağır bang + sekme
            if (acilis)
            {
                Bas(s, sure - 0.02f, 0.55f, 110f, 0.09f);
                Darbe(s, sure - 0.02f, 0.5f, 1400f, 0.01f, 0.05f, Rnd);
                Darbe(s, sure + 0.06f, 0.25f, 2000f, 0.006f, 0.03f, Rnd);
            }
            else
            {
                Bas(s, sure - 0.01f, 1.0f, 62f, 0.22f);
                Darbe(s, sure - 0.01f, 1.0f, 650f, 0.02f, 0.28f, Rnd);   // çelik çınlaması
                Darbe(s, sure - 0.005f, 0.7f, 1900f, 0.008f, 0.09f, Rnd);
                Darbe(s, sure + 0.12f, 0.35f, 1500f, 0.006f, 0.05f, Rnd); // sekme
                Bas(s, sure + 0.12f, 0.3f, 80f, 0.08f);
                Darbe(s, sure + 0.22f, 0.15f, 2300f, 0.004f, 0.03f, Rnd);
            }

            // Basit oda yankısı (kasanın içi metal) + normalize
            var yanki = (float[])s.Clone();
            int[] gecikme = { 1301, 1889, 2711 };
            float[] kazanc = { 0.22f, 0.15f, 0.1f };
            for (int g = 0; g < gecikme.Length; g++)
                for (int i = gecikme[g]; i < n; i++) yanki[i] += s[i - gecikme[g]] * kazanc[g];
            float tepe = 0.0001f;
            foreach (float v in yanki) tepe = Mathf.Max(tepe, Mathf.Abs(v));
            for (int i = 0; i < n; i++)
            {
                float sonda = i > n - 2205 ? (n - i) / 2205f : 1f; // son 50 ms sönüm
                yanki[i] = yanki[i] / tepe * 0.9f * sonda;
            }
            return yanki;
        }

        // Metalik darbe: gürültü patlaması + sönümlü rezonans
        private static void Darbe(float[] s, float t0, float guc, float frekans, float gurultuSure, float cinlamaSure, System.Func<float> rnd)
        {
            int bas = Mathf.Max(0, (int)(t0 * Hz));
            int uz = (int)((gurultuSure + cinlamaSure) * 6f * Hz);
            float f2 = frekans * (1.48f + rnd() * 0.1f);
            for (int i = 0; i < uz && bas + i < s.Length; i++)
            {
                float t = i / (float)Hz;
                float gurultu = (rnd() * 2f - 1f) * Mathf.Exp(-t / gurultuSure);
                float cinlama = (Mathf.Sin(2f * Mathf.PI * frekans * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * f2 * t)) * Mathf.Exp(-t / cinlamaSure);
                s[bas + i] += guc * (gurultu * 0.6f + cinlama * 0.5f);
            }
        }

        // Alçak frekanslı gövde vuruşu
        private static void Bas(float[] s, float t0, float guc, float frekans, float sure)
        {
            int bas = Mathf.Max(0, (int)(t0 * Hz));
            int uz = (int)(sure * 5f * Hz);
            for (int i = 0; i < uz && bas + i < s.Length; i++)
            {
                float t = i / (float)Hz;
                float f = frekans * (1f + 0.6f * Mathf.Exp(-t / 0.02f)); // vuruşta perde düşer
                s[bas + i] += guc * Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t / sure);
            }
        }

        private static void WavYaz(string yol, float[] ornek)
        {
            using var fs = new FileStream(yol, FileMode.Create);
            using var w = new BinaryWriter(fs);
            int veri = ornek.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + veri);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Hz); w.Write(Hz * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(veri);
            foreach (float v in ornek) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
        }
    }
}

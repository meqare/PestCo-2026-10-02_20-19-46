using UnityEngine;

namespace PestCo.Truck
{
    /// <summary>PC / terminal ekranlarını açıp kapatır (Interactable.onInteract'a bağlanır).</summary>
    public class TerminalScreen : MonoBehaviour
    {
        [SerializeField] private Renderer[] ekranlar;
        [Tooltip("Ekran açıkken görünen yazılar / arayüz objeleri.")]
        [SerializeField] private GameObject[] ekranIcerikleri;
        [SerializeField] private Light ekranIsigi;
        [SerializeField] private bool acik = true;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock blok;
        private Color[] temelEmission;

        private void Awake()
        {
            blok = new MaterialPropertyBlock();
            temelEmission = new Color[ekranlar != null ? ekranlar.Length : 0];
            for (int i = 0; i < temelEmission.Length; i++)
            {
                var mat = ekranlar[i] != null ? ekranlar[i].sharedMaterial : null;
                temelEmission[i] = mat != null && mat.HasProperty(EmissionId) ? mat.GetColor(EmissionId) : Color.white;
            }
            Uygula();
        }

        public void Toggle()
        {
            acik = !acik;
            Uygula();
        }

        private void Uygula()
        {
            if (ekranIsigi != null) ekranIsigi.enabled = acik;

            if (ekranIcerikleri != null)
                foreach (var go in ekranIcerikleri)
                    if (go != null) go.SetActive(acik);

            if (ekranlar == null) return;
            for (int i = 0; i < ekranlar.Length; i++)
            {
                if (ekranlar[i] == null) continue;
                ekranlar[i].GetPropertyBlock(blok);
                blok.SetColor(EmissionId, acik ? temelEmission[i] : Color.black);
                ekranlar[i].SetPropertyBlock(blok);
            }
        }
    }
}

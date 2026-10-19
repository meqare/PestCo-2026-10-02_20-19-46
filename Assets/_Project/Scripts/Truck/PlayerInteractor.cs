using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PestCo.Truck
{
    /// <summary>
    /// Kamera merkezinden raycast atar, baktığı Interactable'ı bulur ve E ile Interact() çağırır.
    /// Proje "Both" modunda olduğu için yeni Input System öncelikli kullanılır;
    /// yalnızca eski sistem aktifse Input.GetKeyDown'a düşer.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private Camera kamera;
        [SerializeField] private float menzil = 2.5f;

        [Tooltip("Etkileşilebilir nesnelerin bulunduğu layer(lar).")]
        [SerializeField] private LayerMask interactableMask;

        [Tooltip("Işını engelleyen katı layer(lar) (duvar, mobilya). Etkileşim nesnesinin arkasından geçişi önler.")]
        [SerializeField] private LayerMask engelMask = 1; // Default

        [Header("Arayüz (isteğe bağlı)")]
        [Tooltip("Boş bırakılırsa basit bir OnGUI yazısı çizilir.")]
        [SerializeField] private TMP_Text promptYazisi;

        public Interactable Hedef { get; private set; }

        private void Reset()
        {
            kamera = GetComponentInChildren<Camera>();
            interactableMask = LayerMask.GetMask("Interactable");
        }

        private void Awake()
        {
            if (kamera == null) kamera = Camera.main;
        }

        private void Update()
        {
            HedefiBul();
            PromptuGuncelle();

            if (Hedef != null && EtkilesimTusuBasildi())
                Hedef.Interact();
        }

        private void HedefiBul()
        {
            Hedef = null;
            if (kamera == null) return;

            Ray isin = kamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int tumMaske = interactableMask.value | engelMask.value;

            if (!Physics.Raycast(isin, out RaycastHit vurus, menzil, tumMaske, QueryTriggerInteraction.Ignore))
                return;

            // Önce vurulan şeyin etkileşim layer'ında olduğunu doğrula (duvar araya girerse yok sayılır).
            if ((interactableMask.value & (1 << vurus.collider.gameObject.layer)) == 0)
                return;

            var aday = vurus.collider.GetComponentInParent<Interactable>();
            if (aday != null && aday.CanInteract)
                Hedef = aday;
        }

        private void PromptuGuncelle()
        {
            if (promptYazisi == null) return;
            string metin = Hedef != null ? $"[E] {Hedef.Prompt}" : string.Empty;
            if (promptYazisi.text != metin) promptYazisi.text = metin;
        }

        private static bool EtkilesimTusuBasildi()
        {
#if ENABLE_INPUT_SYSTEM
            var klavye = Keyboard.current;
            return klavye != null && !PestPC.IsOpen && klavye.eKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.E);
#else
            return false;
#endif
        }

        private void OnGUI()
        {
            if (promptYazisi != null || Hedef == null) return;
            var stil = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
            GUI.Label(new Rect(0, Screen.height * 0.58f, Screen.width, 40), $"[E] {Hedef.Prompt}", stil);
        }
    }
}

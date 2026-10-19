using UnityEngine;
using UnityEngine.Events;

namespace PestCo.Truck
{
    /// <summary>
    /// Oyuncunun E tuşuyla etkileşime girebileceği nesne.
    /// Collider'ı "Interactable" layer'ında olmalıdır (PlayerInteractor maskesi).
    /// Ana oyuncunun PlayerInteraction sistemi de IInteractable üzerinden bunu kullanabilir.
    /// </summary>
    [DisallowMultipleComponent]
    public class Interactable : MonoBehaviour, IInteractable
    {
        [Header("Etkileşim")]
        [Tooltip("Oyuncu bu nesneye baktığında ekranda gösterilecek metin.")]
        [SerializeField] private string prompt = "Kullan";

        [Tooltip("İşaretliyse nesne yalnızca bir kez kullanılabilir.")]
        [SerializeField] private bool singleUse = false;

        [Header("Olaylar")]
        public UnityEvent onInteract = new UnityEvent();

        private bool used;

        /// <summary>Ekranda gösterilecek metin (kullanılamıyorsa boş).</summary>
        public string Prompt => CanInteract ? prompt : string.Empty;

        public bool CanInteract => !(singleUse && used);

        /// <summary>Oyuncu etkileşime girdiğinde PlayerInteractor tarafından çağrılır.</summary>
        public void Interact()
        {
            if (!CanInteract) return;
            used = true;
            onInteract?.Invoke();
        }

        public void SetPrompt(string yeniMetin) => prompt = yeniMetin;

        // Ana sahnedeki PlayerInteraction (IInteractable) köprüsü
        void IInteractable.Interact(PlayerInteraction oyuncu) => Interact();
        string IInteractable.GetPrompt() => CanInteract ? "[E] " + prompt : string.Empty;
    }
}

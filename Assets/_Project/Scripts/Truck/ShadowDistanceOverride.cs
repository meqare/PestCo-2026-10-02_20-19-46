using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PestCo.Truck
{
    /// <summary>
    /// Bu sahne açıkken URP Shadow Distance değerini geçici olarak değiştirir, kapanınca eski değere döndürür.
    /// Böylece global URP asset'i (ana ev sahnesi: 12 m) kalıcı olarak değişmez.
    /// </summary>
    public class ShadowDistanceOverride : MonoBehaviour
    {
        [Range(5f, 60f)] [SerializeField] private float golgeMesafesi = 25f;

        private UniversalRenderPipelineAsset urp;
        private float eskiDeger = -1f;

        private void OnEnable()
        {
            urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (urp == null) return;
            eskiDeger = urp.shadowDistance;
            urp.shadowDistance = golgeMesafesi;
        }

        private void OnDisable()
        {
            // Editor'de asset değişikliği kalıcı olacağı için mutlaka eski değere dön.
            if (urp != null && eskiDeger >= 0f) urp.shadowDistance = eskiDeger;
        }
    }
}

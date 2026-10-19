using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PestCo.Truck
{
    /// <summary>
    /// Sahneyi test etmek için minimal birinci şahıs yürüyüşü (WASD + fare).
    /// Kendi Player prefab'ınızı kullanacaksanız bu bileşeni kaldırabilirsiniz.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class TruckTestWalker : MonoBehaviour
    {
        [SerializeField] private Transform kameraPivot;
        [SerializeField] private float yurumeHizi = 2.2f;
        [SerializeField] private float fareHassasiyeti = 0.08f;

        private CharacterController kontrol;
        private float egim;
        private float dusey;

        private void Awake()
        {
            kontrol = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

#if ENABLE_INPUT_SYSTEM
        private void Update()
        {
            var klavye = Keyboard.current;
            var fare = Mouse.current;
            if (klavye == null || fare == null) return;

            if (klavye.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            Vector2 bakis = fare.delta.ReadValue() * fareHassasiyeti;
            transform.Rotate(0f, bakis.x, 0f);
            egim = Mathf.Clamp(egim - bakis.y, -80f, 80f);
            if (kameraPivot != null) kameraPivot.localRotation = Quaternion.Euler(egim, 0f, 0f);

            float x = (klavye.dKey.isPressed ? 1f : 0f) - (klavye.aKey.isPressed ? 1f : 0f);
            float z = (klavye.wKey.isPressed ? 1f : 0f) - (klavye.sKey.isPressed ? 1f : 0f);
            Vector3 hareket = (transform.right * x + transform.forward * z).normalized * yurumeHizi;

            dusey = kontrol.isGrounded ? -1f : dusey + Physics.gravity.y * Time.deltaTime;
            hareket.y = dusey;
            kontrol.Move(hareket * Time.deltaTime);
        }
#else
        private void Update() { }
#endif
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerWeapon : MonoBehaviour
{
    [Header("Ayarlar")]
    public float weaponRange = 5f;
    public Transform firePoint;

    [Header("Bağlantılar")]
    public UIManager uiManager;

    private void Start()
    {
        if (uiManager == null)
        {
            uiManager = UIManager.Instance;
        }

        if (firePoint == null && Camera.main != null)
        {
            firePoint = Camera.main.transform;
        }
    }

    private void Update()
    {
        if (firePoint == null) return;

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            FireWeapon();
        }
    }

    private void FireWeapon()
    {
        Vector3 origin = firePoint.position;
        Vector3 direction = firePoint.forward;

        Debug.DrawRay(origin, direction * weaponRange, new Color(0.2f, 1f, 0.2f));

        if (Physics.Raycast(origin, direction, out RaycastHit hit, weaponRange))
        {
            BugController bug = hit.collider.GetComponentInParent<BugController>();
            if (bug != null)
            {
                Debug.Log("Böcek etkileniyor!");

                // Önce Cute durumu kaydedilir; sadece ilk temasta (her karede tekrar eklenmesin).
                if (uiManager != null && bug.currentState == BugState.Cute)
                {
                    uiManager.AddBugToVakum(bug.bugData, bug.currentState);
                }

                bug.TriggerHorrorMode();
            }
        }
    }
}

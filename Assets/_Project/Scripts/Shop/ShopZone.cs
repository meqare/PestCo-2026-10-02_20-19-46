using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class ShopZone : MonoBehaviour
{
    [Header("Bağlantılar")]
    public UIManager uiManager;

    [Header("Ekonomi")]
    public float money = 0f;

    private bool playerInside;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) playerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) playerInside = false;
    }

    private void Update()
    {
        if (!playerInside || Keyboard.current == null) return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            SellAll();
        }
    }

    private void SellAll()
    {
        if (uiManager == null) return;

        float earned = 0f;
        foreach (UIManager.CapturedBug bug in uiManager.TakeAllBugs())
        {
            earned += CalculatePrice(bug);
        }

        money += earned;
        Debug.Log($"Satış: +{earned} | Toplam para: {money}");
    }

    private float CalculatePrice(UIManager.CapturedBug bug)
    {
        float price = bug.data.baseValue;

        if (bug.state == BugState.Horror)
        {
            price *= bug.data.dangerMultiplier;
        }

        return price;
    }
}

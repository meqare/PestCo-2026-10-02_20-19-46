using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Sprey tabancasının çizgi film modellerini (SprayGunModel altındaki "Sprayer_*" nesneleri) menüden değiştirir.
// Seçilen modeli açar, diğerlerini kapatır ve sprey partiküllerini o modelin "Muzzle" noktasına taşır.
public static class SpraySkinSwitcher
{
    private const string Menu = "PEST CO/Sprey Modeli/";

    [MenuItem(Menu + "1 - Neon Canavar")] private static void Canavar() => Apply("Sprayer_Canavar");
    [MenuItem(Menu + "2 - Lastik Ordek")] private static void Ordek() => Apply("Sprayer_Ordek");
    [MenuItem(Menu + "3 - Muz")] private static void Muz() => Apply("Sprayer_Muz");
    [MenuItem(Menu + "4 - Acme Huni Topu")] private static void Huni() => Apply("Sprayer_Huni");
    [MenuItem(Menu + "5 - Caydanlik")] private static void Caydanlik() => Apply("Sprayer_Caydanlik");

    public static bool Apply(string skinName)
    {
        var anim = Object.FindFirstObjectByType<SprayGunAnimator>(FindObjectsInactive.Include);
        if (anim == null) { Debug.LogWarning("PEST CO: SprayGunAnimator sahnede bulunamadi."); return false; }

        Transform model = anim.transform;
        Transform tool = model.parent;
        Transform chosen = null;
        foreach (Transform child in model)
        {
            if (!child.name.StartsWith("Sprayer_")) continue;
            bool on = child.name == skinName;
            child.gameObject.SetActive(on);
            if (on) chosen = child;
        }
        if (chosen == null) { Debug.LogWarning("PEST CO: Model bulunamadi: " + skinName); return false; }

        Transform muzzle = chosen.Find("Muzzle");
        if (muzzle != null && tool != null)
        {
            Vector3 local = tool.InverseTransformPoint(muzzle.position);
            foreach (var ps in tool.GetComponentsInChildren<ParticleSystem>(true))
                if (ps.transform.parent == tool) ps.transform.localPosition = local;
        }
        EditorSceneManager.MarkSceneDirty(model.gameObject.scene);
        return true;
    }
}

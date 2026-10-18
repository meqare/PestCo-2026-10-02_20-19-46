using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Insan oyuncu <-> hamam böceği (PestTestRig) arasında tek tıkla geçiş.
// Menü: PestCo/Hamam Boecegi Modu  (işaretliyse hamam böceği açık, Player kapalı)
public static class PestModeToggle
{
    private const string MenuPath = "PestCo/Hamam Boecegi Modu";

    private static GameObject FindInScene(string name)
    {
        return Resources.FindObjectsOfTypeAll<Transform>()
            .Select(t => t.gameObject)
            .FirstOrDefault(g => g.name == name && g.scene.IsValid() && g.transform.parent == null);
    }

    [MenuItem(MenuPath)]
    public static void Toggle()
    {
        var rig = FindInScene("PestTestRig");
        var player = FindInScene("Player");
        if (rig == null || player == null) { Debug.LogError("PestTestRig veya Player sahnede yok."); return; }

        bool pestOn = !rig.activeSelf;
        Undo.RecordObject(rig, "Hamam Boecegi Modu");
        Undo.RecordObject(player, "Hamam Boecegi Modu");
        rig.SetActive(pestOn);
        player.SetActive(!pestOn);
        EditorSceneManager.MarkSceneDirty(rig.scene);
    }

    [MenuItem(MenuPath, true)]
    private static bool Validate()
    {
        var rig = FindInScene("PestTestRig");
        Menu.SetChecked(MenuPath, rig != null && rig.activeSelf);
        return rig != null;
    }
}

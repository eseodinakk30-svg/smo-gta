#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using SanMonica.Data;

namespace SanMonica.EditorTools
{
    /// <summary>
    /// Writes the code-defined catalogues out as ScriptableObject assets so they
    /// can be edited in the inspector and version controlled as data. The game
    /// runs from the code catalogues by default; exported assets are for tuning.
    /// </summary>
    public static class DataAssetExporter
    {
        [MenuItem("San Monica/Export Data Assets", priority = 40)]
        public static void Export()
        {
            EnsureFolder("Assets/Data");
            EnsureFolder("Assets/Data/Vehicles");
            EnsureFolder("Assets/Data/Weapons");
            EnsureFolder("Assets/Data/Peds");

            int count = 0;
            foreach (var vehicle in VehicleCatalogData.All)
                count += Save(vehicle, "Assets/Data/Vehicles/" + vehicle.id + ".asset");
            foreach (var weapon in WeaponCatalogData.All)
                count += Save(weapon, "Assets/Data/Weapons/" + weapon.id + ".asset");
            foreach (var ped in PedCatalogData.All)
                count += Save(ped, "Assets/Data/Peds/" + ped.id + ".asset");

            var config = WorldConfig.CreateDefault();
            count += Save(config, "Assets/Data/WorldConfig.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[San Monica] Exported " + count + " data assets to Assets/Data.");
        }

        private static int Save(ScriptableObject source, string path)
        {
            if (source == null) return 0;
            var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(source, existing);
                EditorUtility.SetDirty(existing);
                return 1;
            }
            var clone = Object.Instantiate(source);
            AssetDatabase.CreateAsset(clone, path);
            return 1;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif

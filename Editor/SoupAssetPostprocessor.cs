using Soup.Editor.Core;
using UnityEditor;

namespace Soup.Editor
{
    internal sealed class SoupAssetPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (deletedAssets.Length > 0)
            {
                SoupEditorCore.UnloadDeletedGraphs();
            }

            if (importedAssets.Length > 0)
            {
                SoupEditorCore.ReloadImportedGraphs(importedAssets);
            }
        }
    }
}

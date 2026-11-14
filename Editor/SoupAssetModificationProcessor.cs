using System;
using UnityEditor;

namespace Soup.Editor
{
    internal sealed class SoupAssetModificationProcessor : AssetModificationProcessor
    {
        /** Raised after Unity saves assets, once their dirty flags are cleared. */
        internal static event Action OnAssetsSaved;

        private static string[] OnWillSaveAssets(string[] paths)
        {
            EditorApplication.delayCall += () => OnAssetsSaved?.Invoke();
            return paths;
        }
    }
}

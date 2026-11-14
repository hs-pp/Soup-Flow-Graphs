using System;
using System.IO;
using System.Text;
using Soup.Editor.Views.Inspector;
using Soup.Editor.Views.Nodes;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Soup.Editor.ScriptTemplates
{
    /// <summary>
    /// Creates Soup scripts from the templates in <see cref="SoupScriptTemplates"/>. Node scripts come from the Project
    /// window's Create menu. View and inspector scripts come from a node's context menu in the graph, which knows the node
    /// type they register for. Each checks the assembly its file would compile into before writing it.
    /// </summary>
    public static class SoupScriptCreator
    {
        private const string SOUP_RUNTIME_ASSEMBLY = "Soup";
        private const string SOUP_EDITOR_ASSEMBLY = "Soup.Editor";
        private const string LAST_FOLDER_SETTING = "Soup.ScriptTemplates.LastFolder.";
        private const string SCRIPTS_MENU = "Scripts/";

        #region Node Scripts
        [MenuItem("Assets/Create/Soup/Node Script", false, 1)]
        private static void CreateNodeScriptMenuItem()
        {
            Texture2D icon = EditorGUIUtility.IconContent("cs Script Icon").image as Texture2D;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(EntityId.None, ScriptableObject.CreateInstance<CreateNodeScriptAction>(), "NewNode.cs", icon, null);
        }

        internal static void CreateNodeScriptFromProjectWindow(string pathName)
        {
            if (!TryCreateNodeScript(pathName, out string scriptPath, out string error))
            {
                Debug.LogError($"[Soup] {error}");
                return;
            }

            ProjectWindowUtil.ShowCreatedAsset(AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath));
        }

        /// <summary>
        /// Spaces are dropped from the file name, which is also the class name. A node may live in an Editor-only
        /// assembly, with a warning, as players can't load the graphs that use it.
        /// </summary>
        public static bool TryCreateNodeScript(string pathName, out string scriptPath, out string error)
        {
            if (!TryPrepareScriptPath(pathName, out scriptPath, out error))
            {
                return false;
            }

            // An assembly with no scripts yet can't be checked. The compiler will say if the node can't see Soup.
            ScriptAssembly target = ScriptAssembly.Find(scriptPath);
            if (target.Compiled != null && !target.References(SOUP_RUNTIME_ASSEMBLY))
            {
                error = $"'{target.Name}' doesn't reference {SOUP_RUNTIME_ASSEMBLY}, so {Path.GetFileName(scriptPath)} wouldn't compile. Add {SOUP_RUNTIME_ASSEMBLY} to its assembly definition's references, or create the node in another folder.";
                return false;
            }

            if (!SoupScriptTemplates.TryRender(SoupScriptKind.Node, scriptPath, null, out string content, out error))
            {
                return false;
            }

            WriteScript(scriptPath, content);
            if (target.IsEditorOnly == true)
            {
                Debug.LogWarning($"[Soup] {Path.GetFileName(scriptPath)} is in the Editor-only assembly '{target.Name}'. Players can't load graphs that use it.");
            }
            return true;
        }
        #endregion

        #region Node View and Inspector Scripts
        /// <summary>Adds the Scripts submenu to a node's context menu.</summary>
        internal static void AppendNodeMenuItems(DropdownMenu menu, Type nodeType)
        {
            if (nodeType == null)
            {
                return;
            }

            menu.AppendAction(SCRIPTS_MENU + "Open Node Script", _ => OpenScript(nodeType));
            menu.AppendSeparator(SCRIPTS_MENU);
            AppendEditorScriptItems(menu, nodeType, SoupScriptKind.NodeView, EditorTypesFactory.HasOwnNodeView(nodeType),
                EditorTypesFactory.GetNodeViewType(nodeType), typeof(DefaultNodeView));
            AppendEditorScriptItems(menu, nodeType, SoupScriptKind.NodeInspector, EditorTypesFactory.HasOwnNodeInspector(nodeType),
                EditorTypesFactory.GetNodeInspectorType(nodeType), typeof(DefaultNodeInspector));
        }

        private static void AppendEditorScriptItems(DropdownMenu menu, Type nodeType, SoupScriptKind kind, bool hasOwn, Type currentType, Type defaultType)
        {
            string label = GetLabel(kind);
            if (hasOwn)
            {
                menu.AppendAction($"{SCRIPTS_MENU}Open {label} Script", _ => OpenScript(currentType));
                return;
            }

            string text = $"{SCRIPTS_MENU}Create {label} Script";
            string reason = SoupScriptTemplates.GetUnsupportedNodeTypeReason(nodeType);
            if (reason != null)
            {
                menu.AppendAction($"{text} ({reason})", _ => { }, DropdownMenuAction.Status.Disabled);
                return;
            }

            if (currentType != defaultType)
            {
                text += $" (currently {currentType.Name})";
            }
            menu.AppendAction(text + "...", _ => CreateEditorScriptWithDialog(kind, nodeType));
        }

        private static void CreateEditorScriptWithDialog(SoupScriptKind kind, Type nodeType)
        {
            string label = GetLabel(kind);
            string path = EditorUtility.SaveFilePanelInProject($"Create Node {label} Script", nodeType.Name + label, "cs",
                $"Pick a folder that compiles into an Editor-only assembly for the {nodeType.Name} {label.ToLowerInvariant()}.", GetDefaultFolder(kind, nodeType));
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (!TryCreateEditorScript(kind, nodeType, path, out string scriptPath, out string error))
            {
                EditorUtility.DisplayDialog($"Can't Create {label} Script", error, "OK");
                return;
            }

            EditorUserSettings.SetConfigValue(LAST_FOLDER_SETTING + kind, GetFolder(scriptPath));
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            ProjectWindowUtil.ShowCreatedAsset(script);
            AssetDatabase.OpenAsset(script);
        }

        /// <summary>
        /// Writes a view or inspector script for the node type. Fails when the file's assembly is part of player builds,
        /// or can't see Soup.Editor or the node's own assembly. Spaces are dropped from the file name, which is also the
        /// class name.
        /// </summary>
        public static bool TryCreateEditorScript(SoupScriptKind kind, Type nodeType, string pathName, out string scriptPath, out string error)
        {
            if (kind == SoupScriptKind.Node)
            {
                throw new ArgumentException("Node scripts are created with TryCreateNodeScript.", nameof(kind));
            }
            if (!TryPrepareScriptPath(pathName, out scriptPath, out error))
            {
                return false;
            }

            error = FindEditorScriptAssemblyProblem(kind, nodeType, scriptPath);
            if (error != null)
            {
                return false;
            }

            if (!SoupScriptTemplates.TryRender(kind, scriptPath, nodeType, out string content, out error))
            {
                return false;
            }

            WriteScript(scriptPath, content);
            return true;
        }

        private static string FindEditorScriptAssemblyProblem(SoupScriptKind kind, Type nodeType, string scriptPath)
        {
            ScriptAssembly target = ScriptAssembly.Find(scriptPath);
            string fileName = Path.GetFileName(scriptPath);
            if (target.IsEditorOnly == false)
            {
                return $"{fileName} would compile into '{target.Name}', which is part of player builds, so its UnityEditor references would break the build. Put it in a folder named Editor, or under an assembly definition that only includes the Editor platform.";
            }
            if (target.Compiled == null)
            {
                return null; // Not compiled yet, so there's nothing to check its references against.
            }

            if (!target.References(SOUP_EDITOR_ASSEMBLY))
            {
                return $"'{target.Name}' doesn't reference {SOUP_EDITOR_ASSEMBLY}, so {fileName} wouldn't compile. Add {SOUP_EDITOR_ASSEMBLY} to its assembly definition's references, or pick another folder.";
            }

            string nodeAssembly = nodeType.Assembly.GetName().Name;
            if (!target.References(nodeAssembly))
            {
                return $"'{target.Name}' doesn't reference '{nodeAssembly}', where {nodeType.Name} is defined, so the {GetLabel(kind).ToLowerInvariant()} couldn't name it. Add '{nodeAssembly}' to its assembly definition's references, or pick another folder.";
            }

            return null;
        }

        // The last folder used for this kind, else an Editor folder next to the node's script, else the node's own folder.
        private static string GetDefaultFolder(SoupScriptKind kind, Type nodeType)
        {
            string lastFolder = EditorUserSettings.GetConfigValue(LAST_FOLDER_SETTING + kind);
            if (!string.IsNullOrEmpty(lastFolder) && AssetDatabase.IsValidFolder(lastFolder))
            {
                return lastFolder;
            }

            MonoScript nodeScript = FindScript(nodeType);
            if (nodeScript == null)
            {
                return "Assets";
            }

            string nodeFolder = GetFolder(AssetDatabase.GetAssetPath(nodeScript));
            string editorFolder = nodeFolder + "/Editor";
            return AssetDatabase.IsValidFolder(editorFolder) ? editorFolder : nodeFolder;
        }

        private static string GetLabel(SoupScriptKind kind)
        {
            return kind == SoupScriptKind.NodeView ? "View" : "Inspector";
        }
        #endregion

        #region Shared
        private static bool TryPrepareScriptPath(string pathName, out string scriptPath, out string error)
        {
            string className = Path.GetFileNameWithoutExtension(pathName).Replace(" ", string.Empty);
            scriptPath = $"{GetFolder(pathName)}/{className}.cs";
            if (!SoupScriptTemplates.IsValidClassName(className))
            {
                error = $"'{className}' isn't a valid C# class name. Use letters, digits and underscores, not starting with a digit.";
                return false;
            }
            if (File.Exists(scriptPath))
            {
                error = $"{scriptPath} already exists.";
                return false;
            }

            error = null;
            return true;
        }

        private static void WriteScript(string scriptPath, string content)
        {
            File.WriteAllText(scriptPath, content, new UTF8Encoding(false));
            AssetDatabase.ImportAsset(scriptPath);
        }

        private static string GetFolder(string assetPath)
        {
            return Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
        }

        private static void OpenScript(Type type)
        {
            MonoScript script = FindScript(type);
            if (script == null)
            {
                Debug.LogWarning($"[Soup] Couldn't find the script that defines '{type.Name}'. Unity only finds a class in a file of the same name.");
                return;
            }

            AssetDatabase.OpenAsset(script);
        }

        // MonoScript.GetClass only knows the top-level class named after the file, so nested types go through their outermost one.
        private static MonoScript FindScript(Type type)
        {
            Type outermost = type;
            while (outermost.DeclaringType != null)
            {
                outermost = outermost.DeclaringType;
            }

            foreach (string guid in AssetDatabase.FindAssets($"t:MonoScript {outermost.Name}"))
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == outermost)
                {
                    return script;
                }
            }

            return null;
        }

        /// <summary>
        /// The assembly a script path compiles into. Compiled is null when that assembly has no scripts yet, so nothing is
        /// known about its references. IsEditorOnly is null when that is unknown too.
        /// </summary>
        private readonly struct ScriptAssembly
        {
            private static readonly string[] s_predefinedEditorAssemblies = { "Assembly-CSharp-Editor", "Assembly-CSharp-Editor-firstpass" };
            private static readonly string[] s_predefinedPlayerAssemblies = { "Assembly-CSharp", "Assembly-CSharp-firstpass" };

            public readonly string Name;
            public readonly Assembly Compiled;
            public readonly bool? IsEditorOnly;

            private ScriptAssembly(string name, Assembly compiled, bool? isEditorOnly)
            {
                Name = name;
                Compiled = compiled;
                IsEditorOnly = isEditorOnly;
            }

            public static ScriptAssembly Find(string scriptPath)
            {
                string name = CompilationPipeline.GetAssemblyNameFromScriptPath(scriptPath) ?? string.Empty;
                if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - ".dll".Length);
                }

                foreach (Assembly assembly in CompilationPipeline.GetAssemblies(AssembliesType.Editor))
                {
                    if (assembly.name == name)
                    {
                        return new ScriptAssembly(name, assembly, (assembly.flags & AssemblyFlags.EditorAssembly) != 0);
                    }
                }

                bool? isEditorOnly = null;
                if (Array.IndexOf(s_predefinedEditorAssemblies, name) >= 0)
                {
                    isEditorOnly = true;
                }
                else if (Array.IndexOf(s_predefinedPlayerAssemblies, name) >= 0)
                {
                    isEditorOnly = false;
                }

                return new ScriptAssembly(name, null, isEditorOnly);
            }

            public bool References(string assemblyName)
            {
                if (Compiled == null)
                {
                    return false;
                }
                if (Compiled.name == assemblyName)
                {
                    return true;
                }

                foreach (Assembly reference in Compiled.assemblyReferences)
                {
                    if (reference.name == assemblyName)
                    {
                        return true;
                    }
                }
                // Precompiled DLLs only show up here, as file paths.
                foreach (string referencePath in Compiled.compiledAssemblyReferences)
                {
                    if (Path.GetFileNameWithoutExtension(referencePath) == assemblyName)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
        #endregion
    }
}

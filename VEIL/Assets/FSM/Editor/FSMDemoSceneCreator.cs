using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AulaFSM.EditorTools
{
    /// <summary>
    /// Cria automaticamente a cena "Assets/Scenes/FSM_Demo.unity" (se não existir)
    /// e coloca-a em primeiro lugar nas Build Settings.
    /// Menu: FSM > Criar/Abrir Cena Demo
    /// </summary>
    [InitializeOnLoad]
    public static class FSMDemoSceneCreator
    {
        private const string ScenePath = "Assets/Scenes/FSM_Demo.unity";

        static FSMDemoSceneCreator()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                EnsureMaterials();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                    CreateScene(openAfter: false);
            };
        }

        /// <summary>
        /// Guarda os materiais base (opaco e transparente) numa pasta Resources para serem incluídos na Build.
        /// Sem isto, numa Build os objetos criados por código ficavam cor-de-rosa (shader removido).
        /// </summary>
        public static void EnsureMaterials()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            System.IO.Directory.CreateDirectory("Assets/FSM/Resources");

            string opaque = "Assets/FSM/Resources/" + Visuals.Gfx.OpaqueResource + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(opaque) == null)
                AssetDatabase.CreateAsset(new Material(shader), opaque);

            string transparent = "Assets/FSM/Resources/" + Visuals.Gfx.TransparentResource + ".mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(transparent) == null)
            {
                var mat = new Material(shader);
                Visuals.Gfx.ConfigureTransparent(mat);
                AssetDatabase.CreateAsset(mat, transparent);
            }
            // Tipos de guarda editáveis no Inspector
            string guardTypes = "Assets/FSM/Resources/" + Enemy.GuardTypeSettings.ResourceName + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Enemy.GuardTypeSettings>(guardTypes) == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<Enemy.GuardTypeSettings>(), guardTypes);

            string glow = "Assets/FSM/Resources/" + Visuals.Gfx.GlowResource + ".mat";
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit != null && AssetDatabase.LoadAssetAtPath<Material>(glow) == null)
            {
                var mat = new Material(unlit);
                Visuals.Gfx.ConfigureTransparent(mat);
                AssetDatabase.CreateAsset(mat, glow);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("FSM/Criar ou Abrir Cena Demo", priority = 0)]
        public static void OpenOrCreate()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                CreateScene(openAfter: false);

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void CreateScene(bool openAfter)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Additive);
            var bootstrap = new GameObject("FSM Demo (constrói o nível)");
            bootstrap.AddComponent<DemoBootstrap>();
            SceneManager.MoveGameObjectToScene(bootstrap, scene);

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true);
            AddToBuildSettingsFirst();
            Debug.Log($"[FSM] Cena criada em {ScenePath} e adicionada às Build Settings. Abre-a e carrega em Play!");

            if (openAfter) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        private static void AddToBuildSettingsFirst()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}

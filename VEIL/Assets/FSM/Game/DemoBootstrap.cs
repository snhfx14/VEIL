using System.Collections.Generic;
using AulaFSM.Enemy;
using AulaFSM.Player;
using AulaFSM.Visuals;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace AulaFSM
{
    /// <summary>
    /// Constrói o nível atual a partir do mapa em texto (ver Levels.cs):
    /// chão, caixas, muros, NavMesh, jogador, guardas, diamantes, câmara, luz, HUD e menus.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class DemoBootstrap : MonoBehaviour
    {
        private const float Cell = 2f;           // cada carácter do mapa = 2x2 metros
        private const float CrateHeight = 1.6f;
        private const float LowCrateHeight = 0.75f; // abaixo da linha dos olhos dos guardas
        private const string FloorName = "Floor";

        // Jogador (CharacterController) e NavMesh — os valores têm de ser coerentes entre si
        private const float PlayerHeight = 2f;
        private const float PlayerRadius = 0.45f;
        private const float PlayerSkinWidth = 0.08f;
        private const float PlayerStepOffset = 0.3f;
        private const float NavAgentRadius = PlayerRadius + PlayerSkinWidth + 0.07f; // 0,6 m: o caminho nunca roça as caixas
        private const float NavStepHeight = 0.25f;  // < stepOffset (0,3) e < caixas baixas (0,75): nunca sobe a caixas

        // ------------------------------------------------------------------ Arranque automático
        // Garante que o jogo arranca em QUALQUER cena (ex.: SampleScene), mesmo sem o objeto na cena.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterAutoStart()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            if (FindFirstObjectByType<DemoBootstrap>() != null) return;
            new GameObject("FSM Demo (auto)").AddComponent<DemoBootstrap>();
        }

        // ------------------------------------------------------------------

        private int _cols, _rows;
        private Vector3 _playerSpawn;
        private readonly List<(Vector3 pos, GuardType type)> _guardSpawns = new List<(Vector3, GuardType)>();
        private readonly List<Vector3> _gemSpawns = new List<Vector3>();

        private void Awake()
        {
            LevelManager.EnsureInit();
            var data = LevelManager.Current;
            Random.InitState(2011 + LevelManager.CurrentLevel * 97);
            GuardSquad.Reset(); // alerta da zona e cadáveres começam do zero em cada nível

            var level = BuildLevel(data);
            BakeNavMesh(level);

            var player = SpawnPlayer(_playerSpawn);
            foreach (var g in _gemSpawns)
            {
                var gem = CharacterFactory.BuildGem(transform, g + Vector3.up);
                gem.AddComponent<Crystal>();
                AddPointLight(gem.transform, Vector3.zero, new Color(0.3f, 1f, 1f), 3f, 2.5f); // brilho do diamante
            }
            for (int i = 0; i < _guardSpawns.Count; i++)
                SpawnEnemy(player, _guardSpawns[i].pos, _guardSpawns[i].type, MakePatrolRoute(data, i, _guardSpawns[i].pos), data);

            new GameObject("GameManager").AddComponent<GameManager>();
            new GameObject("DebugHUD").AddComponent<DebugHUD>();
            new GameObject("MenuUI").AddComponent<MenuUI>();
            new GameObject("PathVisual").AddComponent<PathVisual>().player = player;
            SetupCameraAndLight(player.transform);
        }

        // ------------------------------------------------------------------ Nível a partir do mapa

        private Vector3 CellToWorld(int col, int row)
        {
            float x = (col - (_cols - 1) * 0.5f) * Cell;
            float z = ((_rows - 1) * 0.5f - row) * Cell;
            return new Vector3(x, 0f, z);
        }

        private GameObject BuildLevel(LevelData data)
        {
            var level = new GameObject("Level");
            _rows = data.Map.Length;
            _cols = 0;
            foreach (var row in data.Map) _cols = Mathf.Max(_cols, row.Length);

            float sizeX = _cols * Cell, sizeZ = _rows * Cell;

            // Chão de azulejos dividido em blocos de 4x4 m
            // (cada bloco só é iluminado pelas lanternas próximas → funciona com muitas luzes)
            const float chunk = 4f;
            int cx = Mathf.CeilToInt((sizeX + 3f) / chunk), cz = Mathf.CeilToInt((sizeZ + 3f) / chunk);
            var floorMat = Gfx.Make(Color.white, ProceduralTextures.Floor, new Vector2(chunk / 2f, chunk / 2f));
            for (int ix = 0; ix < cx; ix++)
            for (int iz = 0; iz < cz; iz++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "Floor";
                tile.transform.SetParent(level.transform);
                tile.transform.position = new Vector3((ix - (cx - 1) * 0.5f) * chunk, -0.5f, (iz - (cz - 1) * 0.5f) * chunk);
                tile.transform.localScale = new Vector3(chunk, 1f, chunk);
                tile.GetComponent<Renderer>().sharedMaterial = floorMat;
                tile.isStatic = true;
            }

            // Muros exteriores de tijolo
            float hx = sizeX / 2f + 0.75f, hz = sizeZ / 2f + 0.75f;
            BrickWall(level.transform, 0f, hz, sizeX + 3f, 1.5f);
            BrickWall(level.transform, 0f, -hz, sizeX + 3f, 1.5f);
            BrickWall(level.transform, hx, 0f, 1.5f, sizeZ);
            BrickWall(level.transform, -hx, 0f, 1.5f, sizeZ);

            // Ler o mapa carácter a carácter
            _playerSpawn = Vector3.zero;
            _guardSpawns.Clear();
            _gemSpawns.Clear();
            for (int r = 0; r < _rows; r++)
            {
                string line = data.Map[r];
                for (int c = 0; c < _cols; c++)
                {
                    char ch = c < line.Length ? line[c] : '.';
                    Vector3 p = CellToWorld(c, r);
                    switch (ch)
                    {
                        case '#':
                            float h = Random.value < 0.25f ? CrateHeight * 1.25f : CrateHeight;
                            Crate(level.transform, p, new Vector3(Cell, h, Cell), 0f);
                            break;
                        case 'B':
                            Crate(level.transform, p, new Vector3(Cell, 2f, Cell), 0f);
                            Crate(level.transform, p + new Vector3(0.1f, 2f, -0.1f), new Vector3(1.2f, 1.2f, 1.2f), Random.Range(-20f, 20f), collider: false);
                            break;
                        case 'o':
                            // Caixas baixas: bloqueiam a passagem mas NÃO a visão (zona de pouca cobertura)
                            Crate(level.transform, p, new Vector3(Cell * 0.9f, LowCrateHeight, Cell * 0.9f), Random.Range(-6f, 6f));
                            break;
                        case 'P': _playerSpawn = p; break;
                        case 'G': _gemSpawns.Add(p); break;
                        default:
                            if (LevelData.TryGetGuardType(ch, out var type)) _guardSpawns.Add((p, type));
                            break;
                    }
                }
            }
            return level;
        }

        private void BrickWall(Transform parent, float x, float z, float sx, float sz)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Wall";
            go.transform.SetParent(parent);
            go.transform.position = new Vector3(x, 1.25f, z);
            go.transform.localScale = new Vector3(sx, 2.5f, sz);
            float length = Mathf.Max(sx, sz);
            go.GetComponent<Renderer>().material = Gfx.Make(Color.white, ProceduralTextures.Wall, new Vector2(length / 2.5f, 1f));
            go.isStatic = true;
        }

        private void Crate(Transform parent, Vector3 basePos, Vector3 size, float yaw, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Crate";
            go.transform.SetParent(parent);
            go.transform.position = basePos + Vector3.up * (size.y * 0.5f);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = size;
            var tex = Random.value < 0.5f ? ProceduralTextures.Crate : ProceduralTextures.CrateDark;
            go.GetComponent<Renderer>().material = Gfx.Make(Color.white, tex);
            if (!collider) DestroyImmediate(go.GetComponent<Collider>());
            Gfx.AddOutline(go, PrimitiveType.Cube, 0.03f);
            go.isStatic = true;
        }

        /// <summary>
        /// Constrói o NavMesh SINCRONIZADO com a física:
        ///  - só o chão é caminhável; caixas e muros são obstáculos "Not Walkable"
        ///    (antes o NavMesh criava zonas caminháveis em cima das caixas);
        ///  - altura de degrau (agentClimb) menor do que o stepOffset do jogador e do que as caixas baixas
        ///    (antes era 0,75 m = altura das caixas baixas → o caminho passava POR CIMA delas:
        ///     os guardas atravessavam-nas e o jogador ficava preso a tentar subir);
        ///  - raio maior do que a cápsula do jogador + skin width (antes 0,5 m < 0,53 m → prendia nos cantos).
        /// </summary>
        private void BakeNavMesh(GameObject level)
        {
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(level.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0,
                new List<NavMeshBuildMarkup>(), sources);

            int notWalkable = NavMesh.GetAreaFromName("Not Walkable");
            if (notWalkable < 0) notWalkable = 1;
            for (int i = 0; i < sources.Count; i++)
            {
                var s = sources[i];
                if (s.component != null && s.component.gameObject.name != FloorName)
                {
                    s.area = notWalkable;
                    sources[i] = s;
                }
            }

            var settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = NavAgentRadius;
            settings.agentHeight = PlayerHeight;
            settings.agentClimb = NavStepHeight;

            var bounds = new Bounds(Vector3.zero, new Vector3(_cols * Cell + 8f, 10f, _rows * Cell + 8f));
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            level.AddComponent<RuntimeNavMesh>().Init(NavMesh.AddNavMeshData(data), data);
        }

        // ------------------------------------------------------------------ Jogador

        private PlayerController SpawnPlayer(Vector3 position)
        {
            var root = new GameObject("Player");
            root.SetActive(false); // configurar tudo antes do Awake
            root.transform.position = position;

            // Valores explícitos e coerentes com o NavMesh (ver BakeNavMesh)
            var cc = root.AddComponent<CharacterController>();
            cc.center = new Vector3(0f, PlayerHeight * 0.5f, 0f);
            cc.height = PlayerHeight;
            cc.radius = PlayerRadius;
            cc.skinWidth = PlayerSkinWidth;
            cc.stepOffset = PlayerStepOffset;
            cc.slopeLimit = 40f;
            cc.minMoveDistance = 0f;

            var rig = CharacterFactory.BuildPlayer(root.transform);

            var health = root.AddComponent<Health>();
            health.maxHealth = 100f;

            var player = root.AddComponent<PlayerController>();
            player.model = rig.Model;
            player.swordPivot = rig.WeaponPivot;
            player.bodyRenderer = rig.Torso;

            // Pequena luz à volta do jogador (para ele se ver no escuro)
            AddPointLight(root.transform, new Vector3(0f, 3f, 0f), new Color(1f, 0.9f, 0.75f), 6.5f, 1.6f);

            root.SetActive(true);
            return player;
        }

        private static Light AddPointLight(Transform parent, Vector3 localPos, Color color, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            return light;
        }

        // ------------------------------------------------------------------ Guardas

        private void SpawnEnemy(PlayerController player, Vector3 spawn, GuardType type, Vector3[] route, LevelData data)
        {
            var profile = GuardTypeSettings.ProfileFor(type);

            var root = new GameObject("Guarda");
            root.SetActive(false);
            root.transform.position = spawn;
            // Começa virado para o próximo ponto do percurso (ou numa direção aleatória se for posto fixo)
            Vector3 look = route.Length > 1 ? route[1] - spawn : Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f) * Vector3.forward;
            look.y = 0f;
            root.transform.rotation = Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward);

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.height = 2f;
            col.radius = 0.45f;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.45f;
            agent.height = 2f;
            agent.angularSpeed = 540f;
            agent.acceleration = 30f;
            agent.stoppingDistance = 0.3f;

            var rig = CharacterFactory.BuildGuard(root.transform, type, profile);

            var health = root.AddComponent<Health>();
            health.maxHealth = profile.maxHealth;

            // Valores base do nível × atributos do tipo de guarda (o sistema de disparo fica intacto)
            var ai = root.AddComponent<EnemyAI>();
            ai.type = type;
            ai.profile = profile;
            ai.patrolPoints = route;
            ai.target = player;
            ai.bodyRenderer = rig.Torso;
            ai.aimLaser = rig.AimLaser;
            ai.viewDistance = data.GuardViewDistance * profile.viewDistanceMultiplier;
            ai.viewAngle = profile.viewAngle;
            ai.hearingRadius = profile.hearingRadius;
            ai.chaseSpeed = data.GuardChaseSpeed;
            ai.timeToConfirmTarget = data.GuardConfirmTime;

            // LANTERNA: luz spot verdadeira, na ponta da arma, com o mesmo ângulo e alcance da visão do guarda.
            // As caixas fazem sombra → o jogador pode esconder-se na sombra.
            var lampGo = new GameObject("Flashlight");
            lampGo.transform.SetParent(root.transform, false);
            lampGo.transform.localPosition = new Vector3(0.15f, 1.25f, 1.15f);
            lampGo.transform.localRotation = Quaternion.Euler(Mathf.Atan2(1.25f, ai.viewDistance * 0.75f) * Mathf.Rad2Deg, 0f, 0f);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.spotAngle = ai.viewAngle + 6f;
            lamp.innerSpotAngle = ai.viewAngle * 0.7f;
            lamp.range = ai.viewDistance + 2.5f;
            lamp.intensity = 9f;
            lamp.color = new Color(1f, 0.93f, 0.7f);
            lamp.shadows = LightShadows.Soft;
            lamp.shadowStrength = 0.95f;
            ai.flashlight = lamp;

            // Laser a brilhar no escuro
            foreach (var r in rig.AimLaser.GetComponentsInChildren<Renderer>())
                r.material = Gfx.MakeGlow(new Color(1f, 0.15f, 0.1f, 0.95f));

            // Feixe da lanterna desenhado no chão (cortado pelas caixas)
            var cone = new GameObject("VisionCone");
            cone.AddComponent<MeshFilter>();
            var mr = cone.AddComponent<MeshRenderer>();
            mr.material = Gfx.MakeGlow(new Color(1f, 0.95f, 0.6f, 0.12f));
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            cone.AddComponent<VisionConeVisual>().enemy = ai;

            root.SetActive(true);
        }

        /// <summary>
        /// Percurso de patrulha: o desenhado no nível (Routes) ou, se não existir,
        /// o ponto inicial + 3 pontos aleatórios alcançáveis perto dele.
        /// </summary>
        private Vector3[] MakePatrolRoute(LevelData data, int guardIndex, Vector3 spawn)
        {
            var cells = data.GetRoute(guardIndex);
            if (cells.Count > 0)
            {
                var route = new List<Vector3>();
                foreach (var cell in cells)
                {
                    Vector3 p = CellToWorld(cell.x, cell.y);
                    if (NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas)) route.Add(hit.position);
                }
                if (route.Count > 0) return route.ToArray();
            }

            var points = new List<Vector3> { spawn };
            int tries = 0;
            while (points.Count < 4 && tries++ < 40)
            {
                if (!EnemyAI.RandomPointNear(spawn, 7f, out var p)) continue;
                if (Vector3.Distance(p, points[points.Count - 1]) < 3f) continue;
                points.Add(p);
            }
            return points.ToArray();
        }

        // ------------------------------------------------------------------ Câmara e luz

        private static void SetupCameraAndLight(Transform player)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = camGo.AddComponent<Camera>();
            }
            cam.fieldOfView = 42f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.01f, 0.012f, 0.02f);
            var follow = cam.gameObject.GetComponent<CameraFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
            follow.target = player;
            follow.offset = new Vector3(0f, 24f, -8f); // quase de cima, como nos jogos stealth top-down

            // NOITE: só um luar azulado muito fraco — o resto da luz vem das lanternas dos guardas
            Light light = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { light = l; break; }
            if (light == null)
            {
                light = new GameObject("Directional Light").AddComponent<Light>();
                light.type = LightType.Directional;
            }
            light.color = new Color(0.45f, 0.55f, 0.85f);
            light.intensity = 0.22f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;
            light.transform.rotation = Quaternion.Euler(62f, -35f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.055f, 0.062f, 0.09f);
            RenderSettings.fog = false;
        }
    }

    /// <summary>Guarda o NavMesh construído em runtime e remove-o quando o nível é descarregado.</summary>
    public class RuntimeNavMesh : MonoBehaviour
    {
        private NavMeshDataInstance _instance;
        private NavMeshData _data;

        public void Init(NavMeshDataInstance instance, NavMeshData data)
        {
            _instance = instance;
            _data = data;
        }

        private void OnDestroy()
        {
            if (_instance.valid) _instance.Remove();
            if (_data != null) Destroy(_data);
        }
    }
}

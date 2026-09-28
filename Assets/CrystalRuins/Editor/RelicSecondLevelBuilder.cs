using System;
using System.IO;
using System.Linq;
using Gamekit2D;
using CrystalRuins;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Authors only RelicRun2. The existing, manually edited first level is never rebuilt or saved here.
/// </summary>
public static class RelicSecondLevelBuilder
{
    private const string Kit = "Assets/2DGamekit/";
    private const string FirstScenePath = "Assets/CrystalRuins/Scenes/RelicRun.unity";
    private const string ScenePath = "Assets/CrystalRuins/Scenes/RelicRun2.unity";
    private const string TemplatePath = Kit + "Scenes/Utilities/_TemplateScene.unity";
    private static Scene scene;
    private static RelicGameManager manager;
    private static Transform terrainRoot;
    private static Transform props;
    private static TileBase rock;
    private static Material spriteMaterial;

    [MenuItem("Relic Run/Build second level only")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before building the second level.");

        // Never silently save or discard another scene, especially the user's edited first level.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isLoaded && (open.isDirty || string.IsNullOrEmpty(open.path)))
                throw new InvalidOperationException("Save open scenes before building RelicRun2. No scene was changed.");
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FirstScenePath) == null)
            throw new InvalidOperationException("The first level must already exist at " + FirstScenePath);
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplatePath) == null)
            throw new InvalidOperationException("Missing 2D Game Kit template scene.");

        // Reuse the first level's material without editing it or creating any shared asset.
        spriteMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/CrystalRuins/RelicSprite.mat");
        rock = AssetDatabase.LoadAssetAtPath<TileBase>(Kit + "Art/TileMapPalettes/Tileset/TilesetRockRules.asset");
        if (spriteMaterial == null || rock == null)
            throw new InvalidOperationException("Required RelicSprite material or Game Kit rock tile is missing.");

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        Scene template = default;
        bool openedTemplate = false;
        try
        {
            Directory.CreateDirectory("Assets/CrystalRuins/Scenes");
            // Additive creation keeps all first-level objects untouched in memory.
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Scene oldSecond = SceneManager.GetSceneByPath(ScenePath);
            if (oldSecond.IsValid() && oldSecond.isLoaded) EditorSceneManager.CloseScene(oldSecond, true);
            SceneManager.SetActiveScene(scene);

            template = SceneManager.GetSceneByPath(TemplatePath);
            if (!template.IsValid() || !template.isLoaded)
            {
                template = EditorSceneManager.OpenScene(TemplatePath, OpenSceneMode.Additive);
                openedTemplate = true;
            }
            GameObject sourceBackground = template.GetRootGameObjects().FirstOrDefault(x => x.name == "Background");
            if (sourceBackground == null) throw new InvalidOperationException("Game Kit template has no Background root.");
            GameObject background = UnityEngine.Object.Instantiate(sourceBackground);
            background.name = "Background - Shattered Causeway panorama";
            SceneManager.MoveGameObjectToScene(background, scene);
            background.transform.position += Vector3.up * 5f;
            Transform sky = background.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Sky");
            if (sky != null) sky.position += Vector3.right * 40f;
            if (openedTemplate)
            {
                EditorSceneManager.CloseScene(template, true);
                openedTemplate = false;
            }
            SceneManager.SetActiveScene(scene);

            BuildUtilities();
            PlayerCharacter player = BuildPlayer();
            BuildManager(player);
            RelicTeleportAbility teleport = player.gameObject.AddComponent<RelicTeleportAbility>();
            teleport.player = player;
            teleport.manager = manager;
            // Ability defaults are speed 14, range 18, and lifetime 1.5 seconds.
            BuildCamera(player);
            BuildTerrain();
            BuildEncounters();
            BuildRelics();
            BuildExit();
            BuildPresentation();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save " + ScenePath);

            // The two campaign levels lead the build; preserve unrelated existing entries after them.
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(FirstScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            }.Concat(EditorBuildSettings.scenes.Where(x => x.path != FirstScenePath && x.path != ScenePath)).ToArray();
            Debug.Log("RelicRun2 saved: THE SHATTERED CAUSEWAY; gaps 10/11/12, 8 relics, 6 required. First level unchanged.");
        }
        finally
        {
            if (openedTemplate && template.IsValid() && template.isLoaded)
                EditorSceneManager.CloseScene(template, true);
            // Unity cannot remove its last open scene. If the second level was the only
            // original scene, its saved replacement already matches the restored setup.
            if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(scene, true);
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            manager = null;
            terrainRoot = null;
            props = null;
        }
    }

    private static void BuildUtilities()
    {
        Prefab("Utilities/VFXController", Vector3.zero);
        Prefab("Utilities/PhysicsHelper", Vector3.zero);
        GameObject fader = Prefab("SceneControl/ScreenFader", Vector3.zero);
        foreach (CanvasGroup group in fader.GetComponentsInChildren<CanvasGroup>(true))
        {
            group.alpha = 0;
            group.blocksRaycasts = false;
            group.interactable = false;
        }
        Prefab("SceneControl/SceneController", Vector3.zero);
        Prefab("GlobalLight2D", Vector3.zero);
    }

    private static PlayerCharacter BuildPlayer()
    {
        PlayerCharacter player = Prefab("Ellen", new Vector3(2, 1, 0), "Ellen - causeway explorer").GetComponent<PlayerCharacter>();
        player.maxSpeed = 7;
        player.gravity = 38;
        player.jumpSpeed = 16.5f;
        player.hurtJumpSpeed = 4;
        player.damageable.startingHealth = 5;
        player.damageable.invulnerabilityDuration = 1.5f;
        player.damageable.dataSettings.persistenceType = DataSettings.PersistenceType.DoNotPersist;
        player.GetComponent<Gamekit2D.PlayerInput>().dataSettings.persistenceType = DataSettings.PersistenceType.DoNotPersist;
        player.GetComponent<InventoryController>().dataSettings.persistenceType = DataSettings.PersistenceType.DoNotPersist;
        return player;
    }

    private static void BuildManager(PlayerCharacter player)
    {
        manager = new GameObject("Relic Run 02 - Game Loop", typeof(RelicGameManager)).GetComponent<RelicGameManager>();
        manager.player = player;
        manager.requiredRelics = 6;
        manager.totalRelics = 8;
        manager.levelNumber = 2;
        manager.levelTitle = "THE SHATTERED CAUSEWAY";
        manager.nextSceneName = "RelicRun3";
        manager.pickupSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Kit + "Audio/Interactables/GenericPickup.ogg");
        manager.winSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Kit + "Audio/Enemies/Gunner/GunnerDefeatedSting2.ogg");
        manager.loseSound = AssetDatabase.LoadAssetAtPath<AudioClip>(Kit + "Audio/Music/MusicDeathSting.ogg");
    }

    private static void BuildCamera(PlayerCharacter player)
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(RelicCamera));
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6.8f;
        camera.backgroundColor = new Color(.12f, .2f, .23f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.farClipPlane = 200;
        cameraObject.transform.position = new Vector3(8, 3.7f, -20);
        RelicCamera follow = cameraObject.GetComponent<RelicCamera>();
        follow.target = player.transform;
        follow.minX = 8;
        follow.maxX = 102;
        follow.height = 3.7f;
        follow.orthographicSize = 6.8f;
        follow.lookAhead = 4.5f;
    }

    private static void BuildTerrain()
    {
        terrainRoot = new GameObject("Terrain - The Shattered Causeway", typeof(Grid)).transform;
        props = new GameObject("Causeway ruins, foliage and teaching signs").transform;

        // All four top surfaces are y=0, giving the horizontal orb matching-height arrivals.
        // Gap widths 10/11/12 exceed Ellen's roughly 6-unit unassisted jump range.
        Land("01 Beacon approach", -6, 18, -7, 0);
        Land("02 Guardian isle", 28, 47, -7, 0);
        Land("03 Restored outpost", 58, 78, -7, 0);
        Land("04 Last sanctuary", 90, 114, -7, 0);
        Land("West boundary", -7, -5, -7, 7);
        Land("East boundary", 113, 115, -7, 7);

        // No platforms, low ceilings, hazards, or decorative colliders occupy these landing lanes:
        // first [28,34], second [58,66], third [90,98]. The capsule has ample headroom.
        GameObject fall = new GameObject("Causeway void - defeat trigger", typeof(BoxCollider2D), typeof(RelicFallZone));
        fall.transform.position = new Vector3(54, -7, 0);
        BoxCollider2D fallCollider = fall.GetComponent<BoxCollider2D>();
        fallCollider.size = new Vector2(132, 2);
        fallCollider.isTrigger = true;
        fall.GetComponent<RelicFallZone>().manager = manager;
    }

    private static void BuildEncounters()
    {
        foreach (float x in new[] { 37f, 103f })
        {
            EnemyBehaviour enemy = Prefab("Enemies/Chomper", new Vector3(x, 1, 0), "Causeway Chomper guardian").GetComponent<EnemyBehaviour>();
            enemy.speed = 1.5f;
            enemy.viewDistance = 4;
            enemy.viewFov = 100;
        }
        foreach (float x in new[] { 41f, 69f })
        {
            GameObject spikes = Prefab("Environment/Spikes", new Vector3(x, 0, 0), "Interior island spikes");
            foreach (Damager damager in spikes.GetComponentsInChildren<Damager>())
            {
                damager.damage = 1;
                damager.forceRespawn = false;
                damager.ignoreInvincibility = false;
            }
        }
        Prefab("Interactables/HealthPickup", new Vector3(64, 1.4f, 0), "Causeway recovery cache");
    }

    private static void BuildRelics()
    {
        Vector2[] positions =
        {
            new Vector2(5, 1.5f), new Vector2(14, 1.5f),
            new Vector2(30, 1.5f), new Vector2(44, 1.5f),
            new Vector2(60, 1.5f), new Vector2(75, 1.5f),
            new Vector2(93, 1.5f), new Vector2(108, 1.5f)
        };
        GameObject key = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Prefabs/Interactables/Key.prefab");
        Sprite keySprite = key.GetComponentsInChildren<SpriteRenderer>(true).First(x => x.sprite != null).sprite;
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject relic = new GameObject("Causeway relic " + (i + 1).ToString("00"), typeof(CircleCollider2D), typeof(RelicPickup));
            relic.transform.position = positions[i];
            CircleCollider2D collider = relic.GetComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = .62f;
            GameObject visual = new GameObject("Glowing relic", typeof(SpriteRenderer));
            visual.transform.SetParent(relic.transform, false);
            visual.transform.localScale = Vector3.one * 1.1f;
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sprite = keySprite;
            renderer.sharedMaterial = spriteMaterial;
            renderer.color = new Color(.8f, 1f, .58f);
            renderer.sortingLayerName = "Front";
            renderer.sortingOrder = 5;
            RelicPickup pickup = relic.GetComponent<RelicPickup>();
            pickup.manager = manager;
            pickup.visual = visual.transform;
        }
    }

    private static void BuildExit()
    {
        GameObject portal = Prefab("Interactables/Teleporter", new Vector3(110, 0, 0), "Final causeway exit");
        foreach (TransitionPoint transition in portal.GetComponentsInChildren<TransitionPoint>())
            UnityEngine.Object.DestroyImmediate(transition);
        foreach (Collider2D collider in portal.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        SpriteRenderer glow = portal.GetComponent<SpriteRenderer>();
        glow.sharedMaterial = spriteMaterial;
        GameObject exit = new GameObject("Final exit - six relics required", typeof(BoxCollider2D), typeof(RelicExit));
        exit.transform.position = new Vector3(110, 1.5f, 0);
        BoxCollider2D exitCollider = exit.GetComponent<BoxCollider2D>();
        exitCollider.size = new Vector2(2, 3);
        exitCollider.isTrigger = true;
        RelicExit exitLogic = exit.GetComponent<RelicExit>();
        exitLogic.manager = manager;
        exitLogic.glow = glow;
    }

    private static void BuildPresentation()
    {
        foreach (float x in new[] { -3f, 1f, 8f, 12f, 32f, 35f, 43f, 62f, 66f, 72f, 95f, 99f, 106f, 112f })
            Deco("Plants", ((int)x + 12) % 6, x, 0, .6f);
        Deco("AlienStatue01", 0, -1, 0, .8f);
        Deco("AlienStatue03", 0, 34.5f, 0, .65f);
        Deco("AlienSculpture01", 0, 65.5f, 0, .7f);
        Deco("AlienSculpture02", 0, 106, 0, .8f);

        Label("01 / AWAKEN THE BEACON", 7, 5.5f);
        Label("B: TELEPORT MODE", 13.5f, 4.25f, .43f);
        Label("O: SEND ORB   |   L: WARP", 13.5f, 3.55f, .36f);
        Label("02 / THE BROKEN SPAN", 36, 5.5f);
        Label("03 / THE RESTING ISLE", 67.5f, 5.5f);
        Label("04 / THE LAST SANCTUARY", 102, 5.5f);
        Label("FINAL EXIT  >", 110, 4.35f, .48f);

        foreach (float x in new[] { 16f, 45f, 76f })
            Label("SEND ORB  >", x - 1, 2.8f, .32f);
        foreach (float x in new[] { 30f, 60f, 92f })
            Label("SAFE ARRIVAL", x + .8f, 3.7f, .30f);

        Hint("Teleport introduction", 8, 1, "These gaps are too wide to jump. B selects teleport mode; O sends an orb; L warps to it.", 8);
        Hint("First launch lesson", 15.3f, 1, "Face right on solid ground. Press O, wait until the orb is over the far shore, then press L.", 8);
        Hint("First arrival", 30, 1, "You made it! B switches back to normal shots. K still strikes nearby guardians.", 6);
        Hint("Second launch lesson", 45, 1, "Select teleport mode with B if needed. Fire across, then L when the orb reaches clear ground.", 7);
        Hint("Health cache lesson", 63, 1, "A recovery cache waits here. The next landing is broad and clear: send your orb across before warping.", 6);
        Hint("Final launch lesson", 76, 1, "The final gap is wider. Stand near the edge, face right, and let the orb clear the far ledge before L.", 8);
        Hint("Final arrival", 93, 1, "Last sanctuary! Recover at least 6 relics, then reach the glowing exit to the Folded Archive.", 6);

        AudioSource music = new GameObject("Causeway ambience - Game Kit music", typeof(AudioSource)).GetComponent<AudioSource>();
        music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Kit + "Audio/Music/MusicGameplay.ogg");
        music.loop = true;
        music.playOnAwake = true;
        music.volume = .24f;
        music.spatialBlend = 0;
    }

    private static GameObject Prefab(string relativePath, Vector3 position, string name = null)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Prefabs/" + relativePath + ".prefab");
        if (source == null) throw new InvalidOperationException("Missing Game Kit prefab: " + relativePath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
        instance.transform.position = position;
        if (name != null) instance.name = name;
        return instance;
    }

    private static void Land(string name, int left, int right, int bottom, int top)
    {
        GameObject island = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
        island.transform.SetParent(terrainRoot, false);
        island.layer = 31;
        Tilemap map = island.GetComponent<Tilemap>();
        TilemapRenderer renderer = island.GetComponent<TilemapRenderer>();
        renderer.sortingLayerName = "Level";
        renderer.sortingOrder = 0;
        renderer.sharedMaterial = spriteMaterial;
        for (int x = left; x < right; x++)
            for (int y = bottom; y < top; y++) map.SetTile(new Vector3Int(x, y, 0), rock);
        island.AddComponent<TilemapCollider2D>();
    }

    private static void Deco(string file, int index, float x, float y, float scale)
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(Kit + "Art/Sprites/Environment/" + file + ".png")
            .OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
        if (sprites.Length == 0) throw new InvalidOperationException("Missing decoration sprites: " + file);
        GameObject decoration = new GameObject(file + " decoration", typeof(SpriteRenderer));
        decoration.transform.SetParent(props, false);
        SpriteRenderer renderer = decoration.GetComponent<SpriteRenderer>();
        renderer.sprite = sprites[index % sprites.Length];
        renderer.sharedMaterial = spriteMaterial;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = -3;
        decoration.transform.localScale = Vector3.one * scale;
        decoration.transform.position = new Vector3(x, y - renderer.sprite.bounds.min.y * scale, 0);
    }

    private static void Hint(string name, float x, float y, string message, float duration)
    {
        GameObject zone = new GameObject(name, typeof(BoxCollider2D), typeof(RelicHintZone));
        zone.transform.position = new Vector3(x, y, 0);
        BoxCollider2D collider = zone.GetComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = new Vector2(2.4f, 5);
        RelicHintZone hint = zone.GetComponent<RelicHintZone>();
        hint.manager = manager;
        hint.message = message;
        hint.duration = duration;
    }

    private static void Label(string text, float x, float y, float size = .53f)
    {
        GameObject label = new GameObject(text, typeof(TextMesh));
        label.transform.SetParent(props, false);
        label.transform.position = new Vector3(x, y, -1);
        TextMesh mesh = label.GetComponent<TextMesh>();
        mesh.text = text;
        mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.fontSize = 50;
        mesh.characterSize = size / 5;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.color = new Color(.82f, .92f, .8f);
        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mesh.font.material;
        renderer.sortingLayerName = "Front";
    }
}

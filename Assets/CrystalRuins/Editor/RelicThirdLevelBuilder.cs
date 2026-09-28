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
/// Authors RelicRun3 only. Re-running replaces this generated scene; save manual edits first.
/// Existing RelicRun and RelicRun2 scene objects and assets are never rebuilt or saved here.
/// </summary>
public static class RelicThirdLevelBuilder
{
    private const string Kit = "Assets/2DGamekit/";
    private const string FirstScenePath = "Assets/CrystalRuins/Scenes/RelicRun.unity";
    private const string SecondScenePath = "Assets/CrystalRuins/Scenes/RelicRun2.unity";
    private const string ScenePath = "Assets/CrystalRuins/Scenes/RelicRun3.unity";
    private const string TemplatePath = Kit + "Scenes/Utilities/_TemplateScene.unity";
    private static Scene scene;
    private static RelicGameManager manager;
    private static Transform terrainRoot;
    private static Transform props;
    private static Transform portalRoot;
    private static TileBase rock;
    private static Material spriteMaterial;
    private static readonly Color Cyan = new Color(.26f, .98f, .94f);
    private static readonly Color Amber = new Color(1f, .73f, .25f);
    private static readonly Color Cobalt = new Color(.48f, .65f, 1f);
    private static readonly Color Green = new Color(.65f, 1f, .64f);

    [MenuItem("Relic Run/Build third level only")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before building the third level.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isLoaded && (open.isDirty || string.IsNullOrEmpty(open.path)))
                throw new InvalidOperationException("Save open scenes before building RelicRun3. No scene was changed.");
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(FirstScenePath) == null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>(SecondScenePath) == null)
            throw new InvalidOperationException("The existing first and second levels must be present.");
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TemplatePath) == null)
            throw new InvalidOperationException("Missing 2D Game Kit template scene.");
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
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Scene oldThird = SceneManager.GetSceneByPath(ScenePath);
            if (oldThird.IsValid() && oldThird.isLoaded) EditorSceneManager.CloseScene(oldThird, true);
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
            background.name = "Background - Folded Archive panorama";
            SceneManager.MoveGameObjectToScene(background, scene);
            background.transform.position += Vector3.up * 6f;
            Transform sky = background.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "Sky");
            if (sky != null) sky.position += Vector3.right * 70f;
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
            BuildCamera(player);
            BuildTerrain();
            BuildPortalPuzzles();
            BuildRelics();
            BuildExit();
            BuildPresentation();
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Could not save " + ScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(FirstScenePath, true),
                new EditorBuildSettingsScene(SecondScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            }.Concat(EditorBuildSettings.scenes.Where(x => x.path != FirstScenePath &&
                x.path != SecondScenePath && x.path != ScenePath)).ToArray();
            Debug.Log("RelicRun3 saved: THE FOLDED ARCHIVE; body lift, beacon window, three-route hub; all 4 relics required. Earlier levels unchanged.");
        }
        finally
        {
            if (openedTemplate && template.IsValid() && template.isLoaded)
                EditorSceneManager.CloseScene(template, true);
            if (scene.IsValid() && scene.isLoaded && SceneManager.sceneCount > 1)
                EditorSceneManager.CloseScene(scene, true);
            EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            manager = null;
            terrainRoot = null;
            props = null;
            portalRoot = null;
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
        PlayerCharacter player = Prefab("Ellen", new Vector3(1, 1, 0), "Ellen - archive pathfinder").GetComponent<PlayerCharacter>();
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
        manager = new GameObject("Relic Run 03 - Game Loop", typeof(RelicGameManager)).GetComponent<RelicGameManager>();
        manager.player = player;
        manager.requiredRelics = 4;
        manager.totalRelics = 4;
        manager.levelNumber = 3;
        manager.levelTitle = "THE FOLDED ARCHIVE";
        manager.nextSceneName = "";
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
        camera.orthographicSize = 8.2f;
        camera.backgroundColor = new Color(.07f, .13f, .19f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.farClipPlane = 200;
        cameraObject.transform.position = new Vector3(8, 4.5f, -20);
        RelicCamera follow = cameraObject.GetComponent<RelicCamera>();
        follow.target = player.transform;
        follow.minX = 8;
        follow.maxX = 163;
        follow.height = 4.5f;
        follow.orthographicSize = 8.2f;
        follow.lookAhead = 3.5f;
    }

    private static void BuildTerrain()
    {
        terrainRoot = new GameObject("Terrain - The Folded Archive", typeof(Grid)).transform;
        props = new GameObject("Archive ruins, route colors and teaching signs").transform;
        portalRoot = new GameObject("Archive portal network - move root with its markers").transform;
        Land("01 Entry court", -6, 9, -7, 0);
        Land("01 Fold balcony - launch at 16", 9, 18, -7, 5);
        Land("01 Far balcony - arrive beyond 29", 29, 47, -7, 5);
        Land("02 Sealed archive wall", 47, 50, -7, 13);
        Land("02 Beacon window landing", 50, 69, -7, 5);
        Land("03 Route selector hub", 76, 98, -7, 0);
        Land("03 Hub east wall", 98, 100, -7, 13);
        Land("03 Amber archive", 104, 120, -7, 3);
        Land("03 Amber east wall", 120, 122, -7, 13);
        Land("03 Cobalt archive", 130, 146, -7, 3);
        Land("03 Cobalt east wall", 146, 148, -7, 13);
        Land("04 Restoration chamber", 156, 174, -7, 0);
        Land("West boundary", -8, -6, -7, 13);
        Land("East boundary", 174, 176, -7, 13);
        GameObject fall = new GameObject("Archive void - defeat trigger", typeof(BoxCollider2D), typeof(RelicFallZone));
        fall.transform.position = new Vector3(84, -7, 0);
        BoxCollider2D box = fall.GetComponent<BoxCollider2D>();
        box.size = new Vector2(200, 2);
        box.isTrigger = true;
        fall.GetComponent<RelicFallZone>().manager = manager;
    }

    private static void BuildPortalPuzzles()
    {
        // Receivers are deliberately non-enterable. Each arrival marker is separated from
        // its return entrance; standing still after a fold can never trigger another fold.
        RelicPortal balcony = Portal("01 Balcony arrival", 11.5f, 5, Cyan, false, false);
        RelicPortal lift = Portal("01 WALK IN - balcony fold", 6, 0, Cyan, true, true);
        lift.destination = balcony;

        RelicPortal windowArrival = Portal("02 Window far-side arrival", 53, 5, Cyan, false, false);
        RelicPortal window = Portal("02 BEACONS ONLY - sealed window", 45, 5, Cyan, false, true);
        window.destination = windowArrival;
        window.GetComponent<BoxCollider2D>().size = new Vector2(1.3f, 3.2f);
        window.GetComponent<BoxCollider2D>().offset = new Vector2(0, 1.5f);
        Label("BEACONS ONLY", 44.5f, 8.7f, .40f).color = Cyan;
        Label("O THROUGH THE WINDOW\nL AFTER IT APPEARS ON THE RIGHT", 38.5f, 9.3f, .29f);
        Label("ARRIVAL\nTHEN PRESS L", 54, 8.75f, .30f).color = Cyan;

        RelicPortal hubArrival = Portal("03 Hub arrival - safe return", 79.5f, 0, Green, false, false);
        RelicPortal hubEntry = Portal("02 WALK IN - route hall", 65, 5, Green, true, true);
        hubEntry.destination = hubArrival;

        RelicPortal amberArrival = Portal("03 Amber archive arrival", 109, 3, Amber, false, false);
        RelicPortal cobaltArrival = Portal("03 Cobalt archive arrival", 135, 3, Cobalt, false, false);
        RelicPortal exitArrival = Portal("04 Restoration chamber arrival", 161, 0, Green, false, false);
        RelicPortal routeGate = Portal("03 ROUTED BEACON GATE - use console first", 91, 0, Green, false, true);
        routeGate.destination = amberArrival;

        GameObject console = new GameObject("03 E route console - Amber, Cobalt, Exit", typeof(BoxCollider2D), typeof(RelicPortalSwitch));
        console.transform.SetParent(portalRoot, false);
        console.transform.position = new Vector3(86.5f, 1.1f, 0);
        BoxCollider2D consoleTrigger = console.GetComponent<BoxCollider2D>();
        consoleTrigger.isTrigger = true;
        consoleTrigger.size = new Vector2(4f, 3.3f);
        RelicPortalSwitch route = console.GetComponent<RelicPortalSwitch>();
        route.manager = manager;
        route.controlledPortal = routeGate;
        route.destinations = new[] { amberArrival, cobaltArrival, exitArrival };
        route.routeNames = new[] { "AMBER ARCHIVE", "COBALT ARCHIVE", "RESTORATION EXIT" };
        route.interactionDistance = 2.4f;
        route.routeLabel = Label("ROUTE: AMBER ARCHIVE\n[E] CHANGE DESTINATION", 86.5f, 4.8f, .38f);
        Label("E", 86.5f, 2.4f, .8f).color = Amber;
        Deco("AlienSculpture01", 0, 86.5f, 0, .5f);

        Portal("03 Amber return - WALK IN", 105.5f, 3, Green, true, true).destination = hubArrival;
        Portal("03 Cobalt return - WALK IN", 131.5f, 3, Green, true, true).destination = hubArrival;
        Portal("04 Exit return - WALK IN", 157.5f, 0, Green, true, true).destination = hubArrival;

        // A skipped early seal must remain recoverable even after reaching the final door.
        RelicPortal backtrackSecond = Portal("02 Backtrack arrival from hub", 59.5f, 5, Green, false, false);
        RelicPortal backtrackFirst = Portal("01 Backtrack arrival from window", 39, 5, Green, false, false);
        Portal("03 BACKTRACK - previous chamber", 77.2f, 0, Green, true, true).destination = backtrackSecond;
        Portal("02 BACKTRACK - first seal", 51.2f, 5, Green, true, true).destination = backtrackFirst;
        Label("< BACKTRACK", 77.2f, 4.2f, .29f).color = Green;
        Label("< FIRST SEAL", 51.0f, 7.9f, .24f).color = Green;

        Label("RETURN TO ROUTE HALL", 106, 7f, .28f).color = Green;
        Label("RETURN TO ROUTE HALL", 132, 7f, .28f).color = Green;
        Label("RETURN TO ROUTE HALL", 157.5f, 4f, .28f).color = Green;
        Label("AMBER SEAL  >", 115.5f, 6f, .38f).color = Amber;
        Label("COBALT SEAL  >", 141.5f, 6f, .38f).color = Cobalt;
        Label("BEACON GATE  >", 92, 3.8f, .36f).color = Green;
        Label("O THROUGH GATE, THEN L\nTHE VIEW FOLLOWS YOUR SHOT", 89, 6.3f, .28f).color = Cyan;
    }

    private static RelicPortal Portal(string name, float x, float ground, Color color, bool allowPlayer, bool allowProjectiles)
    {
        GameObject root = new GameObject(name, typeof(BoxCollider2D), typeof(RelicPortal));
        root.transform.SetParent(portalRoot, false);
        root.transform.position = new Vector3(x, ground, 0);
        BoxCollider2D trigger = root.GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.6f, 3.1f);
        trigger.offset = new Vector2(0, 1.4f);
        RelicPortal portal = root.GetComponent<RelicPortal>();
        portal.manager = manager;
        portal.portalName = name;
        portal.allowPlayer = allowPlayer;
        portal.allowProjectiles = allowProjectiles;
        portal.outgoingDirection = Vector2.right;
        Transform feet = new GameObject("Player arrival - feet, clear of terrain").transform;
        feet.SetParent(root.transform, false);
        feet.localPosition = new Vector3(0, .12f, 0);
        portal.playerArrival = feet;
        Transform orb = new GameObject("Beacon arrival - standing shot height").transform;
        orb.SetParent(root.transform, false);
        orb.localPosition = new Vector3(0, 1.45f, 0);
        portal.projectileArrival = orb;

        GameObject art = PortalArt(name + " - visual", x, ground, color);
        art.transform.SetParent(root.transform, true);
        art.transform.localScale = new Vector3(.75f, .8f, 1);
        return portal;
    }

    private static GameObject PortalArt(string name, float x, float ground, Color color)
    {
        GameObject art = Prefab("Interactables/Teleporter", new Vector3(x, ground - .15f, 0), name);
        foreach (TransitionPoint transition in art.GetComponentsInChildren<TransitionPoint>(true))
            UnityEngine.Object.DestroyImmediate(transition);
        foreach (Collider2D collider in art.GetComponentsInChildren<Collider2D>(true))
            collider.enabled = false;
        foreach (SpriteRenderer renderer in art.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sharedMaterial = spriteMaterial;
            renderer.color = color;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 0;
        }
        return art;
    }

    private static void BuildRelics()
    {
        Vector2[] positions = { new Vector2(32, 6.25f), new Vector2(57, 6.25f), new Vector2(116, 4.25f), new Vector2(142, 4.25f) };
        string[] names = { "01 Flight seal", "02 Window seal", "03 Amber seal", "04 Cobalt seal" };
        Color[] colors = { Cyan, Cyan, Amber, Cobalt };
        GameObject key = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Prefabs/Interactables/Key.prefab");
        Sprite keySprite = key.GetComponentsInChildren<SpriteRenderer>(true).First(x => x.sprite != null).sprite;
        Transform root = new GameObject("Archive seals - move each parent to keep pickup aligned").transform;
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject relic = new GameObject(names[i], typeof(CircleCollider2D), typeof(RelicPickup));
            relic.transform.SetParent(root, false);
            relic.transform.position = positions[i];
            CircleCollider2D collider = relic.GetComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = .7f;
            GameObject visual = new GameObject("Glowing seal - move the parent", typeof(SpriteRenderer));
            visual.transform.SetParent(relic.transform, false);
            visual.transform.localScale = Vector3.one * 1.25f;
            SpriteRenderer renderer = visual.GetComponent<SpriteRenderer>();
            renderer.sprite = keySprite;
            renderer.sharedMaterial = spriteMaterial;
            renderer.color = colors[i];
            renderer.sortingLayerName = "Front";
            renderer.sortingOrder = 5;
            RelicPickup pickup = relic.GetComponent<RelicPickup>();
            pickup.manager = manager;
            pickup.visual = visual.transform;
        }
    }

    private static void BuildExit()
    {
        GameObject art = PortalArt("Final archive exit - visual", 170, 0, Green);
        GameObject exit = new GameObject("Final archive exit - ALL FOUR seals required", typeof(BoxCollider2D), typeof(RelicExit));
        exit.transform.position = new Vector3(170, 1.5f, 0);
        BoxCollider2D collider = exit.GetComponent<BoxCollider2D>();
        collider.size = new Vector2(2, 3);
        collider.isTrigger = true;
        RelicExit logic = exit.GetComponent<RelicExit>();
        logic.manager = manager;
        logic.glow = art.GetComponent<SpriteRenderer>();
    }

    private static void BuildPresentation()
    {
        Deco("AlienStatue01", 0, -2, 0, .7f);
        Deco("AlienStatue03", 0, 34.5f, 5, .55f);
        Deco("AlienSculpture02", 0, 60, 5, .6f);
        Deco("AlienStatue01", 0, 94.5f, 0, .65f);
        Deco("AlienSculpture01", 0, 113, 3, .55f);
        Deco("AlienSculpture02", 0, 139, 3, .55f);
        Deco("AlienStatue03", 0, 165, 0, .75f);
        foreach (Vector2 p in new[] { new Vector2(3, 0), new Vector2(13.5f, 5), new Vector2(36, 5), new Vector2(39, 5),
            new Vector2(59, 5), new Vector2(62, 5), new Vector2(82, 0), new Vector2(95.5f, 0), new Vector2(111, 3),
            new Vector2(118, 3), new Vector2(137, 3), new Vector2(144, 3), new Vector2(163.5f, 0), new Vector2(172, 0) })
            Deco("Plants", ((int)p.x) % 6, p.x, p.y, .45f);

        Label("01 / FOLD + FLIGHT", 12, 9.3f, .52f);
        Label("WALK INTO THE CYAN PORTAL", 3.5f, 4.6f, .34f).color = Cyan;
        Label("B: BEACON   O: FIRE   L: WARP", 16, 8.45f, .33f);
        Label("11-UNIT GAP\nSEND A BEACON  >", 22.5f, 7.3f, .34f).color = Cyan;
        Label("SAFE LANDING", 31.5f, 8.7f, .30f).color = Cyan;
        Label("02 / THROUGH THE SEALED WALL", 58.0f, 9.3f, .40f);
        Label("WALK IN FOR THE ROUTE HALL", 64, 8.6f, .27f).color = Green;
        Label("03 / THE ROUTING PUZZLE", 86, 9.2f, .58f);
        Label("CHOOSE BOTH SEAL ROOMS, THEN THE EXIT", 86, 7.9f, .32f);
        Label("AMBER ARCHIVE", 112, 10.2f, .6f).color = Amber;
        Label("TAKE THE SEAL. GREEN PORTAL RETURNS TO THE CONSOLE.", 112, 8.6f, .24f);
        Label("COBALT ARCHIVE", 138, 10.2f, .6f).color = Cobalt;
        Label("TAKE THE SEAL. GREEN PORTAL RETURNS TO THE CONSOLE.", 138, 8.6f, .24f);
        Label("RESTORATION CHAMBER", 165, 9f, .58f).color = Green;
        Label("ALL 4 SEALS REQUIRED\nRETURN LEFT IF ANY ARE MISSING", 165, 7.2f, .33f);
        Label("FINAL EXIT  >", 170, 4.9f, .42f).color = Green;

        Hint("01 Learn body portals", 3, 1, "Cyan portals fold space. Walk into this one, then send a teleport beacon across the balcony gap.", 8);
        Hint("01 Balcony launch", 15.8f, 6, "Face right near the edge. B selects beacon; O fires. Press L when it reaches the far balcony, before it fades.", 8);
        Hint("02 Sealed window lesson", 40.5f, 6, "This window carries BEACONS ONLY. Fire right through it; press L just after the orb appears beyond the wall.", 9);
        Hint("02 Far-side confirmation", 55.5f, 6, "A portal moves your beacon, but L moves you. Take the seal, then enter the green route-hall portal.", 7);
        Hint("03 Route console lesson", 83.5f, 1, "E chooses Amber, Cobalt, or Exit. Fire a beacon through the gate. The view follows the folded shot; press L over clear ground.", 10);
        Hint("03 Amber arrival lesson", 109, 4, "AMBER ARCHIVE. Take the seal on the right; walk into the green return portal on the left.", 7);
        Hint("03 Cobalt arrival lesson", 135, 4, "COBALT ARCHIVE. Take the seal on the right; return left and set the console to RESTORATION EXIT.", 7);
        Hint("04 Final lock lesson", 161.5f, 1, "The final door needs ALL FOUR seals. Missing one? The green portal on the left returns to the route console.", 8);

        AudioSource music = new GameObject("Archive ambience - Game Kit music", typeof(AudioSource)).GetComponent<AudioSource>();
        music.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Kit + "Audio/Music/MusicGameplay.ogg");
        music.loop = true;
        music.playOnAwake = true;
        music.volume = .21f;
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

    private static TextMesh Label(string text, float x, float y, float size = .53f)
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
        return mesh;
    }
}

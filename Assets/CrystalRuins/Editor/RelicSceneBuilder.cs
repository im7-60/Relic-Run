using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;
using Gamekit2D;
using CrystalRuins;

public static class RelicSceneBuilder
{
    const string Kit="Assets/2DGamekit/";
    const string ScenePath="Assets/CrystalRuins/Scenes/RelicRun.unity";
    static readonly string Out=Path.GetFullPath("Build/RelicRun");
    static RelicGameManager manager;
    static Transform terrainRoot,props;
    static TileBase rock;
    static Material spriteMat;
    static GameObject Prefab(string path,Vector3 position,string name=null)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Kit+"Prefabs/"+path+".prefab");
        if(!source)throw new Exception("Missing prefab "+path);
        var o=(GameObject)PrefabUtility.InstantiatePrefab(source);o.transform.position=position;if(name!=null)o.name=name;return o;
    }
    static Sprite Sprite(string file,int index=0) {return AssetDatabase.LoadAllAssetsAtPath(Kit+"Art/Sprites/Environment/"+file+".png").OfType<Sprite>().OrderBy(x=>x.name).ElementAt(index);}
    static void Land(string name,int left,int right,int bottom,int top)
    {
        var o=new GameObject(name,typeof(Tilemap),typeof(TilemapRenderer));o.transform.SetParent(terrainRoot,false);o.layer=31;
        var map=o.GetComponent<Tilemap>();var renderer=o.GetComponent<TilemapRenderer>();renderer.sortingLayerName="Level";renderer.sortingOrder=0;renderer.sharedMaterial=spriteMat;
        for(int x=left;x<right;x++)for(int y=bottom;y<top;y++)map.SetTile(new Vector3Int(x,y,0),rock);
        o.AddComponent<TilemapCollider2D>();
    }
    static SpriteRenderer Deco(string file,int index,float x,float y,float scale=1f,int order=-3)
    {
        var o=new GameObject(file+" decoration",typeof(SpriteRenderer));o.transform.SetParent(props);var r=o.GetComponent<SpriteRenderer>();r.sprite=Sprite(file,index);r.sortingLayerName="Default";r.sortingOrder=order;r.sharedMaterial=spriteMat;
        o.transform.localScale=Vector3.one*scale;var b=r.sprite.bounds;o.transform.position=new Vector3(x,y-b.min.y*scale,0);return r;
    }
    static void Hint(string name,float x,float y,string text)
    {
        var o=new GameObject(name,typeof(BoxCollider2D),typeof(RelicHintZone));o.transform.position=new Vector3(x,y,0);var c=o.GetComponent<BoxCollider2D>();c.isTrigger=true;c.size=new Vector2(3,5);var h=o.GetComponent<RelicHintZone>();h.manager=manager;h.message=text;h.duration=5;
    }
    static void Label(string text,float x,float y,float size=.6f)
    {
        var o=new GameObject(text,typeof(TextMesh));o.transform.SetParent(props);o.transform.position=new Vector3(x,y,-1);var t=o.GetComponent<TextMesh>();t.text=text;t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=50;t.characterSize=size/5;t.anchor=TextAnchor.MiddleCenter;t.color=new Color(.82f,.92f,.8f);o.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;o.GetComponent<MeshRenderer>().sortingLayerName="Front";
    }
    [MenuItem("Relic Run/Build assignment scene")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop play before rebuilding");
        var previous=SceneManager.GetActiveScene();if(previous.isDirty&& !string.IsNullOrEmpty(previous.path))EditorSceneManager.SaveScene(previous);
        Directory.CreateDirectory("Assets/CrystalRuins/Scenes");Directory.CreateDirectory(Out);
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var template=EditorSceneManager.OpenScene(Kit+"Scenes/Utilities/_TemplateScene.unity",OpenSceneMode.Additive);
        var sourceBG=template.GetRootGameObjects().First(x=>x.name=="Background");
        var bg=UnityEngine.Object.Instantiate(sourceBG);bg.name="Background - Game Kit panorama";bg.transform.position+=Vector3.up*5f;SceneManager.MoveGameObjectToScene(bg,scene);
        bg.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Sky").position+=Vector3.right*20f;
        EditorSceneManager.CloseScene(template,true);SceneManager.SetActiveScene(scene);
        Prefab("Utilities/VFXController",Vector3.zero);Prefab("Utilities/PhysicsHelper",Vector3.zero);
        var fader=Prefab("SceneControl/ScreenFader",Vector3.zero);foreach(var group in fader.GetComponentsInChildren<CanvasGroup>(true)){group.alpha=0;group.blocksRaycasts=false;group.interactable=false;}
        Prefab("SceneControl/SceneController",Vector3.zero);Prefab("GlobalLight2D",Vector3.zero);
        spriteMat=new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        var materialPath="Assets/CrystalRuins/RelicSprite.mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(existing){UnityEngine.Object.DestroyImmediate(spriteMat);spriteMat=existing;}else AssetDatabase.CreateAsset(spriteMat,materialPath);
        var p=Prefab("Ellen",new Vector3(2,1,0),"Ellen - explorer").GetComponent<PlayerCharacter>();p.maxSpeed=7;p.gravity=38;p.jumpSpeed=16.5f;p.hurtJumpSpeed=4;p.damageable.startingHealth=5;p.damageable.invulnerabilityDuration=1.5f;
        p.damageable.dataSettings.persistenceType=DataSettings.PersistenceType.DoNotPersist;
        p.GetComponent<Gamekit2D.PlayerInput>().dataSettings.persistenceType=DataSettings.PersistenceType.DoNotPersist;
        p.GetComponent<InventoryController>().dataSettings.persistenceType=DataSettings.PersistenceType.DoNotPersist;
        manager=new GameObject("Relic Run - Game Loop",typeof(RelicGameManager)).GetComponent<RelicGameManager>();manager.player=p;manager.requiredRelics=6;manager.totalRelics=8;
        manager.pickupSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Kit+"Audio/Interactables/GenericPickup.ogg");manager.winSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Kit+"Audio/Enemies/Gunner/GunnerDefeatedSting2.ogg");manager.loseSound=AssetDatabase.LoadAssetAtPath<AudioClip>(Kit+"Audio/Music/MusicDeathSting.ogg");
        var camera=new GameObject("Main Camera",typeof(Camera),typeof(RelicCamera));camera.tag="MainCamera";var cam=camera.GetComponent<Camera>();cam.orthographic=true;cam.orthographicSize=6.8f;cam.backgroundColor=new Color(.12f,.2f,.23f);cam.clearFlags=CameraClearFlags.SolidColor;cam.farClipPlane=200;camera.transform.position=new Vector3(8,3.7f,-20);var follow=camera.GetComponent<RelicCamera>();follow.target=p.transform;follow.minX=8;follow.maxX=68;follow.height=3.7f;follow.orthographicSize=6.8f;
        terrainRoot=new GameObject("Terrain - original level",typeof(Grid)).transform;props=new GameObject("Ruins, foliage and signs").transform;
        rock=AssetDatabase.LoadAssetAtPath<TileBase>(Kit+"Art/TileMapPalettes/Tileset/TilesetRockRules.asset");
        Land("01 Landing meadow",-6,18,-7,0);Land("02 Chomper courtyard",21,36,-7,0);Land("03 Lift landing",42,57,-7,1);Land("04 Exit gardens",60,80,-7,1);
        Land("Practice ledge",7,12,1,2);Land("Relic overlook",27,31,1,2);Land("Courtyard stepping stone",32,35,2,3);
        // Low walls keep the player inside the designed route.
        Land("West boundary",-7,-5,-7,7);Land("East boundary",79,81,-7,7);
        var moving=Prefab("Interactables/MovingPlatform",new Vector3(37.5f,.7f,0),"Moving bridge").GetComponent<MovingPlatform>();moving.localNodes=new[]{Vector3.zero,new Vector3(3,1,0)};moving.waitTimes=new[]{.6f,.6f};moving.speed=2;moving.platformType=MovingPlatform.MovingPlatformType.BACK_FORTH;moving.startMovingOnlyWhenVisible=false;moving.isMovingAtStart=true;
        foreach(float x in new[]{25f,49f}){var e=Prefab("Enemies/Chomper",new Vector3(x,x>40?2:1,0),"Chomper guardian").GetComponent<EnemyBehaviour>();e.speed=1.5f;e.viewDistance=5;e.viewFov=100;}
        foreach(float x in new[]{13.5f,47f,61.5f}){var o=Prefab("Environment/Spikes",new Vector3(x,x>40?1:0,0),"Spike hazard");foreach(var d in o.GetComponentsInChildren<Damager>()){d.damage=1;d.forceRespawn=false;d.ignoreInvincibility=false;}}
        var door=Prefab("Interactables/Door",new Vector3(54.8f,2.52f,0),"Pressure gate").GetComponent<Animator>();
        Physics2D.SyncTransforms();var doorCollider=door.GetComponentInChildren<Collider2D>();door.transform.position+=Vector3.up*(1f-doorCollider.bounds.min.y);
        Land("Sanctum gate lintel",53,57,4,6);
        var pad=Prefab("Interactables/PressurePad",new Vector3(52.3f,.90f,0),"Gate pressure switch").GetComponent<PressurePad>();pad.requiredCount=1;pad.activationType=PressurePad.ActivationType.ItemCount;pad.platformCatcher.contactFilter.SetLayerMask(1 << LayerMask.NameToLayer("Player"));pad.platformCatcher.contactFilter.useTriggers=false;UnityEventTools.AddStringPersistentListener(pad.OnPressed,door.Play,"DoorOpening");
        Prefab("Interactables/HealthPickup",new Vector3(44,2.4f,0),"Health cache");
        var positions=new[]{new Vector2(5,1.5f),new Vector2(10,3.3f),new Vector2(16,1.5f),new Vector2(23,1.5f),new Vector2(29,3.4f),new Vector2(40,3),new Vector2(48,2.4f),new Vector2(65,2.3f)};
        var key=AssetDatabase.LoadAssetAtPath<GameObject>(Kit+"Prefabs/Interactables/Key.prefab");var keySprite=key.GetComponentsInChildren<SpriteRenderer>(true).First(x=>x.sprite).sprite;
        for(int i=0;i<positions.Length;i++)
        {
            var o=new GameObject("Relic "+(i+1).ToString("00"),typeof(CircleCollider2D),typeof(RelicPickup));o.transform.position=positions[i];o.GetComponent<CircleCollider2D>().isTrigger=true;o.GetComponent<CircleCollider2D>().radius=.62f;
            var v=new GameObject("Glowing relic",typeof(SpriteRenderer));v.transform.SetParent(o.transform,false);var sr=v.GetComponent<SpriteRenderer>();sr.sprite=keySprite;sr.sharedMaterial=spriteMat;sr.color=new Color(.8f,1f,.58f);sr.sortingLayerName="Front";sr.sortingOrder=5;v.transform.localScale=Vector3.one*1.1f;
            var pick=o.GetComponent<RelicPickup>();pick.manager=manager;pick.visual=v.transform;
        }
        var end=Prefab("Interactables/Teleporter",new Vector3(74,1,0),"Relic exit portal");foreach(var t in end.GetComponentsInChildren<TransitionPoint>())UnityEngine.Object.DestroyImmediate(t);foreach(var c in end.GetComponentsInChildren<Collider2D>())c.enabled=false;end.GetComponent<SpriteRenderer>().sharedMaterial=spriteMat;
        var trigger=new GameObject("Exit - requires six relics",typeof(BoxCollider2D),typeof(RelicExit));trigger.transform.position=new Vector3(74,2.5f,0);trigger.GetComponent<BoxCollider2D>().size=new Vector2(2,3);trigger.GetComponent<BoxCollider2D>().isTrigger=true;trigger.GetComponent<RelicExit>().manager=manager;trigger.GetComponent<RelicExit>().glow=end.GetComponent<SpriteRenderer>();
        var fall=new GameObject("Fall zone",typeof(BoxCollider2D),typeof(RelicFallZone));fall.transform.position=new Vector3(37,-7,0);fall.GetComponent<BoxCollider2D>().size=new Vector2(100,2);fall.GetComponent<BoxCollider2D>().isTrigger=true;fall.GetComponent<RelicFallZone>().manager=manager;
        for(int i=0;i<18;i++){float x=-3+i*4.6f;if((x>18&&x<21)||(x>36&&x<42)||(x>50&&x<56)||(x>57&&x<60))continue;Deco("Plants",i%6,x,x>=42?1:0,.6f,-3);}
        Deco("AlienStatue01",0,-1,0,.8f);Deco("AlienStatue03",0,34,0,.7f);Deco("AlienSculpture01",0,67,1,.8f);Deco("AlienSculpture02",0,71,1,.8f);
        Label("01 / THE LANDING",7,5.2f);Label("02 / THE COURTYARD",28,5.7f);Label("03 / THE CROSSING",40,6);Label("04 / THE SANCTUM",66,6);Label("EXIT   >",74,5.5f);Label("STEP ON THE SWITCH",52,6.8f,.45f);
        Hint("Jump tutorial",5,1,"A / D to move  •  Hold SPACE to jump higher");Hint("Combat tutorial",22,1,"Guardians ahead  •  K to strike  /  O to fire");Hint("Lift tutorial",35,1,"Ride the moving bridge. Wait for it to return before jumping.");Hint("Switch tutorial",51,2,"Step onto the glowing pressure switch to open the stone gate.");Hint("Exit tutorial",69,2,"The portal needs 6 relics. Explore the upper ledges for more.");
        var music=new GameObject("Ruins ambience - music",typeof(AudioSource)).GetComponent<AudioSource>();music.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Kit+"Audio/Music/MusicGameplay.ogg");music.loop=true;music.playOnAwake=true;music.volume=.24f;music.spatialBlend=0;
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};PlayerSettings.productName="Relic Run";PlayerSettings.companyName="Student Games";PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
        EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        Selection.activeGameObject=p.gameObject;SceneView.lastActiveSceneView?.LookAt(new Vector3(12,2,0),Quaternion.identity,15,true);
        File.WriteAllText(Path.Combine(Out,"scene-summary.txt"),"Scene: "+ScenePath+"\n8 relics, 6 required, 5HP; 2 Chompers; 3 spike hazards; moving bridge; pressure gate; health cache.\n");
        Debug.Log("Relic Run scene built and saved.");
    }
    [MenuItem("Relic Run/Build Windows player")]
    public static void BuildPlayer()
    {
        BuildWindowsAt(Path.Combine(Out,"Windows"));
    }
    public static void BuildWindowsAt(string folder)
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before building.");
        for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save edited scenes before building.");
        Directory.CreateDirectory(folder);
        var sceneSetup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            // Keep authoring-scene global lights out of the build's temporary scene validation.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),locationPathName=Path.Combine(folder,"RelicRun.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText(Path.Combine(folder,"..","build-report.txt"),report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nBytes: "+report.summary.totalSize+"\nDuration: "+report.summary.totalTime);
        }
        finally {EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);}
    }
}


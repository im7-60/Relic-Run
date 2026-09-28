using System;
using System.Collections.Generic;
using System.Linq;
using CrystalRuins;
using Gamekit2D;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>Adds adjacent-level return gates without rebuilding any authored terrain or puzzles.</summary>
public static class RelicCampaignPortalBuilder
{
    private const string RootName = "Campaign travel portals and arrival points";
    private static readonly string[] ScenePaths = {
        "Assets/CrystalRuins/Scenes/RelicRun.unity",
        "Assets/CrystalRuins/Scenes/RelicRun2.unity",
        "Assets/CrystalRuins/Scenes/RelicRun3.unity"
    };
    private static readonly Color ReturnColor = new Color(.73f, .57f, 1f);

    [MenuItem("Relic Run/Add adjacent-level return portals")]
    public static void Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play mode before adding campaign portals.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene open = SceneManager.GetSceneAt(i);
            if (open.isLoaded && (open.isDirty || string.IsNullOrEmpty(open.path)))
                throw new InvalidOperationException("Save open scenes before adding campaign portals. No scene was changed.");
        }
        foreach (string path in ScenePaths)
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                throw new InvalidOperationException("Missing campaign scene: " + path);
        GameObject teleporter = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/2DGamekit/Prefabs/Interactables/Teleporter.prefab");
        Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/CrystalRuins/RelicSprite.mat");
        if (teleporter == null || material == null)
            throw new InvalidOperationException("The Game Kit teleporter and RelicSprite material must be available.");

        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            for (int i = 0; i < ScenePaths.Length; i++)
            {
                Scene scene = EditorSceneManager.OpenScene(ScenePaths[i], OpenSceneMode.Single);
                ApplyToScene(scene, i, teleporter, material);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("Could not save " + scene.path);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(previousSetup); }
        Debug.Log("Campaign portals saved: level 1 end <-> level 2 start; level 2 end <-> level 3 start. Existing level layouts preserved.");
    }

    private static void ApplyToScene(Scene scene, int index, GameObject teleporter, Material material)
    {
        RelicGameManager manager = Components<RelicGameManager>(scene).Single();
        PlayerCharacter player = manager.player;
        RelicExit exit = Components<RelicExit>(scene).Single();
        if (player == null || player.GetComponent<CapsuleCollider2D>() == null)
            throw new InvalidOperationException("Scene needs its existing Ellen and capsule: " + scene.path);
        foreach (TilemapCollider2D tilemap in Components<TilemapCollider2D>(scene)) tilemap.ProcessTilemapChanges();
        Physics2D.SyncTransforms();

        // Compute all safe positions before replacing this builder's owned objects.
        float startX = player.transform.position.x;
        Vector2 startFeet = SafeFeet(player, new Vector2(startX, player.transform.position.y), null);
        Collider2D exitCollider = exit.GetComponent<Collider2D>();
        // Return on the approach side so the player can walk back into the level
        // without crossing its already-unlocked forward exit again.
        Vector2 endFeet = SafeFeet(player, new Vector2(exit.transform.position.x - 3.5f,
            exitCollider.bounds.min.y), exitCollider);
        Vector2 returnFeet = default;
        if (index > 0)
            returnFeet = SafeFeet(player, new Vector2(index == 1 ? -2.5f : -3f, startFeet.y), null);

        foreach (GameObject previous in scene.GetRootGameObjects().Where(root => root.name == RootName))
            UnityEngine.Object.DestroyImmediate(previous);
        Transform root = new GameObject(RootName).transform;
        SceneManager.MoveGameObjectToScene(root.gameObject, scene);
        Marker(root, "start", startFeet);
        Marker(root, "end", endFeet);

        if (index < ScenePaths.Length - 1)
        {
            manager.nextSceneName = System.IO.Path.GetFileNameWithoutExtension(ScenePaths[index + 1]);
            TextMesh oldExitLabel = Components<TextMesh>(scene).FirstOrDefault(label =>
                Mathf.Abs(label.transform.position.x - exit.transform.position.x) < 2f &&
                (label.text.Trim() == "EXIT   >" || label.text.Trim() == "FINAL EXIT  >" ||
                 label.text.Trim() == "LEVEL " + (index + 2) + "  >"));
            if (oldExitLabel != null) oldExitLabel.text = "LEVEL " + (index + 2) + "  >";
            else Label(root, "LEVEL " + (index + 2) + "  >", new Vector2(exit.transform.position.x,
                exitCollider.bounds.max.y + 1.1f), new Color(.98f, .83f, .4f), .34f);
        }

        if (index > 0)
        {
            // Make the new gate visible at the entry even in a narrow Game view.
            // Keep the authored camera transform and all other follow settings intact.
            foreach (RelicCamera camera in Components<RelicCamera>(scene))
                if (camera.target == player.transform)
                    camera.minX = Mathf.Min(camera.minX, startX + camera.lookAhead);
            ReturnPortal(scene, root, manager, teleporter, material, returnFeet, index);
            GameObject hint = new GameObject("Return portal instructions", typeof(BoxCollider2D), typeof(RelicHintZone));
            hint.transform.SetParent(root, false);
            hint.transform.position = startFeet + Vector2.up;
            BoxCollider2D hintCollider = hint.GetComponent<BoxCollider2D>();
            hintCollider.isTrigger = true;
            hintCollider.size = new Vector2(4f, 3f);
            RelicHintZone zone = hint.GetComponent<RelicHintZone>();
            zone.manager = manager;
            zone.message = "The violet gate on the left returns to LEVEL " + index +
                " near its exit. Collected relics stay collected when you travel between levels.";
            zone.duration = 7f;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (RelicPickup pickup in Components<RelicPickup>(scene))
        {
            if (string.IsNullOrEmpty(pickup.pickupId))
                pickup.pickupId = scene.name + "::" + HierarchyPath(pickup.transform);
            if (!ids.Add(pickup.pickupId))
                throw new InvalidOperationException("Duplicate relic persistence id in " + scene.name + ": " + pickup.pickupId);
        }
        Debug.Log(scene.name + " campaign arrivals: start=" + startFeet.ToString("F3") +
            " end=" + endFeet.ToString("F3") + (index > 0 ? " return gate=" + returnFeet.ToString("F3") : "") +
            "; unique relic ids=" + ids.Count);
    }

    private static void ReturnPortal(Scene scene, Transform root, RelicGameManager manager,
        GameObject teleporter, Material material, Vector2 feet, int previousLevel)
    {
        GameObject gate = new GameObject("Return to level " + previousLevel + " - WALK IN", typeof(BoxCollider2D), typeof(RelicCampaignPortal));
        gate.transform.SetParent(root, false);
        gate.transform.position = feet - Vector2.up * .12f;
        BoxCollider2D trigger = gate.GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.55f, 3.1f);
        trigger.offset = new Vector2(0, 1.4f);
        RelicCampaignPortal portal = gate.GetComponent<RelicCampaignPortal>();
        portal.manager = manager;
        portal.destinationScene = System.IO.Path.GetFileNameWithoutExtension(ScenePaths[previousLevel - 1]);
        portal.arrivalId = "end";

        GameObject art = (GameObject)PrefabUtility.InstantiatePrefab(teleporter, scene);
        art.name = "Violet return portal - visual only";
        art.transform.SetParent(gate.transform, false);
        art.transform.localPosition = new Vector3(0, -.15f, 0);
        art.transform.localScale = new Vector3(.75f, .8f, 1);
        foreach (TransitionPoint transition in art.GetComponentsInChildren<TransitionPoint>(true))
            UnityEngine.Object.DestroyImmediate(transition);
        foreach (Collider2D collider in art.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
        foreach (SpriteRenderer renderer in art.GetComponentsInChildren<SpriteRenderer>(true))
        {
            renderer.sharedMaterial = material;
            renderer.color = ReturnColor;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 0;
        }
        Label(root, "<  LEVEL " + previousLevel + "\nRETURN PORTAL", feet + Vector2.up * 4.15f, ReturnColor, .34f);
    }

    private static void Marker(Transform root, string id, Vector2 feet)
    {
        var marker = new GameObject("Arrival " + id + " - safe player feet", typeof(RelicCampaignArrival));
        marker.transform.SetParent(root, false);
        marker.transform.position = feet;
        RelicCampaignArrival arrival = marker.GetComponent<RelicCampaignArrival>();
        arrival.arrivalId = id;
        arrival.feet = marker.transform;
    }

    private static Vector2 SafeFeet(PlayerCharacter player, Vector2 preferred, Collider2D exit)
    {
        CapsuleCollider2D capsule = player.GetComponent<CapsuleCollider2D>();
        Vector3 scale = capsule.transform.lossyScale;
        Vector2 size = new Vector2(Mathf.Abs(capsule.size.x * scale.x), Mathf.Abs(capsule.size.y * scale.y)) + Vector2.one * .07f;
        Vector2 centerOffset = (Vector2)capsule.transform.TransformPoint(capsule.offset) - (Vector2)player.transform.position;
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(Physics2D.AllLayers);
        var overlaps = new Collider2D[64];
        foreach (float offset in new[] { 0f, -.5f, .5f, -1f, 1f })
        {
            float x = preferred.x + offset;
            if (exit != null && Mathf.Abs(x + centerOffset.x - exit.bounds.center.x) <
                exit.bounds.extents.x + size.x * .5f + .5f) continue;
            RaycastHit2D[] hits = Physics2D.RaycastAll(new Vector2(x, preferred.y + 4f), Vector2.down, 10f);
            foreach (RaycastHit2D hit in hits.OrderBy(hit => Mathf.Abs(hit.point.y - preferred.y)))
            {
                if (hit.collider == null || hit.collider.isTrigger || hit.normal.y < .8f ||
                    hit.collider.GetComponent<TilemapCollider2D>() == null) continue;
                Vector2 position = new Vector2(x, hit.point.y + .12f);
                int count = Physics2D.OverlapCapsule(position + centerOffset, size, capsule.direction,
                    capsule.transform.eulerAngles.z, filter, overlaps);
                if (count >= overlaps.Length) continue;
                bool clear = true;
                for (int i = 0; i < count; i++)
                    if (overlaps[i] != null && !overlaps[i].transform.IsChildOf(player.transform)) { clear = false; break; }
                if (clear) return position;
            }
        }
        throw new InvalidOperationException("No safe campaign arrival near " + preferred.ToString("F3") +
            " in " + player.gameObject.scene.name + ". Existing terrain was not changed.");
    }

    private static IEnumerable<T> Components<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    }

    private static string HierarchyPath(Transform transform)
    {
        string path = transform.name + "[" + transform.GetSiblingIndex() + "]";
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "[" + transform.GetSiblingIndex() + "]/" + path;
        }
        return path;
    }

    private static void Label(Transform root, string text, Vector2 position, Color color, float size)
    {
        GameObject label = new GameObject(text.Replace('\n', ' '), typeof(TextMesh));
        label.transform.SetParent(root, false);
        label.transform.position = new Vector3(position.x, position.y, -1f);
        TextMesh mesh = label.GetComponent<TextMesh>();
        mesh.text = text;
        mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        mesh.fontSize = 50;
        mesh.characterSize = size / 5f;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.color = color;
        MeshRenderer renderer = label.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = mesh.font.material;
        renderer.sortingLayerName = "Front";
        renderer.sortingOrder = 10;
    }
}

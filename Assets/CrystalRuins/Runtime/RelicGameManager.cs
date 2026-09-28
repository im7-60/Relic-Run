using System.Collections;
using Gamekit2D;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CrystalRuins
{
    /// <summary>Owns this level's rules and menus while Ellen retains the 2D Game Kit controller.</summary>
    [DefaultExecutionOrder(-100)]
    public class RelicGameManager : MonoBehaviour
    {
        public enum GameState { Menu, Playing, Paused, Won, Lost, Transitioning }

        public PlayerCharacter player;
        public int levelNumber = 1;
        public string levelTitle = "THE VERDANT RUINS";
        public string nextSceneName = "";
        public int requiredRelics = 6;
        public int totalRelics = 8;
        public AudioClip pickupSound;
        public AudioClip winSound;
        public AudioClip loseSound;
        [Tooltip("Optional barrier that disappears when enough relics have been collected.")]
        public GameObject gateVisual;

        public static RelicGameManager Instance { get; private set; }
        public GameState State { get; private set; } = GameState.Menu;
        public int CollectedRelics { get; private set; }
        public int Score { get; private set; }
        public float ElapsedTime { get; private set; }
        public bool IsPlaying => State == GameState.Playing;
        public bool ExitUnlocked => CollectedRelics >= requiredRelics;

        private static bool startAfterReload;
        private bool reloading;
        private Damageable health;
        private AudioSource audioSource;
        private Font font;
        private GameObject modal;
        private GameObject hud;
        private Text scoreText;
        private Text objectiveText;
        private Text timerText;
        private Text ammoText;
        private RelicTeleportAbility teleportAbility;
        private Text hintText;
        private Text modalEyebrow;
        private Text modalTitle;
        private Text modalBody;
        private Text modalFootnote;
        private Text primaryLabel;
        private Text secondaryLabel;
        private Button primaryButton;
        private Button secondaryButton;
        private Image[] healthCells;
        private float hintUntil;

        private static readonly Color Ink = new Color(0.035f, 0.11f, 0.13f, 1f);
        private static readonly Color Cream = new Color(0.96f, 0.95f, 0.87f, 1f);
        private static readonly Color Muted = new Color(0.60f, 0.76f, 0.72f, 1f);
        private static readonly Color Gold = new Color(1f, 0.76f, 0.34f, 1f);
        private static readonly Color Teal = new Color(0.31f, 0.84f, 0.72f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
            startAfterReload = false;
        }

        private void Awake()
        {
            Instance = this;
            if (player == null) player = FindAnyObjectByType<PlayerCharacter>();
            if (player != null)
            {
                var inventory = player.GetComponent<InventoryController>();
                if (inventory != null) inventory.SetDataSettings("RelicRun.PlayerInventory", DataSettings.PersistenceType.DoNotPersist);
                health = player.damageable != null ? player.damageable : player.GetComponent<Damageable>();
                if (health != null)
                {
                    health.startingHealth = 5;
                    health.disableOnDeath = false;
                    health.SetDataSettings("RelicRun.PlayerHealth", DataSettings.PersistenceType.DoNotPersist);
                    // The stock persistent OnDie listener respawns Ellen. This level has a lose screen instead.
                    health.OnDie = new Damageable.DamageEvent();
                    health.OnDie.AddListener(OnPlayerDied);
                    health.OnHealthSet.AddListener(OnHealthChanged);
                }
            }
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerPause = true;
        }

        private void Start()
        {
            totalRelics = Mathf.Max(1, totalRelics);
            requiredRelics = Mathf.Clamp(requiredRelics, 1, totalRelics);
            RelicCampaignState.LevelSnapshot snapshot = RelicCampaignState.GetSnapshot(gameObject.scene.name);
            if (snapshot != null)
            {
                CollectedRelics = Mathf.Clamp(snapshot.CollectedRelics, 0, totalRelics);
                Score = snapshot.Score;
                ElapsedTime = snapshot.ElapsedTime;
            }
            if (health != null) health.SetHealth(snapshot != null ? snapshot.Health : 5);
            if (gateVisual != null && ExitUnlocked) gateVisual.SetActive(false);
            if (Gamekit2D.PlayerInput.Instance != null)
            {
                Gamekit2D.PlayerInput.Instance.SetDataSettings("RelicRun.PlayerInput", DataSettings.PersistenceType.DoNotPersist);
                Gamekit2D.PlayerInput.Instance.Pause.Disable();
                Gamekit2D.PlayerInput.Instance.EnableMeleeAttacking();
                Gamekit2D.PlayerInput.Instance.EnableRangedAttacking();
            }
            teleportAbility = player != null ? player.GetComponent<RelicTeleportAbility>() : null;
            BuildUI();
            RefreshHUD();
            if (RelicCampaignState.TryConsumeArrival(gameObject.scene.name, out string arrivalId))
            {
                startAfterReload = false;
                State = GameState.Transitioning;
                modal.SetActive(false);
                hud.SetActive(true);
                Freeze();
                StartCoroutine(CompleteArrival(arrivalId));
            }
            else if (startAfterReload)
            {
                startAfterReload = false;
                StartGame();
            }
            else
            {
                State = GameState.Menu;
                Freeze();
                ShowModal("LEVEL " + levelNumber.ToString("00") + "  /  " + levelTitle, "RELIC RUN",
                    "Recover " + requiredRelics + " of the " + totalRelics + " ancient relics, then reach the glowing exit.\n\n" +
                    (levelNumber == 1
                        ? "Cross broken paths. Ride the moving platform.\nWalk onto the pressure switch to open the stone gate.\nEnter the portal to continue to level 2."
                        : levelNumber == 2
                            ? "These gaps are too wide to jump. Stand near each edge.\nB: select teleport ammo. O: launch.\nL: teleport when the cyan shot is over the next island.\nThe exit leads to the Folded Archive."
                            : "Portals fold distant rooms together. Step through a linked portal.\nSend a teleport shot through ORB ONLY windows, then press L.\nUse E at the route console. Recover all four archive relics."),
                    "A / D  MOVE     SPACE  JUMP     K  MELEE     O  SHOOT\nB  SWITCH AMMO     L  TELEPORT     ESC  PAUSE\n\nTeleport shots fade after 18 units / 1.5 seconds. Falling ends the attempt.",
                    "PLAY", "");
            }
        }

        private void Update()
        {
            // Disable the kit's separate UIMenus route; Escape is handled by this level only.
            if (Gamekit2D.PlayerInput.Instance != null) Gamekit2D.PlayerInput.Instance.Pause.Disable();
            var keyboard = Keyboard.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool confirm = keyboard != null && keyboard.enterKey.wasPressedThisFrame;
            if (escape)
            {
                if (State == GameState.Playing) PauseGame();
                else if (State == GameState.Paused) ResumeGame();
            }
            if (confirm && State == GameState.Menu) StartGame();
            else if (confirm && (State == GameState.Won || State == GameState.Lost)) RestartGame();
            if (IsPlaying)
            {
                ElapsedTime += Time.deltaTime;
                if (timerText != null) timerText.text = FormatTime(ElapsedTime);
                if (player != null && player.transform.position.y < -12f) Lose();
            }
            if (ammoText != null && teleportAbility != null) ammoText.text = teleportAbility.StatusText;
            if (hintText != null && Time.unscaledTime >= hintUntil) hintText.text = "";
        }

        public void StartGame()
        {
            if (State != GameState.Menu) return;
            State = GameState.Playing;
            modal.SetActive(false);
            hud.SetActive(true);
            Unfreeze();
            ShowHint(levelNumber == 1 ? "Collect " + requiredRelics + " relics. Walk into the portal to reach level 2."
                : levelNumber == 2 ? "B: select TELEPORT. O: launch. Wait until the shot reaches the next island, then press L."
                : "Portals carry you and your teleport shots. E changes the route at the console. Recover all 4 archive relics.", 7f);
        }

        public void PauseGame()
        {
            if (!IsPlaying) return;
            State = GameState.Paused;
            Freeze();
            ShowModal("EXPEDITION PAUSED", "TAKE A BREATH",
                "Your progress is safe while this menu is open.\n\n" + CollectedRelics + " / " + totalRelics + " relics recovered  |  " + Score + " points",
                "A / D  MOVE     SPACE  JUMP     K  MELEE     O  SHOOT\nB  SWITCH AMMO     L  TELEPORT     ESC  RESUME", "RESUME", "RESTART LEVEL");
        }

        public void ResumeGame()
        {
            if (State != GameState.Paused) return;
            State = GameState.Playing;
            modal.SetActive(false);
            Unfreeze();
        }

        public bool AddRelic(string pickupId)
        {
            if (!IsPlaying || CollectedRelics >= totalRelics ||
                !RelicCampaignState.TryCollect(gameObject.scene.name, pickupId)) return false;
            CollectedRelics++;
            Score += 100;
            SaveProgress();
            Play(pickupSound, 0.65f);
            if (CollectedRelics == requiredRelics)
            {
                if (gateVisual != null) gateVisual.SetActive(false);
                ShowHint(totalRelics > requiredRelics ? "EXIT UNLOCKED! Reach the glowing portal. Extra relics earn bonus points." : "ALL ARCHIVE RELICS RECOVERED! Set the route to EXIT and enter the final portal.", 5f);
            }
            else ShowHint("RELIC RECOVERED  +100", 1.8f);
            RefreshHUD();
            return true;
        }

        public void Win()
        {
            if (!IsPlaying || !ExitUnlocked) return;
            if (!string.IsNullOrEmpty(nextSceneName))
            {
                TravelToScene(nextSceneName, "start");
                return;
            }
            State = GameState.Won;
            if (teleportAbility != null) teleportAbility.ClearProjectile();
            int healthBonus = Mathf.Max(0, health != null ? health.CurrentHealth : 0) * 50;
            Score += 500 + healthBonus;
            RefreshHUD();
            Freeze();
            Play(winSound, 0.8f);
            ShowModal("EXPEDITION COMPLETE", "RUINS RESTORED",
                "All " + levelNumber + " levels cleared. The expedition is complete!\n\n" +
                CollectedRelics + " / " + totalRelics + " RELICS     " + FormatTime(ElapsedTime) + " ELAPSED\n\n" +
                "LEVEL " + levelNumber + " SCORE  " + Score.ToString("N0"),
                "This level: Relics  " + (CollectedRelics * 100) + "   +   Exit  500   +   Health bonus  " + healthBonus +
                "\n" + (CollectedRelics == totalRelics ? "Perfect collection. Every relic is safe." : "Try another expedition to recover all " + totalRelics + " relics."),
                "RESTART", "");
        }

        public void Lose()
        {
            if (!IsPlaying) return;
            State = GameState.Lost;
            if (teleportAbility != null) teleportAbility.ClearProjectile();
            if (player != null) player.StopAllCoroutines();
            Freeze();
            Play(loseSound, 0.7f);
            ShowModal("EXPEDITION ENDED", "THE RUINS REMAIN",
                "The ruins claimed this expedition. You can try again.\n\n" +
                CollectedRelics + " / " + totalRelics + " RELICS     " + FormatTime(ElapsedTime) + " ELAPSED\n\n" +
                "SCORE  " + Score.ToString("N0"),
                "B changes ammo. O launches a shot. L teleports to the active cyan shot.\nAim for clear ground before the shot fades; use normal ammo against enemies.",
                "RESTART", "");
        }

        public void RestartGame()
        {
            if (reloading) return;
            Scene scene = gameObject.scene;
            // A completed expedition starts over; retries erase only this level's progress.
            string restartScene = State == GameState.Won ? SceneUtility.GetScenePathByBuildIndex(0) : scene.path;
            if (!Application.CanStreamedLevelBeLoaded(restartScene))
            {
                ShowHint("The restart scene is not included in this build.", 5f);
                return;
            }
            reloading = true;
            if (State == GameState.Won) RelicCampaignState.ResetCampaign();
            else RelicCampaignState.ResetLevel(scene.name);
            startAfterReload = true;
            if (teleportAbility != null) teleportAbility.ClearProjectile();
            Time.timeScale = 1f;
            AudioListener.pause = false;
            PersistentDataManager.ClearPersisters();
            SceneManager.LoadScene(restartScene);
        }

        private void SaveProgress()
        {
            RelicCampaignState.Save(gameObject.scene.name, Score, ElapsedTime,
                health != null ? health.CurrentHealth : 5);
        }

        /// <summary>Changes scene without losing collected relics or this level's score and timer.</summary>
        public bool TravelToScene(string destinationScene, string arrivalId)
        {
            if (!IsPlaying || reloading) return false;
            if (string.IsNullOrEmpty(destinationScene) || string.IsNullOrEmpty(arrivalId) ||
                !Application.CanStreamedLevelBeLoaded(destinationScene))
            {
                ShowHint("That portal's destination is not included in this build.", 5f);
                return false;
            }
            SaveProgress();
            RelicCampaignState.PrepareArrival(destinationScene, arrivalId);
            State = GameState.Transitioning;
            reloading = true;
            if (teleportAbility != null) teleportAbility.ClearProjectile();
            startAfterReload = false;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            PersistentDataManager.ClearPersisters();
            try
            {
                SceneManager.LoadScene(destinationScene);
                return true;
            }
            catch (System.Exception exception)
            {
                RelicCampaignState.CancelArrival();
                reloading = false;
                State = GameState.Playing;
                Unfreeze();
                Debug.LogException(exception, this);
                ShowHint("The portal could not load its destination. Please try again.", 5f);
                return false;
            }
        }

        private IEnumerator CompleteArrival(string arrivalId)
        {
            // Ellen caches her authored spawn in Start; place her only after all scene Starts.
            yield return null;
            RelicCampaignArrival arrival = null;
            foreach (RelicCampaignArrival candidate in FindObjectsByType<RelicCampaignArrival>(FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene == gameObject.scene && candidate.arrivalId == arrivalId)
                {
                    arrival = candidate;
                    break;
                }
            }
            bool placed = false;
            if (arrival != null && player != null)
            {
                Vector2 destination = arrival.feet != null ? arrival.feet.position : arrival.transform.position;
                CharacterController2D controller = player.GetComponent<CharacterController2D>();
                if (controller != null && teleportAbility != null && teleportAbility.IsDestinationClear(destination))
                {
                    player.SetMoveVector(Vector2.zero);
                    Rigidbody2D body = controller.Rigidbody2D;
                    if (body != null)
                    {
                        body.linearVelocity = Vector2.zero;
                        body.angularVelocity = 0f;
                    }
                    controller.Teleport(destination);
                    if (body != null) body.position = destination;
                    player.transform.position = new Vector3(destination.x, destination.y, player.transform.position.z);
                    Physics2D.SyncTransforms();
                    controller.CheckCapsuleEndCollisions();
                    controller.CheckCapsuleEndCollisions(false);
                    Animator animator = player.GetComponent<Animator>();
                    if (animator != null)
                    {
                        animator.SetBool("Grounded", controller.IsGrounded);
                        animator.SetFloat("HorizontalSpeed", 0f);
                        animator.SetFloat("VerticalSpeed", 0f);
                    }
                    foreach (RelicCamera camera in FindObjectsByType<RelicCamera>(FindObjectsSortMode.None))
                        if (camera.target == player.transform) camera.SnapToTarget();
                    placed = true;
                }
            }
            // Give a normal, time-limited grace period while the player gets their bearings.
            if (placed && health != null) health.EnableInvulnerability();
            State = GameState.Playing;
            Unfreeze();
            RefreshHUD();
            if (placed) ShowHint("LEVEL " + levelNumber + "  /  " + levelTitle + " — progress restored. Brief arrival protection.", 4f);
            else
            {
                Debug.LogWarning("Campaign arrival '" + arrivalId + "' is missing or blocked in " + gameObject.scene.name + ". Using the authored start.", this);
                ShowHint("Portal landing unavailable. You have returned to this level's safe start.", 5f);
            }
        }

        public void ShowHint(string message, float duration = 3f)
        {
            if (hintText == null) return;
            hintText.text = message;
            hintUntil = Time.unscaledTime + duration;
        }

        public bool IsPlayer(Collider2D other)
        {
            // Pooled bullets are parented under Ellen, so ancestry is not body identity.
            return other != null && player != null && !other.isTrigger
                && other == player.GetComponent<CapsuleCollider2D>();
        }

        private void OnPlayerDied(Damager damager, Damageable damageable) { Lose(); }
        private void OnHealthChanged(Damageable damageable) { RefreshHUD(); }

        private void Freeze()
        {
            if (Gamekit2D.PlayerInput.Instance != null) Gamekit2D.PlayerInput.Instance.ReleaseControl(true);
            Time.timeScale = 0f;
        }

        private void Unfreeze()
        {
            Time.timeScale = 1f;
            if (Gamekit2D.PlayerInput.Instance != null)
            {
                Gamekit2D.PlayerInput.Instance.GainControl();
                Gamekit2D.PlayerInput.Instance.Pause.Disable();
            }
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void RefreshHUD()
        {
            if (scoreText == null) return;
            scoreText.text = "SCORE  " + Score.ToString("0000");
            if (timerText != null) timerText.text = FormatTime(ElapsedTime);
            objectiveText.text = "RELICS  " + CollectedRelics + " / " + totalRelics +
                (ExitUnlocked ? "    EXIT OPEN" : "    " + requiredRelics + " TO OPEN EXIT");
            objectiveText.color = ExitUnlocked ? Gold : Cream;
            for (int i = 0; i < healthCells.Length; i++)
                healthCells[i].color = health != null && i < health.CurrentHealth ? Teal : new Color(0.17f, 0.26f, 0.27f, 1f);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip != null) audioSource.PlayOneShot(clip, volume);
        }

        private string FormatTime(float seconds)
        {
            int whole = Mathf.FloorToInt(seconds);
            return (whole / 60).ToString("00") + ":" + (whole % 60).ToString("00");
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.OnHealthSet.RemoveListener(OnHealthChanged);
                health.OnDie.RemoveListener(OnPlayerDied);
            }
            if (Instance == this) Instance = null;
            Time.timeScale = 1f;
        }

        private void BuildUI()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("Relic Run Interface", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("Relic Run EventSystem", typeof(EventSystem));
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }
            foreach (BaseInputModule oldModule in eventSystem.GetComponents<BaseInputModule>())
                if (!(oldModule is InputSystemUIInputModule)) oldModule.enabled = false;
            var inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null) inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            if (inputModule.actionsAsset == null) inputModule.AssignDefaultActions();
            inputModule.enabled = true;

            hud = Panel("HUD", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.clear).gameObject;
            Image header = Panel("HUD Header", hud.transform, new Vector2(0, 1), Vector2.one, Vector2.zero, new Vector2(0, 116), new Color(Ink.r, Ink.g, Ink.b, 0.94f));
            header.rectTransform.pivot = new Vector2(0.5f, 1);
            Label("RELIC RUN", header.transform, new Vector2(35, -28), new Vector2(280, 38), 31, Gold, TextAnchor.MiddleLeft);
            Label("LEVEL " + levelNumber + " / " + levelTitle, header.transform, new Vector2(35, -71), new Vector2(350, 30), 14, Muted, TextAnchor.MiddleLeft);
            objectiveText = Label("", header.transform, new Vector2(410, -30), new Vector2(680, 45), 25, Cream, TextAnchor.MiddleLeft);
            Label("VITALITY", header.transform, new Vector2(1140, -18), new Vector2(200, 25), 15, Muted, TextAnchor.MiddleLeft);
            healthCells = new Image[5];
            for (int i = 0; i < healthCells.Length; i++)
                healthCells[i] = Panel("Health " + (i + 1), header.transform, Vector2.up, Vector2.up, new Vector2(1140 + i * 37, -56), new Vector2(29, 24), Teal);
            scoreText = Label("", header.transform, new Vector2(1470, -25), new Vector2(360, 36), 29, Cream, TextAnchor.MiddleRight);
            timerText = Label("00:00", header.transform, new Vector2(1500, -70), new Vector2(330, 23), 18, Muted, TextAnchor.MiddleRight);
            hintText = Label("", hud.transform, new Vector2(0, -154), new Vector2(1540, 64), 25, Cream, TextAnchor.MiddleCenter);
            var hintRect = hintText.rectTransform;
            hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 1f);
            hintRect.pivot = new Vector2(0.5f, 0.5f);
            hintRect.anchoredPosition = new Vector2(0, -158);
            hintText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);
            Text footer = Label("A / D  MOVE     SPACE  JUMP     K  MELEE     O  SHOOT     B  AMMO     L  TELEPORT     ESC  PAUSE", hud.transform, Vector2.zero, new Vector2(1700, 38), 18, Cream, TextAnchor.MiddleCenter);
            footer.rectTransform.anchorMin = footer.rectTransform.anchorMax = new Vector2(0.5f, 0);
            footer.rectTransform.pivot = new Vector2(0.5f, 0);
            footer.rectTransform.anchoredPosition = new Vector2(0, 20);
            footer.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);

            ammoText = Label("", hud.transform, Vector2.zero, new Vector2(1500, 38), 22, Teal, TextAnchor.MiddleCenter);
            ammoText.rectTransform.anchorMin = ammoText.rectTransform.anchorMax = new Vector2(0.5f, 0);
            ammoText.rectTransform.pivot = new Vector2(0.5f, 0);
            ammoText.rectTransform.anchoredPosition = new Vector2(0, 65);
            ammoText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);

            modal = Panel("Menu Backdrop", canvasObject.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.015f, 0.045f, 0.055f, 0.82f)).gameObject;
            modal.GetComponent<Image>().raycastTarget = true;
            Image card = Panel("Menu Card", modal.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1130, 820), Ink);
            Panel("Gold Rule", card.transform, Vector2.up, Vector2.up, new Vector2(60, -46), new Vector2(84, 5), Gold);
            modalEyebrow = Label("", card.transform, new Vector2(60, -76), new Vector2(1000, 30), 19, Gold, TextAnchor.MiddleLeft);
            modalTitle = Label("", card.transform, new Vector2(54, -124), new Vector2(1020, 90), 67, Cream, TextAnchor.MiddleLeft);
            modalBody = Label("", card.transform, new Vector2(60, -246), new Vector2(1000, 230), 27, Cream, TextAnchor.UpperLeft);
            modalFootnote = Label("", card.transform, new Vector2(60, -502), new Vector2(1000, 116), 20, Muted, TextAnchor.UpperLeft);
            primaryButton = MakeButton("Primary", card.transform, new Vector2(60, -675), new Vector2(570, 78), Gold, Ink, out primaryLabel);
            primaryButton.onClick.AddListener(PrimaryAction);
            secondaryButton = MakeButton("Secondary", card.transform, new Vector2(670, -675), new Vector2(400, 78), new Color(0.10f, 0.23f, 0.25f), Cream, out secondaryLabel);
            secondaryButton.onClick.AddListener(RestartGame);
        }

        private void ShowModal(string eyebrow, string title, string body, string footnote, string primary, string secondary)
        {
            modalEyebrow.text = eyebrow;
            modalTitle.text = title;
            modalBody.text = body;
            modalFootnote.text = footnote;
            primaryLabel.text = primary;
            secondaryLabel.text = secondary;
            secondaryButton.gameObject.SetActive(!string.IsNullOrEmpty(secondary));
            modal.SetActive(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(primaryButton.gameObject);
        }

        private void PrimaryAction()
        {
            if (State == GameState.Menu) StartGame();
            else if (State == GameState.Paused) ResumeGame();
            else if (State == GameState.Won || State == GameState.Lost) RestartGame();
        }

        private Image Panel(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin == Vector2.up && anchorMax == Vector2.up ? Vector2.up : new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text Label(string value, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = Vector2.up;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = fontSize;
            text.color = color;
            text.text = value;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Button MakeButton(string name, Transform parent, Vector2 position, Vector2 size, Color background, Color foreground, out Text label)
        {
            Image image = Panel(name, parent, Vector2.up, Vector2.up, position, size, background);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.78f, 0.85f, 0.81f, 1f);
            colors.selectedColor = Color.white;
            button.colors = colors;
            label = Label("", image.transform, Vector2.zero, size, 25, foreground, TextAnchor.MiddleCenter);
            return button;
        }
    }
}

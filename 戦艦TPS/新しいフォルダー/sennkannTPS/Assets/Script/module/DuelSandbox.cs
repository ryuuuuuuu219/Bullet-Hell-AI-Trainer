using UnityEngine;
using UnityEngine.InputSystem;

namespace Script
{
    public enum DuelResult { Running, Victory, Defeat }

    // Local phase 08 test only. Does not read/write campaign or construction saves.
    [DefaultExecutionOrder(50)]
    public sealed partial class DuelSandbox : MonoBehaviour
    {
        public BattleshipInstance PlayerShip { get; private set; }
        public BattleshipInstance EnemyShip { get; private set; }
        public DuelResult Result { get; private set; }
        public float Elapsed { get; private set; }
        public int CompletionCount { get; private set; }
        public bool AutomaticPlayer { get; private set; }
        private GameObject round, bullets;
        private Material material;
        private BattleshipSection[] mounts;
        private Camera view;
        private double playerHP, enemyHP;
        private const float PanelWidth = 430;

        private void Start() { if (round == null) Restart(false); }

        public void Restart(bool automaticPlayer)
        {
            if (round != null) { round.SetActive(false); Destroy(round); }
            if (material == null) material = new Material(Shader.Find("Battleship/TilingTest"));
            view = Camera.main;
            round = new GameObject("Duel round"); round.transform.SetParent(transform, false);
            bullets = new GameObject("Duel projectiles"); bullets.transform.SetParent(round.transform, false);
            PlayerShip = ShipBuilder.Build(PlayerDesign, material, new Vector2(-1200, 0), 0, true, !automaticPlayer);
            EnemyShip = ShipBuilder.BuildWithAI(EnemyDesign, material, new Vector2(1200, 0), 180, false,
                Steering(EnemyAI, PlayerShip.transform));
            PlayerShip.name = "Player test ship"; EnemyShip.name = "Enemy test ship";
            PlayerShip.transform.SetParent(round.transform, true); EnemyShip.transform.SetParent(round.transform, true);
            foreach (var ship in new[] { PlayerShip, EnemyShip })
            {
                ship.GetComponent<ShipFiringAI>().ProjectileRoot = bullets.transform;
                ship.GetComponent<ShipMovement>().halfExtent = 5000;
            }
            if (automaticPlayer) PlayerShip.gameObject.AddComponent<ShipSteeringAI>().Configure(
                Steering(PlayerAI, EnemyShip.transform));
            mounts = PlayerShip.GetComponentsInChildren<BattleshipSection>();
            foreach (var mount in mounts) mount.ManualMissileTarget = EnemyShip.GetComponent<WeaponTarget>();
            AutomaticPlayer = automaticPlayer;
            Result = DuelResult.Running; Elapsed = 0; CompletionCount = 0;
            playerHP = PlayerShip.CoreHealth; enemyHP = EnemyShip.CoreHealth;
            playerLimit = PlayerShip.CoreHealthLimit; enemyLimit = EnemyShip.CoreHealthLimit;
            editing = false; ApplyAmmo();
        }

        public static ShipBuildData TestDesign()
        {
            // Heavy AP can damage the lowest Basic armor; no special ammo required.
            return new ShipBuildData { seed = 0, referenceSections = 6,
                weaponCounts = new[] { 0, 4, 0, 0, 0, 0, 0 },
                armor = new[] { new ShipArmorSettings { count = 2 }, new ShipArmorSettings(), new ShipArmorSettings() } };
        }

        private void Update()
        {
            if (editing) return;
            EvaluateResult();
            if (Result != DuelResult.Running) return;
            Elapsed += Time.deltaTime;
            if (!AutomaticPlayer && PlayerShip != null)
            {
                var mouse = Mouse.current;
                Vector2? aim = null;
                if (mouse != null && view != null && mouse.leftButton.isPressed)
                {
                    Vector2 screen = mouse.position.ReadValue();
                    if (!HudRect.Contains(new Vector2(screen.x, Screen.height - screen.y)) &&
                        !AmmoRect.Contains(new Vector2(screen.x, Screen.height - screen.y)))
                        aim = (Vector2)view.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -view.transform.position.z));
                }
                foreach (var mount in mounts) if (mount != null) mount.TickManualWeapon(Time.deltaTime, aim, bullets.transform);
            }
        }

        public void EvaluateResult()
        {
            if (editing || round == null || Result != DuelResult.Running) return;
            playerHP = PlayerShip != null ? System.Math.Max(0, PlayerShip.CoreHealth) : 0;
            enemyHP = EnemyShip != null ? System.Math.Max(0, EnemyShip.CoreHealth) : 0;
            // Confirmed outcome priority: player sinking precedes target elimination.
            if (PlayerShip == null || !PlayerShip.IsAlive) Result = DuelResult.Defeat;
            else if (EnemyShip == null || !EnemyShip.IsAlive) Result = DuelResult.Victory;
            if (Result == DuelResult.Running) return;
            CompletionCount++;
            round.SetActive(false); // Stops movement, status damage and every in-flight shot together.
        }

        private void LateUpdate()
        {
            if (view == null || PlayerShip == null || EnemyShip == null || Result != DuelResult.Running) return;
            Vector2 middle = (PlayerShip.CorePosition + EnemyShip.CorePosition) * .5f;
            Vector2 delta = PlayerShip.CorePosition - EnemyShip.CorePosition;
            view.transform.position = new Vector3(middle.x, middle.y, -10);
            view.orthographicSize = Mathf.Max(1800, Mathf.Abs(delta.y) * .7f + 700,
                (Mathf.Abs(delta.x) * .7f + 700) / Mathf.Max(.1f, view.aspect));
        }

        private Rect HudRect => new Rect(10, 10, Mathf.Min(PanelWidth, Screen.width - 20), 250);
        private void OnGUI()
        {
            if (editing) { DrawSettings(); return; }
            GUILayout.BeginArea(HudRect, GUI.skin.box);
            GUILayout.Label("1対1戦闘試験 / 工程08");
            GUILayout.Label($"自艦コアHP {playerHP:F0} / {playerLimit:F0}　敵艦 {enemyHP:F0} / {enemyLimit:F0}");
            GUILayout.Label($"{Elapsed:F1} 秒　" + (Result == DuelResult.Running ? "戦闘中" : Result == DuelResult.Victory ? "勝利" : "敗北"));
            GUILayout.Label(AutomaticPlayer ? $"観戦中：自艦{PlayerAI} / 敵艦{EnemyAI}" : "W/S：速度段階　A/D：旋回　Space：停止\n左クリック長押し：マウス位置へ射撃");
            GUI.enabled = !editing;
            if (GUILayout.Button("手動操艦で再試行")) Restart(false);
            if (GUILayout.Button("双方AIで観戦・再試行")) Restart(true);
            if (GUILayout.Button("建造・AI設定")) OpenSettings();
            GUI.enabled = true;
            GUILayout.EndArea();
            LabelShip(PlayerShip, "自艦"); LabelShip(EnemyShip, "敵艦");
            if (!editing) DrawAmmo();
        }

        private void LabelShip(BattleshipInstance ship, string label)
        {
            if (ship == null || view == null || Result != DuelResult.Running) return;
            Vector3 screen = view.WorldToScreenPoint(ship.CorePosition);
            GUI.Label(new Rect(screen.x - 30, Screen.height - screen.y - 55, 80, 25), label);
        }

        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}

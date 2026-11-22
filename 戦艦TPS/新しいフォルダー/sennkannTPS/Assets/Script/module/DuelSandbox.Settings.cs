using System;
using System.Linq;
using UnityEngine;

namespace Script
{
    public sealed partial class DuelSandbox
    {
        public ShipBuildData PlayerDesign { get; private set; } = TestDesign();
        public ShipBuildData EnemyDesign { get; private set; } = TestDesign();
        public AIType PlayerAI { get; private set; } = AIType.BS;
        public AIType EnemyAI { get; private set; } = AIType.BS;
        public BattleshipProjectile.Affect ShellAmmo { get; private set; } = BattleshipProjectile.Affect.AP;
        public BattleshipProjectile.Affect MissileAmmo { get; private set; } = BattleshipProjectile.Affect.HE;
        private double playerLimit, enemyLimit;
        private bool editing;
        private ShipBuildData[] drafts;
        private AIType[] draftAI;
        private int side, ammoMenu = -1, beamIndex;
        private Vector2 settingsScroll, ammoScroll;
        private string settingError;
        private static readonly string[] AINames = { "C1", "C2", "TOr", "Obs", "DF（護衛対象なし）", "AC", "BS" };
        private static readonly string[] ArmorNames = { "Basic", "ERA", "AEM" };
        private static readonly string[] AmmoNames = { "AP", "APC", "APCR", "HE", "HEAT", "HESH" };
        private static readonly string[] BeamNames = { "F.Beam", "Plasma", "E.Beam", "N.Beam" };
        private Rect AmmoRect => new Rect(Screen.width * .8f, Screen.height * .6f, Screen.width * .2f, Screen.height * .4f);

        private static ShipSteeringSettings Steering(AIType type, Transform target)
            => new ShipSteeringSettings { major = type, attackTarget = target,
                orbitCenterPosition = (Vector2)target.position + Vector2.up * 5000 };

        public bool Configure(ShipBuildData player, ShipBuildData enemy, AIType playerAI, AIType enemyAI, out string error)
        {
            error = null;
            if (!Enum.IsDefined(typeof(AIType), playerAI) || !Enum.IsDefined(typeof(AIType), enemyAI) ||
                playerAI == AIType.DF || enemyAI == AIType.DF)
            { error = "1対1試験には護衛する別の味方艦がないためDFは指定できません。"; return false; }
            if (player == null || enemy == null) { error = "建造データがありません。"; return false; }
            if (!player.TryCreateConstructionSnapshot(out var p, out error) ||
                !enemy.TryCreateConstructionSnapshot(out var e, out error)) return false;
            PlayerDesign = p; EnemyDesign = e; PlayerAI = playerAI; EnemyAI = enemyAI;
            return true;
        }

        public void OpenSettings()
        {
            drafts = new[] { PlayerDesign.Snapshot(), EnemyDesign.Snapshot() };
            draftAI = new[] { PlayerAI, EnemyAI }; settingError = null;
            editing = true; ammoMenu = -1;
            if (round != null) round.SetActive(false);
        }

        private void CloseSettings()
        {
            editing = false;
            if (round != null && Result == DuelResult.Running) round.SetActive(true);
        }

        private void DrawSettings()
        {
            var rect = new Rect(10, 10, Mathf.Min(650, Screen.width - 20), Screen.height - 20);
            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("建造・AI設定（編集中は戦闘停止／適用で再開始）");
            side = GUILayout.Toolbar(side, new[] { "自艦", "敵艦" });
            settingsScroll = GUILayout.BeginScrollView(settingsScroll);
            var data = drafts[side];
            GUILayout.Label(side == 0 ? "操艦AI（双方AIで観戦時に使用）" : "操艦AI");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < AINames.Length; i++)
            {
                GUI.enabled = i != (int)AIType.DF;
                if (GUILayout.Toggle((int)draftAI[side] == i, AINames[i], GUI.skin.button)) draftAI[side] = (AIType)i;
            }
            GUI.enabled = true; GUILayout.EndHorizontal();
            GUILayout.Label("DFは別の味方艦が必要。TOr半径3000、Obs半径2000／中心は敵初期位置＋(0,5000)。");
            data.coreHPSettingID = IntSlider("コアHP", data.coreHPSettingID, 10, ShipBuildData.CoreHP[data.coreHPSettingID].ToString("F0"));
            for (int i = 0; i < 3; i++)
            {
                var armor = data.armor[i];
                GUILayout.Label(ArmorNames[i] + "装甲");
                armor.count = IntSlider("区画数", armor.count, 60);
                armor.hpSettingID = IntSlider("区画HP", armor.hpSettingID, 4, ShipBuildData.ArmorHP[armor.hpSettingID].ToString("F0"));
                armor.defenseThicknessSettingID = IntSlider("装甲厚", armor.defenseThicknessSettingID, 4, ShipBuildData.Thickness[armor.defenseThicknessSettingID].ToString("F0"));
            }
            GUILayout.Label("兵装マウント数（試験用MVP4種）");
            for (int i = 0; i < 4; i++) data.weaponCounts[i] = IntSlider(MvpWeaponCatalog.Get(i).Name, data.weaponCounts[i], 128);
            data.referenceSections = data.weaponCounts.Sum() + data.armor.Sum(a => a.count);
            GUILayout.Label($"目安区画数：{data.referenceSections} / 128　総装甲：{data.armor.Sum(a => a.count)} / 60　砲塔HP：{data.TurretHP:F0}");
            GUILayout.Label("シード0。砲方式は既存の装薬砲。レールガンの性能・発射条件は未接続。");
            GUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(settingError)) GUILayout.Label(settingError);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("適用して手動戦闘")) ApplySettings(false);
            if (GUILayout.Button("適用して双方AI戦闘")) ApplySettings(true);
            if (GUILayout.Button("キャンセル")) CloseSettings();
            GUILayout.EndHorizontal(); GUILayout.EndArea();
        }

        private void ApplySettings(bool automatic)
        {
            if (Configure(drafts[0], drafts[1], draftAI[0], draftAI[1], out settingError)) Restart(automatic);
        }

        private static int IntSlider(string label, int value, int max, string display = null)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(100));
            if (GUILayout.Button("−", GUILayout.Width(28))) value--;
            value = Mathf.RoundToInt(GUILayout.HorizontalSlider(Mathf.Clamp(value, 0, max), 0, max));
            if (GUILayout.Button("＋", GUILayout.Width(28))) value++;
            value = Mathf.Clamp(value, 0, max);
            GUILayout.Label(display == null ? value.ToString() : $"{display} (ID {value})", GUILayout.Width(110));
            GUILayout.EndHorizontal(); return value;
        }

        public void SelectAmmo(bool missile, BattleshipProjectile.Affect value)
        {
            if ((int)value < 0 || (int)value >= AmmoNames.Length) throw new ArgumentOutOfRangeException(nameof(value));
            if (missile) MissileAmmo = value; else ShellAmmo = value;
            ApplyAmmo();
        }

        private void ApplyAmmo()
        {
            if (PlayerShip == null) return;
            foreach (var section in PlayerShip.GetComponentsInChildren<BattleshipSection>())
            {
                var weapon = section.ConstructionWeapon;
                if (weapon == null) continue;
                bool missile = weapon.Kind == BattleshipWeaponKind.AntiShipMissile;
                weapon.Affect = missile ? MissileAmmo : ShellAmmo;
                weapon.ExplosionRadius = ProjectileEffects.ExplosionRadius(weapon.Power, weapon.Affect);
            }
        }

        private void DrawAmmo()
        {
            GUILayout.BeginArea(AmmoRect, GUI.skin.box);
            ammoScroll = GUILayout.BeginScrollView(ammoScroll);
            GUILayout.Label("自艦 弾種設定（試験用）");
            AmmoDropdown(0, "実弾砲弾種：" + ShellAmmo, AmmoNames, false);
            AmmoDropdown(1, "ビーム兵器弾種：" + BeamNames[beamIndex], BeamNames, true);
            GUILayout.Label("ビーム：対応マウント未実装");
            AmmoDropdown(2, "ミサイル弾種：" + MissileAmmo, AmmoNames, false);
            GUILayout.Label("選択は次の発射から即時反映。\n飛翔中の弾は変更しません。\n追加選択肢・実弾の一括適用は試験用。保存なし。");
            GUILayout.EndScrollView(); GUILayout.EndArea();
        }

        private void AmmoDropdown(int id, string label, string[] options, bool previewOnly)
        {
            if (GUILayout.Button(label + " ↓")) ammoMenu = ammoMenu == id ? -1 : id;
            if (ammoMenu != id) return;
            for (int i = 0; i < options.Length; i++)
                if (GUILayout.Button(options[i]))
                {
                    if (previewOnly) beamIndex = i;
                    else SelectAmmo(id == 2, (BattleshipProjectile.Affect)i);
                    ammoMenu = -1;
                }
        }
    }
}

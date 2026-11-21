using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Script
{
    public sealed partial class TilingTestScene
    {
        private int coreHPSettingID;
        private readonly int[] armorHPSettings = new int[3];
        private readonly int[] armorDefenseSettings = new int[3];
        private string speedText = "250", turnText = "25", generatorSpeedText = "1", generatorTurnText = "1";
        private string radarText = "1", heatReductionText = "0", emReductionText = "0", heatCapText = "0", emCapText = "0";
        private BattleshipInstance movementTrial;
        private GameObject movementTrialProjectiles;
        private BattleshipSection[] movementTrialTurrets;
        private string generationInputError;
        private string savedBuildError;

        public bool TryGetBuild(out ShipBuildData data, out string error, bool requireCountMatch = true)
        {
            data = null;
            error = "兵装と装甲を手動指定にしてください。";
            if (!manualWeaponAllocation || !manualArmorAllocation) return false;
            error = "シード・目安区画数・試行上限・機関性能に有効な数値を入力してください。";
            if (!int.TryParse(seedText ?? seed.ToString(), out int buildSeed) ||
                !int.TryParse(targetText ?? targetSections.ToString(), out int count) ||
                !int.TryParse(attemptsText ?? maximumAttempts.ToString(), out int attempts) ||
                !float.TryParse(speedText, NumberStyles.Float, CultureInfo.InvariantCulture, out float speed) ||
                !float.TryParse(turnText, NumberStyles.Float, CultureInfo.InvariantCulture, out float turn) ||
                !float.TryParse(generatorSpeedText, NumberStyles.Float, CultureInfo.InvariantCulture, out float gv) ||
                !float.TryParse(generatorTurnText, NumberStyles.Float, CultureInfo.InvariantCulture, out float gr) ||
                !float.TryParse(radarText, NumberStyles.Float, CultureInfo.InvariantCulture, out float radar) ||
                !float.TryParse(heatReductionText, NumberStyles.Float, CultureInfo.InvariantCulture, out float heatReduction) ||
                !float.TryParse(emReductionText, NumberStyles.Float, CultureInfo.InvariantCulture, out float emReduction) ||
                !float.TryParse(heatCapText, NumberStyles.Float, CultureInfo.InvariantCulture, out float heatCap) ||
                !float.TryParse(emCapText, NumberStyles.Float, CultureInfo.InvariantCulture, out float emCap)) return false;
            data = new ShipBuildData { seed = buildSeed, referenceSections = count, maximumAttempts = attempts,
                triangleStart = triangleStart, startAngle = (int)(triangleStart ? triangleStartAngle : squareStartAngle),
                formEnds = formEnds, endSearchDepth = endSearchDepth, endSearchLimit = endSearchLimit,
                shipClass = (int)shipClass, coreHPSettingID = coreHPSettingID,
                baseSpeed = speed, baseTurnSpeed = turn, generatorSpeed = gv, generatorTurn = gr, sectionSize = sectionWorldSize,
                radarCorrection = radar, heatGainReduction = heatReduction, emGainReduction = emReduction, heatCapOffset = heatCap, emCapOffset = emCap };
            for (int i = 0; i < 4; i++) data.weaponCounts[i] = requestedWeaponCounts[i];
            for (int i = 0; i < 3; i++) data.armor[i] = new ShipArmorSettings {
                count = requestedArmorCounts[i], hpSettingID = armorHPSettings[i], defenseThicknessSettingID = armorDefenseSettings[i] };
            return data.TryCreateConstructionSnapshot(out data, out error, requireCountMatch);
        }

        private void ApplySavedBuild(ShipBuildData data)
        {
            seed = data.seed; targetSections = data.referenceSections; maximumAttempts = data.maximumAttempts;
            seedText = seed.ToString(CultureInfo.InvariantCulture);
            targetText = targetSections.ToString(CultureInfo.InvariantCulture);
            attemptsText = maximumAttempts.ToString(CultureInfo.InvariantCulture);
            triangleStart = data.triangleStart;
            if (triangleStart) triangleStartAngle = (StartAngle)data.startAngle; else squareStartAngle = (StartAngle)data.startAngle;
            formEnds = data.formEnds; endSearchDepth = data.endSearchDepth; endSearchLimit = data.endSearchLimit;
            shipClass = (ShipClass)data.shipClass; coreHPSettingID = data.coreHPSettingID; sectionWorldSize = data.sectionSize;
            manualWeaponAllocation = manualArmorAllocation = true;
            System.Array.Clear(requestedWeaponCounts, 0, requestedWeaponCounts.Length);
            for (int i = 0; i < 7; i++) if (data.weaponIDs[i] >= 0) requestedWeaponCounts[data.weaponIDs[i]] += data.weaponCounts[i];
            for (int i = 0; i < 3; i++) {
                requestedArmorCounts[i] = data.armor[i].count;
                armorHPSettings[i] = data.armor[i].hpSettingID;
                armorDefenseSettings[i] = data.armor[i].defenseThicknessSettingID;
            }
            speedText = data.baseSpeed.ToString("0.0000", CultureInfo.InvariantCulture);
            turnText = data.baseTurnSpeed.ToString("0.0000", CultureInfo.InvariantCulture);
            generatorSpeedText = data.generatorSpeed.ToString("0.0000", CultureInfo.InvariantCulture);
            generatorTurnText = data.generatorTurn.ToString("0.0000", CultureInfo.InvariantCulture);
            radarText = data.radarCorrection.ToString("0.0000", CultureInfo.InvariantCulture);
            heatReductionText = data.heatGainReduction.ToString("0.0000", CultureInfo.InvariantCulture);
            emReductionText = data.emGainReduction.ToString("0.0000", CultureInfo.InvariantCulture);
            heatCapText = data.heatCapOffset.ToString("0.0000", CultureInfo.InvariantCulture);
            emCapText = data.emCapOffset.ToString("0.0000", CultureInfo.InvariantCulture);
        }

        private void DrawBuildPresets()
        {
            int next = GUILayout.SelectionGrid(coreHPSettingID, ShipBuildData.CoreHP.Select(x => "HP " + x).ToArray(), 6);
            if (next != coreHPSettingID) { coreHPSettingID = next; if (HasFinished) DrawFrame(); }
            GUILayout.Label($"コア設定ID {coreHPSettingID} / 砲塔HP {ShipBuildData.CoreHP[coreHPSettingID] * .2:0}");
        }

        private void DrawArmorPresets(int index)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("HP設定", GUILayout.Width(65));
            int hp = GUILayout.SelectionGrid(armorHPSettings[index], new[] { "0:10", "1:50", "2:100", "3:150", "4:200" }, 5);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("防御設定", GUILayout.Width(65));
            int defense = GUILayout.SelectionGrid(armorDefenseSettings[index], new[] { "0", "1", "2", "3", "4" }, 5);
            GUILayout.Label($"迎撃 {ShipBuildData.Interception[defense]:0.00} / 厚 {ShipBuildData.Thickness[defense]}");
            GUILayout.EndHorizontal();
            if (hp != armorHPSettings[index] || defense != armorDefenseSettings[index]) {
                armorHPSettings[index] = hp; armorDefenseSettings[index] = defense;
                if (HasFinished) DrawFrame();
            }
        }

        private static string DrawNumericField(string label, string value)
        {
            GUILayout.BeginHorizontal(); GUILayout.Label(label, GUILayout.Width(330));
            value = GUILayout.TextField(value, GUILayout.Width(115)); GUILayout.EndHorizontal(); return value;
        }

        private void DrawEngineSettings()
        {
            speedText = DrawNumericField("スラスター補正前速力（U/s）", speedText);
            turnText = DrawNumericField("バウスラスター補正前旋回（deg/s）", turnText);
            generatorSpeedText = DrawNumericField("ジェネレーター速力倍率", generatorSpeedText);
            generatorTurnText = DrawNumericField("ジェネレーター旋回倍率", generatorTurnText);
            radarText = DrawNumericField("レーダー命中補正（1～100）", radarText);
            heatReductionText = DrawNumericField("耐火素材：熱量軽減率（0～0.99）", heatReductionText);
            emReductionText = DrawNumericField("ビームコーティング：EM軽減率（0～0.99）", emReductionText);
            heatCapText = DrawNumericField("火薬搭載量：熱量上限補正（−49～0）", heatCapText);
            emCapText = DrawNumericField("絶縁層：EM上限補正（−49～0）", emCapText);
            GUILayout.Label("発砲遅延装置：0 / 装填装置：0");
            GUILayout.Label("カタパルト：未設定 / 電磁バリアの個体性能：未設定");
            GUILayout.Label("負の上限補正は小数4桁以内で入力してください。");
            if (TryGetBuild(out var data, out string error, false))
                GUILayout.Label($"熱量軽減 {data.heatGainReduction * 100:0.00}% / EM軽減 {data.emGainReduction * 100:0.00}% / 蓄積上限 熱量 {150 + data.heatCapOffset:0.0000}P・EM {150 + data.emCapOffset:0.0000}P");
            else GUILayout.Label(error);
        }

        private void DrawBuildPerformance()
        {
            if (!string.IsNullOrEmpty(savedBuildError)) GUILayout.Label(savedBuildError);
            if (TryGetBuild(out var data, out string error, false)) {
                var performance = data.CalculatePerformance();
                GUILayout.Label($"最大速力 {performance.MaximumSpeed:0.00} U/s / 最大旋回 {performance.MaximumTurnSpeed:0.00} deg/s");
                GUILayout.Label($"コアHP {ShipBuildData.CoreHP[data.coreHPSettingID]} / 砲塔HP {data.TurretHP}");
            } else GUILayout.Label(error);
            bool valid = TryGetBuild(out data, out error);
            GUILayout.Label(valid ? "保存条件：成立（マップへ戻る際に確定保存）" : "保存条件：" + error);
            if (movementTrial == null) {
                bool old = GUI.enabled; GUI.enabled = old && valid && !generating;
                if (GUILayout.Button("現在の設計で操艦試験を開始")) {
                    BeginMovementTrial(data);
                }
                GUI.enabled = old;
            } else {
                var motor = movementTrial.GetComponent<ShipMovement>();
                GUILayout.Label("左クリック：マウス位置へ砲撃（長押しで連射）");
                GUILayout.Label($"↑/W・↓/S：速度段階　←/A・→/D：旋回　Space：停止　指示 {motor.Command} / 適用 {motor.AppliedThrottle}");
                GUILayout.Label($"位置 {movementTrial.transform.position} / EM {movementTrial.EmAccumulation:0.0} / 遅延 {motor.PendingSeconds:0.00}s");
                if (GUILayout.Button("電磁障害を試験（EM +150）")) movementTrial.AccumulateImpact(0, 150);
                if (GUILayout.Button("操艦試験を終了")) StopMovementTrial();
            }
        }

        private void BeginMovementTrial(ShipBuildData data)
        {
            StopMovementTrial();
            protectionTestMode = enableTurretTest = false;
            ClearTestProjectiles();
            movementTrial = ShipBuilder.Build(data, material, Vector2.zero, 0, true, true, stationary: true);
            movementTrial.GetComponent<ShipMovement>().enabled = true;
            movementTrialTurrets = movementTrial.GetComponentsInChildren<BattleshipSection>();
            // World-space container: fired rounds must not follow the moving ship.
            movementTrialProjectiles = new GameObject("Movement trial projectiles");
            drawing.SetActive(false);
            viewCamera.orthographicSize = 2500;
        }

        private void TickMovementTrialFire(float seconds, bool pressed, Vector2 screenPosition)
        {
            if (movementTrial == null || !movementTrial.IsAlive || movementTrialTurrets == null || viewCamera == null) return;
            Vector2? aimPoint = null;
            var guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            if (pressed && viewCamera.pixelRect.Contains(screenPosition) &&
                !StellarSystem.ShipConstructionReturnUI.PanelRect.Contains(guiPosition) && GUIUtility.hotControl == 0)
            {
                var plane = new Plane(Vector3.forward, Vector3.zero);
                var ray = viewCamera.ScreenPointToRay(screenPosition);
                if (plane.Raycast(ray, out float distance)) aimPoint = ray.GetPoint(distance);
            }
            foreach (var turret in movementTrialTurrets)
                if (turret != null) turret.TickManualWeapon(seconds, aimPoint, movementTrialProjectiles.transform);
        }

        private void StopMovementTrial()
        {
            if (movementTrialProjectiles != null) {
                movementTrialProjectiles.SetActive(false); Release(movementTrialProjectiles); movementTrialProjectiles = null;
            }
            movementTrialTurrets = null;
            if (movementTrial == null) return;
            var obj = movementTrial.gameObject; movementTrial = null; obj.SetActive(false); Release(obj);
            if (drawing != null) drawing.SetActive(true);
            if (generator != null && viewCamera != null) UpdateView();
        }

        private void LateUpdate()
        {
            if (movementTrial != null && viewCamera != null) {
                viewCamera.transform.position = new Vector3(movementTrial.transform.position.x, movementTrial.transform.position.y, -10);
                var mouse = UnityEngine.InputSystem.Mouse.current;
                TickMovementTrialFire(Time.deltaTime, mouse != null && mouse.leftButton.isPressed,
                    mouse == null ? Vector2.zero : mouse.position.ReadValue());
            }
        }
    }
}

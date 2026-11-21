using System;
using System.Linq;
using UnityEngine;

namespace Script
{
    [Serializable]
    public sealed class ShipArmorSettings
    {
        public int count;
        public int hpSettingID;
        public int defenseThicknessSettingID;
    }

    // Local build slots use the existing runtime weapon order, not reward catalogue IDs.
    [Serializable]
    public sealed class ShipBuildData
    {
        public int version = 1;
        public int seed;
        public int referenceSections = 12;
        public int maximumAttempts = 160;
        public bool triangleStart;
        public int startAngle;
        public bool formEnds = true;
        public int endSearchDepth = 6;
        public int endSearchLimit = 1200;
        public int shipClass;
        public int coreHPSettingID;
        public int[] weaponIDs = { 0, 1, 2, 3, -1, -1, -1 };
        public int[] weaponCounts = { 4, 0, 0, 6, 0, 0, 0 };
        public ShipArmorSettings[] armor = { new ShipArmorSettings { count = 2 }, new ShipArmorSettings(), new ShipArmorSettings() };
        public float baseSpeed = 250;
        public float baseTurnSpeed = 25;
        public float generatorSpeed = 1;
        public float generatorTurn = 1;
        public float sectionSize = 100;
        public float radarCorrection = 1;
        public float heatGainReduction;
        public float emGainReduction;
        public float heatCapOffset;
        public float emCapOffset;

        public static readonly double[] CoreHP = { 500, 1000, 2000, 5000, 10000, 15000, 20000, 25000, 30000, 40000, 50000 };
        public static readonly double[] ArmorHP = { 10, 50, 100, 150, 200 };
        public static readonly float[] Thickness = { 25, 50, 100, 200, 300 };
        public static readonly double[] Interception = { .10, .12, .15, .20, .25 };
        public double TurretHP => CoreHP[coreHPSettingID] * .2;
        public ShipBuildData Snapshot() => JsonUtility.FromJson<ShipBuildData>(JsonUtility.ToJson(this));

        public bool Validate(out string error, bool requireCountMatch = true)
        {
            error = null;
            if (version != 1 || coreHPSettingID < 0 || coreHPSettingID >= CoreHP.Length ||
                shipClass < 0 || shipClass > 3 || referenceSections < 1 || referenceSections > 128 ||
                maximumAttempts < 1 || maximumAttempts > 2000 || endSearchDepth < 1 || endSearchDepth > 12 ||
                endSearchLimit < 1 || endSearchLimit > 10000)
                error = "建造設定のID・区画数・生成条件が範囲外です。";
            else if (weaponIDs == null || weaponCounts == null || weaponIDs.Length != 7 || weaponCounts.Length != 7 ||
                armor == null || armor.Length != 3 || armor.Any(a => a == null))
                error = "兵装は7枠、装甲はBasic・ERA・AEMの3枠で指定してください。";
            else if (weaponIDs.Where((id, i) => id < -1 || id > 3 || (id == -1 && weaponCounts[i] != 0)).Any() ||
                weaponCounts.Any(n => n < 0 || n > 128) ||
                armor.Any(a => a.count < 0 || a.count > 60 || a.hpSettingID < 0 || a.hpSettingID > 4 ||
                    a.defenseThicknessSettingID < 0 || a.defenseThicknessSettingID > 4) || armor.Sum(a => a.count) > 60)
                error = "兵装または装甲の設定が範囲外です（総装甲数0～60）。";
            else if (!FiniteNonnegative(baseSpeed) || !FiniteNonnegative(baseTurnSpeed) ||
                !FiniteNonnegative(generatorSpeed) || !FiniteNonnegative(generatorTurn) ||
                !FiniteNonnegative(sectionSize) || sectionSize <= 0)
                error = "機関性能と区画サイズには有効な数値を指定してください。";
            else if (!InRange(radarCorrection, 1, 100) || !InRange(heatGainReduction, 0, .99f) ||
                !InRange(emGainReduction, 0, .99f) || !InRange(heatCapOffset, -49, 0) || !InRange(emCapOffset, -49, 0))
                error = "設備性能の範囲：命中補正1～100、軽減率0～0.99、蓄積上限補正−49～0です。";
            else if (!IsValidStartAngle(triangleStart, startAngle))
                error = "初期形状が左右対称になる角度を選んでください。";
            else if (requireCountMatch && referenceSections != weaponCounts.Sum() + armor.Sum(a => a.count))
                error = "目安区画数＝砲塔区画数＋装甲区画数にしてください（コアの+1は不要）。";
            return error == null;
        }

        private static bool FiniteNonnegative(float x) => !float.IsNaN(x) && !float.IsInfinity(x) && x >= 0;
        public static bool IsValidStartAngle(bool triangle, int angle)
            => angle >= -30 && angle < 360 && (triangle ? (angle - 30) % 60 == 0 : angle % 45 == 0);
        private static bool InRange(float x, float min, float max) => !float.IsNaN(x) && x >= min && x <= max;

        public bool TryCreateConstructionSnapshot(out ShipBuildData snapshot, out string error, bool requireCountMatch = true)
        {
            snapshot = null;
            if (!Validate(out error, requireCountMatch) || !ShipEquipmentValues.TryEncode(this, out var values, out error)) return false;
            snapshot = Snapshot();
            return values.TryApply(snapshot, out error) && snapshot.Validate(out error, requireCountMatch);
        }

        public ShipPerformance CalculatePerformance()
        {
            if (!TryCreateConstructionSnapshot(out var normalized, out string error, false)) throw new ArgumentException(error);
            double[] weights = { .4, 1, .7 };
            double denominator = 0, numerator = 0;
            for (int i = 0; i < 3; i++)
            {
                double weight = weights[i] * armor[i].count;
                denominator += weight;
                numerator += weight * (armor[i].hpSettingID + armor[i].defenseThicknessSettingID);
            }
            double s = denominator == 0 ? 0 : numerator / denominator;
            double fa = Math.Max(.5, 1 - .0016 * Math.Pow(armor.Sum(a => a.count), .7 + s / 10));
            double fh = 1.5 - .1 * coreHPSettingID;
            double fn = 1.2 - Math.Max(0, Math.Min(1, (referenceSections - 5) / 50d));
            return new ShipPerformance(
                (float)Math.Max(0, Math.Min(1000, normalized.baseSpeed * normalized.generatorSpeed * fa * fh * fn)),
                (float)Math.Max(5, Math.Min(135, normalized.baseTurnSpeed * (1.2 * fa * fh + .2) * normalized.generatorTurn)));
        }
    }

    public readonly struct ShipPerformance
    {
        public readonly float MaximumSpeed, MaximumTurnSpeed;
        public ShipPerformance(float speed, float turn) { MaximumSpeed = speed; MaximumTurnSpeed = turn; }
    }

    public static class ShipBuildStore
    {
        public const string Key = "ShipBuild.Committed.v2";
        public const string LegacyKey = "ShipBuild.Committed.v1";
        [Serializable]
        private sealed class SaveEnvelope
        {
            public int version = 2;
            public ShipBuildData design;
            public string EquipmentPerformanceValues;
        }
        public static bool TryLoad(out ShipBuildData data)
            => TryLoad(out data, out _);

        public static bool TryLoad(out ShipBuildData data, out string error)
        {
            data = null;
            error = null;
            if (!PlayerPrefs.HasKey(Key) && !PlayerPrefs.HasKey(LegacyKey)) return false;
            try {
                if (PlayerPrefs.HasKey(Key)) {
                    var envelope = JsonUtility.FromJson<SaveEnvelope>(PlayerPrefs.GetString(Key));
                    if (envelope == null || envelope.version != 2 || envelope.design == null || string.IsNullOrEmpty(envelope.EquipmentPerformanceValues))
                        throw new ArgumentException("保存形式が不正です。");
                    var values = JsonUtility.FromJson<ShipEquipmentValues>(envelope.EquipmentPerformanceValues);
                    if (values == null || !values.TryApply(envelope.design, out error) || !envelope.design.Validate(out error)) {
                        if (error == null) error = "設備データがありません。";
                        return false;
                    }
                    data = envelope.design;
                    return true;
                }
                var legacy = JsonUtility.FromJson<ShipBuildData>(PlayerPrefs.GetString(LegacyKey));
                if (legacy != null && legacy.TryCreateConstructionSnapshot(out data, out error)) return true;
            }
            catch (ArgumentException ex) { error = "建造データを読み込めません：" + ex.Message; }
            data = null;
            if (error == null) error = "保存された建造データが不正です。";
            return false;
        }
        public static bool TrySave(ShipBuildData data, out string error)
        {
            error = "建造データがありません。";
            if (data == null || !data.TryCreateConstructionSnapshot(out var snapshot, out error) ||
                !ShipEquipmentValues.TryEncode(snapshot, out var values, out error)) return false;
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(new SaveEnvelope {
                design = snapshot, EquipmentPerformanceValues = JsonUtility.ToJson(values) }));
            PlayerPrefs.Save();
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Script
{
    // IDs and field names belong only to the local build-save adapter, not the
    // future inventory catalogue. Unspecified catapult/barrier values stay empty.
    internal enum BuildEquipmentID
    {
        FiringDelay, Loader, Catapult, Radar, Barrier, BowThruster, Thruster,
        Generator, FireResistantArmor, BeamCoating, PowderCapacity, Insulation
    }

    [Serializable]
    public sealed class ShipEquipmentValues
    {
        public int[] EquipmentID;
        public string[] PerformanceValues;

        private static readonly string[][] Fields = {
            new[] { "spreadReduction" }, new[] { "reloadReduction" }, Array.Empty<string>(),
            new[] { "hitCorrection" }, Array.Empty<string>(), new[] { "turnSpeed" }, new[] { "speed" },
            new[] { "speedMultiplier", "turnMultiplier" }, new[] { "heatReduction" },
            new[] { "emReduction" }, new[] { "heatCapOffset" }, new[] { "emCapOffset" }
        };

        public static bool TryEncode(ShipBuildData data, out ShipEquipmentValues result, out string error)
        {
            result = null;
            error = null;
            float[][] values = {
                new[] { 0f }, new[] { 0f }, Array.Empty<float>(), new[] { data.radarCorrection }, Array.Empty<float>(),
                new[] { data.baseTurnSpeed }, new[] { data.baseSpeed }, new[] { data.generatorSpeed, data.generatorTurn },
                new[] { data.heatGainReduction }, new[] { data.emGainReduction }, new[] { data.heatCapOffset }, new[] { data.emCapOffset }
            };
            var strings = new string[Fields.Length];
            for (int id = 0; id < Fields.Length; id++)
            {
                var parts = new List<string>();
                for (int field = 0; field < Fields[id].Length; field++)
                {
                    if (!TryFormat(values[id][field], out string value)) {
                        error = "設備性能を保存できません。負の上限補正は小数4桁以内で入力してください。";
                        return false;
                    }
                    parts.Add(Fields[id][field] + "=" + value);
                }
                strings[id] = string.Join(",", parts);
            }
            result = new ShipEquipmentValues { EquipmentID = Enumerable.Range(0, Fields.Length).ToArray(), PerformanceValues = strings };
            return true;
        }

        private static bool TryFormat(float input, out string text)
        {
            text = null;
            if (!decimal.TryParse(input.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float,
                CultureInfo.InvariantCulture, out decimal value)) return false;
            // The negative rounding direction is not specified. Accept exact precision
            // only, rather than choosing floor/toward-zero silently.
            if (value < 0 && decimal.Round(value, 4) != value) return false;
            try { value = decimal.Truncate(value * 10000m) / 10000m; }
            catch (OverflowException) { return false; }
            text = value.ToString("0.0000", CultureInfo.InvariantCulture);
            return true;
        }

        public bool TryApply(ShipBuildData data, out string error)
        {
            error = "設備IDと性能文字列の対応が不正です。";
            if (EquipmentID == null || PerformanceValues == null || EquipmentID.Length != Fields.Length ||
                PerformanceValues.Length != Fields.Length || EquipmentID.Distinct().Count() != Fields.Length) return false;
            var parsed = new Dictionary<string, float>[Fields.Length];
            for (int i = 0; i < EquipmentID.Length; i++)
            {
                int id = EquipmentID[i];
                if (id < 0 || id >= Fields.Length || PerformanceValues[i] == null) return false;
                var parameters = new Dictionary<string, float>(StringComparer.Ordinal);
                if (PerformanceValues[i].Length != 0)
                foreach (string pair in PerformanceValues[i].Split(','))
                {
                    string[] tokens = pair.Split('=');
                    if (tokens.Length != 2 || !Fields[id].Contains(tokens[0]) || parameters.ContainsKey(tokens[0]) ||
                        !float.TryParse(tokens[1], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                            CultureInfo.InvariantCulture, out float value) ||
                        !TryFormat(value, out string canonical) || canonical != tokens[1]) return false;
                    parameters.Add(tokens[0], value);
                }
                if (parameters.Count != Fields[id].Length) return false;
                parsed[id] = parameters;
            }
            // Scatter/loader non-MVP values need unresolved range/order rules.
            if (parsed[0]["spreadReduction"] != 0 || parsed[1]["reloadReduction"] != 0) return false;
            data.baseTurnSpeed = parsed[(int)BuildEquipmentID.BowThruster]["turnSpeed"];
            data.baseSpeed = parsed[(int)BuildEquipmentID.Thruster]["speed"];
            data.generatorSpeed = parsed[(int)BuildEquipmentID.Generator]["speedMultiplier"];
            data.generatorTurn = parsed[(int)BuildEquipmentID.Generator]["turnMultiplier"];
            data.radarCorrection = parsed[(int)BuildEquipmentID.Radar]["hitCorrection"];
            data.heatGainReduction = parsed[(int)BuildEquipmentID.FireResistantArmor]["heatReduction"];
            data.emGainReduction = parsed[(int)BuildEquipmentID.BeamCoating]["emReduction"];
            data.heatCapOffset = parsed[(int)BuildEquipmentID.PowderCapacity]["heatCapOffset"];
            data.emCapOffset = parsed[(int)BuildEquipmentID.Insulation]["emCapOffset"];
            error = null;
            return true;
        }
    }
}

using System;
using System.Collections.Generic;

// Immutable-by-convention snapshot: generated once when entering a battle.
public sealed class BarrageDefinition
{
    public float interval, speed, radius, lifetime;
    public int count;
    public CardDirectionBasis basis;
    public float angleStart, angleEnd, turnStart, turnEnd, offsetStart, offsetEnd;
    public float slopeStart, slopeEnd, powerStart, powerEnd;

    public BarrageProjectile GetProjectile(int index)
    {
        if (index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
        // A single bullet uses the start of the formation.
        float t = count == 1 ? 0f : (float)index / (count - 1);
        return new BarrageProjectile
        {
            angle = Lerp(angleStart, angleEnd, t),
            angularSpeed = Lerp(turnStart, turnEnd, t),
            speed = speed + Lerp(offsetStart, offsetEnd, t),
            powerSlope = Lerp(slopeStart, slopeEnd, t),
            powerConstant = Lerp(powerStart, powerEnd, t)
        };
    }

    private static float Lerp(float a, float b, float t) => (float)((1d - t) * a + t * b);
}

public struct BarrageProjectile
{
    public float angle, angularSpeed, speed, powerSlope, powerConstant;
    public float PowerAt(float seconds) => powerSlope * seconds + powerConstant;
}

public static class BarrageComposer
{
    public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static float SumModifier(CardParameterValue parameter, IEnumerable<CardData> cards, bool isparent = true)
    {
        if (parameter == null || parameter.isToParent != isparent) return 0f;
        float sum = parameter.mod;
        if (cards != null)
            foreach (CardData card in cards)
                if (card?.items != null)
                    foreach (CardItem item in card.items)
                        if (item != null && item.ID == parameter.ID) sum += item.mod;
        return sum;
    }

    public static bool TryCompose(CardInitialSettings settings, CardParameterValue[] parameters,
        IEnumerable<CardData> cards, out BarrageDefinition result, out string error, bool isparent = true)
    {
        result = null;
        error = null;
        try
        {
            if (settings == null || parameters == null) throw new ArgumentException("Missing barrage initial settings.");
            if (settings.interpolation != CardFormationInterpolation.Linear)
                throw new ArgumentException("Unsupported formation interpolation.");
            if (settings.directionBasis != CardDirectionBasis.Aim && settings.directionBasis != CardDirectionBasis.Movement) throw new ArgumentException("Invalid direction basis.");
            var values = new Dictionary<string, float>(StringComparer.Ordinal);
            var selected = cards == null ? new List<CardData>() : new List<CardData>(cards);
            foreach (CardParameterValue p in parameters)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.ID) || values.ContainsKey(p.ID))
                    throw new ArgumentException("Null, empty or duplicate parameter ID.");
                float value = p.initialValue + SumModifier(p, selected, isparent);
                if (!IsFinite(value)) throw new ArgumentException(p.ID + " must be finite.");
                values.Add(p.ID, value);
            }
            foreach (CardData card in selected)
                if (card?.items != null)
                    foreach (CardItem item in card.items)
                        if (item == null || string.IsNullOrEmpty(item.ID) || !values.ContainsKey(item.ID) || !IsFinite(item.mod))
                            throw new ArgumentException("Invalid card parameter: ID must exist and modifier must be finite.");
            Func<string, float> get = id => values.TryGetValue(id, out float v)
                ? v : throw new ArgumentException("Missing parameter: " + id);
            float count = get(CardParameterIds.BulletCount);
            double integerCount = Math.Floor(count);
            if (integerCount < 1 || integerCount > int.MaxValue)
                throw new ArgumentException("BulletCount must be a positive int after flooring.");
            var d = new BarrageDefinition
            {
                basis = settings.directionBasis,
                interval = get(CardParameterIds.FireInterval), count = (int)integerCount,
                speed = get(CardParameterIds.BulletSpeed),
                radius = get(CardParameterIds.BulletRadius), lifetime = get(CardParameterIds.BulletLifetime),
                angleStart = get(CardParameterIds.FormationAngleStart), angleEnd = get(CardParameterIds.FormationAngleEnd),
                turnStart = get(CardParameterIds.AngularSpeedStart), turnEnd = get(CardParameterIds.AngularSpeedEnd),
                offsetStart = get(CardParameterIds.SpeedOffsetStart), offsetEnd = get(CardParameterIds.SpeedOffsetEnd),
                slopeStart = get(CardParameterIds.PowerSlopeStart), slopeEnd = get(CardParameterIds.PowerSlopeEnd),
                powerStart = get(CardParameterIds.PowerConstantStart), powerEnd = get(CardParameterIds.PowerConstantEnd)
            };
            if (d.interval <= 0 || d.speed <= 0 || d.radius <= 0 || d.lifetime <= 0)
                throw new ArgumentException("FireInterval, BulletSpeed, BulletRadius and BulletLifetime must be > 0.");
            // Linear interpolation's extrema are its endpoints. Do not emit a partial volley.
            foreach (int i in new[] { 0, d.count - 1 })
            {
                BarrageProjectile shot = d.GetProjectile(i);
                if (!IsFinite(shot.speed) || shot.speed <= 0 || !IsFinite(shot.PowerAt(d.lifetime)))
                    throw new ArgumentException("Invalid formation speed or power.");
            }
            result = d;
            return true;
        }
        catch (ArgumentException exception) { error = exception.Message; return false; }
    }

}

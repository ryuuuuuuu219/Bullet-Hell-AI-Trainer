using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;

class Program
{
    static int checks;
    static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        checks++;
    }
    static void Near(float value, float expected, string name) => Check(Math.Abs(value - expected) < 0.0001f, name);

    static void Main(string[] args)
    {
        string asset = File.ReadAllText(args[0]);
        CardParameterValue[] Defaults() => Regex.Matches(asset,
            "ID: \"(\\w+)\"\\s+isToParent: ([01])\\s+initialValue: ([-\\d.]+)\\s+mod: ([-\\d.]+)")
            .Select(m => new CardParameterValue { ID = m.Groups[1].Value,
                isToParent = m.Groups[2].Value == "1",
                initialValue = float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                mod = float.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) }).ToArray();
        var settings = new CardInitialSettings();
        var speedCard = new CardData { items = new[] { new CardItem { ID = CardParameterIds.BulletSpeed, mod = 10 } } };
        BarrageDefinition Compose(CardParameterValue[] values, params CardData[] cards)
        {
            Check(BarrageComposer.TryCompose(settings, values, cards, out var d, out string error), error);
            return d;
        }
        void Reject(string id, float value)
        {
            var values = Defaults();
            values.Single(p => p.ID == id).initialValue = value;
            Check(!BarrageComposer.TryCompose(settings, values, new[] { speedCard }, out var d, out string error)
                && d == null && !string.IsNullOrEmpty(error), "Reject " + id + "=" + value);
        }
        var original = Defaults();
        var baseDefinition = Compose(original);
        var selected = Compose(original, speedCard);
        Check(selected.count == 8, "Initial count");
        Near(selected.interval, 5, "Interval");
        Near(baseDefinition.GetProjectile(0).speed, 105, "Unselected first speed");
        Near(selected.GetProjectile(0).speed, 115, "Selected first speed");
        Near(selected.GetProjectile(7).speed, 168, "Selected last speed");
        Near(selected.GetProjectile(0).angle, 0, "First angle");
        Near(selected.GetProjectile(7).angle, 14, "Last angle");
        Near(selected.GetProjectile(3).angle, 6, "Interior angle");
        Near(selected.GetProjectile(0).PowerAt(5), .3f, "First power");
        Near(selected.GetProjectile(7).PowerAt(5), .9f, "Last power");
        Near(original.Single(p => p.ID == CardParameterIds.BulletSpeed).initialValue, 120, "Source unchanged");
        Near(speedCard.items[0].mod, 10, "Card unchanged");
        var routed = Defaults();
        var routedSpeed = routed.Single(p => p.ID == CardParameterIds.BulletSpeed);
        routedSpeed.mod = 2;
        Near(BarrageComposer.SumModifier(routedSpeed, new[] { speedCard }), 12, "Parent receives base and card modifiers");
        Near(BarrageComposer.SumModifier(routedSpeed, new[] { speedCard }, false), 0, "Child excludes parent modifiers");
        routedSpeed.isToParent = false;
        Near(Compose(routed, speedCard).speed, 120, "Parent retains initial value for child parameter");
        Check(BarrageComposer.TryCompose(settings, routed, new[] { speedCard }, out var child, out _, false), "Compose child");
        Near(child.speed, 132, "Child receives base and selected card modifiers");
        var invalidCard = new CardData { items = new[] { new CardItem { ID = CardParameterIds.BulletSpeed, mod = float.NaN } } };
        Check(!BarrageComposer.TryCompose(settings, routed, new[] { invalidCard }, out _, out _), "Invalid modifier rejected even when not applied");
        var fractional = new CardData { items = new[] { new CardItem { ID = CardParameterIds.BulletCount, mod = .9f } } };
        Check(Compose(Defaults(), fractional).count == 8, "Floor after composition");
        var one = Defaults(); one.Single(p => p.ID == CardParameterIds.BulletCount).initialValue = 1.9f;
        var single = Compose(one);
        Check(single.count == 1, "Floor 1.9"); Near(single.GetProjectile(0).angle, 0, "Single uses start");
        var varying = Defaults();
        varying.Single(p => p.ID == CardParameterIds.PowerSlopeStart).initialValue = .2f;
        Near(Compose(varying).GetProjectile(0).PowerAt(2), .7f, "Power uses seconds");
        foreach (string id in new[] { CardParameterIds.FireInterval, CardParameterIds.BulletRadius, CardParameterIds.BulletLifetime })
        { Reject(id, 0); Reject(id, -1); Reject(id, float.NaN); Reject(id, float.PositiveInfinity); }
        Reject(CardParameterIds.BulletCount, .9f); Reject(CardParameterIds.BulletCount, -1);
        Reject(CardParameterIds.BulletCount, float.MaxValue);
        Reject(CardParameterIds.BulletSpeed, -10); // +10 yields zero.
        Reject(CardParameterIds.SpeedOffsetEnd, -130); // Final member speed zero.
        var duplicate = Defaults().Concat(new[] { Defaults()[0] }).ToArray();
        Check(!BarrageComposer.TryCompose(settings, duplicate, null, out _, out _), "Duplicate IDs rejected");
        var unknown = new CardData { items = new[] { new CardItem { ID = "Unknown", mod = 1 } } };
        Check(!BarrageComposer.TryCompose(settings, Defaults(), new[] { unknown }, out _, out _), "Unknown ID rejected");
        var small = Defaults(); small.Single(p => p.ID == CardParameterIds.FireInterval).initialValue = .001f;
        Near(Compose(small).interval, .001f, "Positive values not silently clamped");
        var repeated = new CardData { items = new[] { new CardItem { ID = "BulletSpeed", mod = 2 }, new CardItem { ID = "BulletSpeed", mod = 3 } } };
        Near(Compose(Defaults(), speedCard, repeated).speed, 135, "All matching items summed");
        Near((float)CardInventory.ConvertDropWeight(0), 1, "Curve join");
        Near((float)CardInventory.ConvertDropWeight(-1.2), 0.24145533f, "Exponential branch");
        Near((float)CardInventory.ConvertDropWeight(-100), -.2f, "Curve lower asymptote");
        Near((float)((CardInventory.ConvertDropWeight(0) - CardInventory.ConvertDropWeight(-.00001)) / .00001), 1, "Left slope at join");
        var inventory = new CardInventory(new[] { speedCard });
        CardData starter = inventory.Candidates[0];
        Check(!ReferenceEquals(starter, speedCard), "Starter is copied");
        inventory.SetSelection(new[] { starter });
        Check(inventory.TryAddDrop(12.5f, 3, out var reward, out _, () => 0.75f), "Create reward");
        foreach (var item in reward.items)
            Near(item.mod / CardInventory.GetDropMultiplier(item.ID), 203.125f, "Converted random coefficient");
        Check(reward.items.Length == 15 && reward.items.Select(x => x.ID).Distinct().Count() == 15, "All 15 distinct parameters generated");
        Check(!reward.items.Any(x => x.ID == CardParameterIds.SplitInterval), "Unimplemented split excluded");
        Check(BarrageComposer.TryCompose(settings, Defaults(), new[] { reward }, out _, out _), "Reward composes into barrage");
        for (int i = 0; i < 30; i++) Check(inventory.TryAddDrop(i, 1, out _, out _), "Append/evict reward");
        Check(inventory.Candidates.Count == 16, "Capacity 16");
        Check(inventory.Candidates.Contains(starter) && inventory.Selected[0] == starter, "Selected oldest retained");
        Check(!inventory.Candidates.Contains(reward), "Oldest unselected evicted");
        Check(inventory.TryAddDrop(0, 1, out var zero, out _), "Zero score allowed");
        Near(zero.items[0].mod, 0, "Zero score produces zero mod");
        foreach (float bad in new[] { -1f, float.NaN, float.PositiveInfinity })
            Check(!inventory.TryAddDrop(bad, 1, out _, out _), "Invalid score rejected");
        Check(!inventory.TryAddDrop(1, 0, out _, out _), "Invalid stage rejected");
        Check(!inventory.TryAddDrop(float.MaxValue, 2, out _, out _), "Overflow rejected");
        Check(inventory.Candidates.Count == 16 && inventory.Candidates.Last() == zero, "Invalid reward does not evict");
        var before = inventory.Candidates.ToArray();
        foreach (float badWeight in new[] { -1f, 2f, float.NaN, float.PositiveInfinity })
            Check(!inventory.TryAddDrop(1, 1, out _, out _, () => badWeight), "Invalid random source rejected");
        Check(before.SequenceEqual(inventory.Candidates), "Generation failures preserve all candidates");
        Check(inventory.TryAddDrop(15, 1, out var low, out _, () => 0), "Zero samples accepted");
        Near(low.items[0].mod, 41.25f, "Speed lower endpoint");
        Near(low.items[1].mod, 4.125f, "Radius lower endpoint");
        Check(inventory.TryAddDrop(15, 1, out var high, out _, () => 1), "One samples accepted");
        foreach (var item in high.items)
            Near(item.mod, 3 * low.items.Single(x => x.ID == item.ID).mod, "Converted endpoints including negative multipliers");
        Check(low.displayName.Split(' ')[0] != high.displayName.Split(' ')[0], "Drop IDs differ at capacity");
        Check(inventory.TryAddDrop(15, 1, out var center, out _, () => .5f), "Midpoint accepted");
        foreach (var item in center.items)
            Near(item.mod, 165f * CardInventory.GetDropMultiplier(item.ID), "Converted midpoint");
        int draw = 0;
        Check(inventory.TryAddDrop(15, 1, out var mixed, out _, () => draw++ == 0 ? 1 : .5f), "Independent samples accepted");
        Near(mixed.items[0].mod, high.items[0].mod, "Other samples do not change speed");
        foreach (var item in mixed.items.Skip(1))
            Near(item.mod, center.items.Single(x => x.ID == item.ID).mod, "Other parameters keep their own midpoint");
        inventory.SetSelection(inventory.Candidates);
        Check(inventory.Selected.Count == 5, "Selection limit remains five");
        Near(speedCard.items[0].mod, 10, "Catalog untouched by inventory");
        Check(new CardInventory(Enumerable.Repeat(speedCard, 20)).Candidates.Count == 16, "Starter capacity");
        inventory.SetSelection(new[] { mixed, starter, center });
        var options = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        string json = System.Text.Json.JsonSerializer.Serialize(inventory.CaptureSave(), options);
        var savedData = System.Text.Json.JsonSerializer.Deserialize<CardInventory.SaveData>(json, options);
        var loaded = CardInventory.FromSave(savedData);
        Check(loaded.Candidates.Count == inventory.Candidates.Count, "Saved inventory count restored");
        Check(loaded.Selected.Select(c => c.displayName).SequenceEqual(inventory.Selected.Select(c => c.displayName)),
            "Selection order restored");
        Check(loaded.Selected.All(c => loaded.Candidates.Contains(c)), "Selection references restored candidates");
        for (int i = 0; i < loaded.Candidates.Count; i++)
        {
            Check(loaded.Candidates[i].items.Select(x => x.ID).SequenceEqual(inventory.Candidates[i].items.Select(x => x.ID)), "Saved parameter IDs restored");
            Check(loaded.Candidates[i].items.Select(x => x.mod).SequenceEqual(inventory.Candidates[i].items.Select(x => x.mod)), "Saved modifiers restored");
        }
        Check(loaded.TryAddDrop(1, 1, out var resumedDrop, out _, () => .5f), "Drop after reload");
        Check(resumedDrop.displayName.StartsWith("ID:" + savedData.nextDropId + " "), "Drop numbering resumes");
        loaded.SetSelection(Array.Empty<CardData>());
        Check(CardInventory.FromSave(loaded.CaptureSave()).Selected.Count == 0, "Empty selection persists");
        savedData.selectedIndices = new[] { savedData.cards.Length };
        bool rejectedSave = false;
        try { CardInventory.FromSave(savedData); } catch (ArgumentException) { rejectedSave = true; }
        Check(rejectedSave, "Invalid saved selection rejected");
        Console.WriteLine($"PASS: {checks} composition/inventory checks (actual source and saved catalog).");
    }
}

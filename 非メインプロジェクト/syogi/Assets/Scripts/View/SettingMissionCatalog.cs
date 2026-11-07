// Mission order follows ステージ設計.txt (2026-11-03).
// Reward ranges follow its explicitly authoritative later reward section.
public static class SettingMissionCatalog
{
    // Index 0 is initially available. Each time-scale reward unlocks the next entry.
    // This is unlock order, not a fixed mapping from mission IDs to multipliers.
    public static readonly System.Collections.ObjectModel.ReadOnlyCollection<float> TimeScaleUnlockOrder =
        System.Array.AsReadOnly(new[] { 1f, 1.5f, 2f, 3f, 4f, 6f, 8f, 10f, 15f, 20f, .1f, 0f });
    public static string TimeScaleUnlockOrderLabel => string.Join(" / ",
        System.Linq.Enumerable.Select(TimeScaleUnlockOrder, (value, index) =>
            value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)
            + (index == 0 ? "（初期）" : value == 0 ? "（停止）" : "")));

    public sealed class Mission
    {
        public readonly int Stage;
        public readonly int Number;
        public readonly string Condition;
        public readonly string Reward;
        public string Id => Stage + "-" + Number;
        public Mission(int stage, int number, string condition, string reward)
        { Stage = stage; Number = number; Condition = condition; Reward = reward; }
    }
    public static readonly Mission[] All =
    {
        new Mission(0, 1, "草を300秒維持", "次ステージ開放"),
        new Mission(0, 2, "草食動物を600秒維持", "草の初期数・草量上限指定機能"),
        new Mission(0, 3, "草を900秒維持", "タイムスケール開放段階＋1"),
        new Mission(0, 4, "（要・1-4草のサイクル時間指定機能）サイクル時間60にて草を900秒維持", "草（セルオートマトン）の繁殖条件指定機能"),
        new Mission(1, 1, "草食動物を450秒維持", "次ステージ開放"),
        new Mission(1, 2, "肉食動物を600秒維持", "個体数比例繁殖成功率の有効化（草食動物）・基準数指定機能：50～600"),
        new Mission(1, 3, "肉食動物を900秒維持", "タイムスケール開放段階＋1"),
        new Mission(1, 4, "草食動物基準数50にて肉食動物を900秒維持", "草のサイクル時間指定機能：15～60"),
        new Mission(2, 1, "草食動物および肉食動物を450秒維持（論理積）", "次ステージ開放"),
        new Mission(2, 2, "草食動物および肉食動物を600秒維持（論理積）", "個体数比例繁殖成功率の有効化（肉食動物）・基準数指定機能：20～60"),
        new Mission(2, 3, "草食動物基準数60にて肉食動物を900秒維持", "タイムスケール開放段階＋1"),
        new Mission(2, 4, "（要・1-4草のサイクル時間指定機能）サイクル時間60にて肉食動物を900秒維持", "昼夜の速さの指定機能：30／60／90秒"),
        new Mission(3, 1, "犬型を450秒維持", "次ステージ開放"),
        new Mission(3, 2, "犬型および肉食動物を450秒維持（論理積）", "タイムスケール開放段階＋1"),
        new Mission(3, 3, "犬型および肉食動物を900秒維持（論理積）", "タイムスケール開放段階＋1"),
        new Mission(4, 1, "草食動物および犬型を150秒維持（論理積）", "次ステージ開放"),
        new Mission(4, 2, "草食動物および犬型を300秒維持（論理積）", "個体数比例繁殖成功率の有効化（犬型・ヨッシー型・蛇型・甲虫型）・基準数指定機能：0～20"),
        new Mission(4, 3, "（要・1-4草のサイクル時間指定機能）サイクル時間60にて草食動物および犬型を300秒維持（論理積）", "タイムスケール開放段階＋1"),
        new Mission(5, 1, "蛇型の撃破。必要数1（なんらかの弾幕（蛇型のをふくむ）が蛇型のHPを0以下にした場合を指す）", "次ステージ開放"),
        new Mission(5, 2, "蛇型の撃破。必要数5", "タイムスケール開放段階＋1・蛇型の維持費指定：10^-5～10^0資源/秒（対数目盛り、指数1/3刻み・全16段階）"),
        new Mission(5, 3, "蛇型を450秒維持", "タイムスケール開放段階＋1"),
        new Mission(6, 1, "草食動物を450秒維持", "次ステージ開放"),
        new Mission(6, 2, "フリゲート艦型の撃破。必要数5", "個体数比例繁殖成功率の有効化（フリゲート艦型）・基準数指定機能：0～20"),
        new Mission(6, 3, "駆逐艦型を450秒維持", "タイムスケール開放段階＋1"),
        new Mission(7, 1, "駆逐艦型が一度でも5体以上に到達", "次ステージ開放"),
        new Mission(7, 2, "駆逐艦型が一度でも10体以上に到達", "個体数比例繁殖成功率の有効化（駆逐艦型）・基準数指定機能：0～10"),
        new Mission(7, 3, "草食動物およびフリゲート艦型を900秒維持（論理積）", "タイムスケール開放段階＋1"),
        new Mission(8, 1, "戦艦型を450秒維持", "次ステージ開放"),
        new Mission(8, 2, "目安区画数48以上の戦艦型の確認。", "個体数比例繁殖成功率の有効化（巡洋艦型・戦艦型）・基準数指定機能：巡洋艦型0～5、戦艦型0～3"),
        new Mission(8, 3, "目安区画数50以上の戦艦型の確認。", "タイムスケール開放段階＋1"),
        new Mission(8, 4, "目安区画数55以上の戦艦型の確認。", "戦艦型の初期目安区画数指定機能。発生した個体の最大目安区画数を範囲上限とし、リセットしない"),
    };
}

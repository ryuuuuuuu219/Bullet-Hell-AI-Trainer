using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Like BattleViewControls, the scene builds its uGUI hierarchy at runtime.
public sealed class SettingScene : MonoBehaviour
{
    [SerializeField] private Font japaneseFont;
    private Font font;
    private RectTransform missionContent;
    private ScrollRect missionScroll;
    private Button missionOrderButton;
    private Button functionOrderButton;
    private static readonly Dictionary<string, float> values = new Dictionary<string, float>();
    private static readonly Color Accent = new Color(.25f, .78f, .72f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession() => values.Clear();

    private void Start()
    {
        font = japaneseFont != null ? japaneseFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (EventSystem.current == null)
            new GameObject("Setting EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).transform.SetParent(transform, false);
        RectTransform canvas = Rect("Setting Canvas", transform);
        canvas.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        var background = Panel("Background", canvas, new Color(.065f, .09f, .13f));
        Stretch(background);
        Text title = Label(canvas, "生態系の設定", 34);
        Place(title.rectTransform, 32, 18, 1400, 54);
        Text help = Label(canvas, "操作値はプレイ中のみ保持・バトル反映／報酬連携は未接続", 22);
        Place(help.rectTransform, 32, 78, 1700, 38);
        missionOrderButton = SortButton(canvas, "ミッション順", 32, false);
        functionOrderButton = SortButton(canvas, "機能別", 268, true);
        var menu = Panel("Back to Menu", canvas, new Color(.15f, .25f, .32f));
        menu.anchorMin = menu.anchorMax = new Vector2(1, 1);
        menu.pivot = new Vector2(1, 1); menu.anchoredPosition = new Vector2(-32, -20); menu.sizeDelta = new Vector2(220, 52);
        var button = menu.gameObject.AddComponent<Button>();
        button.targetGraphic = menu.GetComponent<Image>();
        button.onClick.AddListener(GetComponent<SceneNavigation>().ReturnToMainMenu);
        Stretch(Label(menu, "メニューへ戻る", 24).rectTransform);

        RectTransform scrollRoot = Panel("Mission Scroll View", canvas, new Color(.09f, .12f, .17f));
        Stretch(scrollRoot); scrollRoot.offsetMin = new Vector2(32, 32); scrollRoot.offsetMax = new Vector2(-32, -196);
        var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false; scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45;
        RectTransform viewport = Panel("Viewport", scrollRoot, Color.white);
        viewport.GetComponent<Image>().color = new Color(1, 1, 1, .01f);
        Stretch(viewport); viewport.offsetMax = new Vector2(-24, 0);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Rect("Missions", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
        content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12); layout.spacing = 8;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content;
        RectTransform track = Panel("Vertical Scrollbar", scrollRoot, new Color(.14f, .18f, .24f));
        track.anchorMin = new Vector2(1, 0); track.anchorMax = Vector2.one;
        track.pivot = Vector2.one; track.sizeDelta = new Vector2(18, 0); track.anchoredPosition = Vector2.zero;
        RectTransform handle = Panel("Handle", track, Accent); Stretch(handle);
        var scrollbar = track.gameObject.AddComponent<Scrollbar>();
        scrollbar.handleRect = handle; scrollbar.targetGraphic = handle.GetComponent<Image>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scroll.verticalScrollbar = scrollbar;
        missionContent = content;
        missionScroll = scroll;
        SortMissions(false);
    }

    private Button SortButton(Transform parent, string caption, float x, bool byFunction)
    {
        var rect = Panel(caption, parent, new Color(.15f, .25f, .32f));
        Place(rect, x, 130, 220, 48);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        var label = Label(rect, caption, 24);
        label.alignment = TextAnchor.MiddleCenter;
        Stretch(label.rectTransform);
        button.onClick.AddListener(() => SortMissions(byFunction));
        return button;
    }

    private static readonly string[] FunctionNames =
    {
        "草の初期数・草量上限", "草の繁殖条件", "草のサイクル時間",
        "個体数比例繁殖・基準数", "昼夜の速さ", "戦艦型の初期目安区画数",
        "タイムスケール", "次ステージ開放"
    };

    private static int FunctionIndex(SettingMissionCatalog.Mission mission)
    {
        switch (mission.Id)
        {
            case "0-2": return 0;
            case "0-4": return 1;
            case "1-4": return 2;
            case "1-2": case "2-2": case "4-2": case "6-2": case "7-2": case "8-2": return 3;
            case "2-4": return 4;
            case "8-4": return 5;
            default: return mission.Reward.Contains("タイムスケール") ? 6 : 7;
        }
    }

    private void SortMissions(bool byFunction)
    {
        // Deactivate immediately: Destroy is deferred until the end of the frame.
        foreach (Transform child in missionContent)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        var missions = new List<SettingMissionCatalog.Mission>(SettingMissionCatalog.All);
        if (byFunction)
            missions.Sort((a, b) =>
            {
                int category = FunctionIndex(a).CompareTo(FunctionIndex(b));
                if (category != 0) return category;
                int stage = a.Stage.CompareTo(b.Stage);
                return stage != 0 ? stage : a.Number.CompareTo(b.Number);
            });
        string previousHeading = null;
        foreach (SettingMissionCatalog.Mission mission in missions)
        {
            string headingText = byFunction ? FunctionNames[FunctionIndex(mission)] : "ステージ " + mission.Stage;
            if (previousHeading != headingText)
            {
                previousHeading = headingText;
                var heading = Label(missionContent, headingText, 28);
                heading.color = Accent;
                heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 48;
            }
            BuildRow(missionContent, mission);
        }
        missionOrderButton.interactable = byFunction;
        functionOrderButton.interactable = !byFunction;
        missionScroll.StopMovement();
        Canvas.ForceUpdateCanvases();
        missionScroll.verticalNormalizedPosition = 1;
    }

    private void BuildRow(Transform parent, SettingMissionCatalog.Mission mission)
    {
        RectTransform row = Panel("Mission " + mission.Id, parent, new Color(.12f, .16f, .22f));
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 112;
        var horizontal = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        horizontal.padding = new RectOffset(16, 16, 10, 10); horizontal.spacing = 16;
        horizontal.childControlWidth = horizontal.childControlHeight = true;
        horizontal.childForceExpandWidth = false; horizontal.childForceExpandHeight = true;
        var label = Label(row, mission.Id + "  " + mission.Condition + "\n<size=19><color=#AABCCD>報酬：" + mission.Reward + "</color></size>", 24);
        var labelLayout = label.gameObject.AddComponent<LayoutElement>();
        labelLayout.minWidth = 550; labelLayout.preferredWidth = 700; labelLayout.flexibleWidth = 1;
        RectTransform controls = Rect("Controls", row);
        var controlLayout = controls.gameObject.AddComponent<LayoutElement>();
        controlLayout.preferredWidth = 940; controlLayout.minWidth = 800;
        var group = controls.gameObject.AddComponent<HorizontalLayoutGroup>();
        group.spacing = 14; group.childControlWidth = group.childControlHeight = true;
        group.childForceExpandWidth = group.childForceExpandHeight = true;
        switch (mission.Id)
        {
            case "0-2":
                SliderControl(controls, "grass.initialCount", "草の初期数", 20, 150, 20);
                SliderControl(controls, "grass.maxAmount", "草量上限", 2, 10, 2);
                break;
            case "0-4": BirthNeighborControls(controls); break;
            case "1-2": Population(controls, mission.Id, "草食動物", 50, 600); break;
            case "1-4": SliderControl(controls, "grass.cycle", "草のサイクル（秒）", 15, 60, 15); break;
            case "2-2": Population(controls, mission.Id, "肉食動物", 20, 60); break;
            case "2-4": SliderControl(controls, "dayNight", "昼夜（秒）", 0, 2, 0, new[] { 30, 60, 90 }); break;
            case "4-2":
                foreach (string species in new[] { "犬型", "ヨッシー型", "蛇型", "甲虫型" }) Population(controls, mission.Id, species, 0, 20);
                break;
            case "6-2": Population(controls, mission.Id, "フリゲート艦型", 0, 20); break;
            case "7-2": Population(controls, mission.Id, "駆逐艦型", 0, 10); break;
            // The memo explicitly gives its later reward section priority over the earlier table.
            case "8-2": Population(controls, mission.Id, "巡洋艦型", 0, 5); Population(controls, mission.Id, "戦艦型", 0, 3); break;
            case "8-4": Pending(controls, "初期目安区画数：上限記録・範囲未接続", true); break;
            default:
                Pending(controls, mission.Reward.Contains("タイムスケール")
                    ? "獲得ごとに次の倍率を開放\n" + SettingMissionCatalog.TimeScaleUnlockOrderLabel
                    : "次ステージ：自動解放", false);
                break;
        }
    }

    private void BirthNeighborControls(Transform parent)
    {
        Slider minimum = SliderControl(parent, "grass.birthNeighborMinimum", "隣接生存セル数：最小", 1, 3, 1)
            .GetComponentInChildren<Slider>();
        Slider maximum = SliderControl(parent, "grass.birthNeighborMaximum", "隣接生存セル数：最大", 1, 3, 1)
            .GetComponentInChildren<Slider>();
        // Keep the inclusive interval valid; move the opposite endpoint when crossed.
        if (minimum.value > maximum.value) maximum.value = minimum.value;
        minimum.onValueChanged.AddListener(value =>
        {
            if (value > maximum.value) maximum.value = value;
        });
        maximum.onValueChanged.AddListener(value =>
        {
            if (value < minimum.value) minimum.value = value;
        });
    }

    private void Population(Transform parent, string mission, string species, int min, int max)
    {
        RectTransform cell = Rect(species, parent);
        cell.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        var toggle = ToggleControl(cell, mission + "." + species + ".enabled", species, true);
        toggle.anchorMin = new Vector2(0, .55f); toggle.anchorMax = Vector2.one;
        var slider = SliderControl(cell, mission + "." + species + ".count", "基準数", min, max, min);
        slider.anchorMax = new Vector2(1, .55f);
    }

    private void Pending(Transform parent, string caption, bool slider)
    {
        if (slider) SliderControl(parent, null, caption, 0, 1, 0);
        else ToggleControl(parent, null, caption, false);
    }

    private RectTransform ToggleControl(Transform parent, string key, string caption, bool enabled)
    {
        RectTransform root = Rect(caption, parent); Stretch(root);
        root.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        RectTransform box = Panel("Box", root, new Color(.22f, .31f, .4f));
        box.anchorMin = box.anchorMax = new Vector2(0, .5f); box.pivot = new Vector2(0, .5f); box.sizeDelta = new Vector2(30, 30);
        RectTransform check = Panel("Checkmark", box, Accent); Stretch(check); check.offsetMin = new Vector2(6, 6); check.offsetMax = new Vector2(-6, -6);
        var toggle = root.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = box.GetComponent<Image>(); toggle.graphic = check.GetComponent<Image>();
        toggle.interactable = enabled;
        toggle.SetIsOnWithoutNotify(key != null && values.TryGetValue(key, out float value) && value != 0);
        if (key != null) toggle.onValueChanged.AddListener(on => values[key] = on ? 1 : 0);
        Text text = Label(root, caption, 21); Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(38, 0);
        // Include the label in the toggle's click target.
        root.gameObject.AddComponent<Image>().color = Color.clear;
        return root;
    }

    private RectTransform SliderControl(Transform parent, string key, string caption, int min, int max, int initial, int[] choices = null)
    {
        RectTransform root = Rect(caption, parent); Stretch(root);
        root.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
        Text text = Label(root, caption, 21); Stretch(text.rectTransform); text.rectTransform.anchorMin = new Vector2(0, .47f);
        RectTransform track = Panel("Slider", root, new Color(.22f, .31f, .4f));
        track.anchorMin = new Vector2(0, .2f); track.anchorMax = new Vector2(1, .2f);
        track.sizeDelta = new Vector2(-24, 14); track.anchoredPosition = Vector2.zero;
        RectTransform handleArea = Rect("Handle Area", track); Stretch(handleArea);
        RectTransform handle = Panel("Handle", handleArea, Accent); handle.sizeDelta = new Vector2(24, 30);
        var slider = track.gameObject.AddComponent<Slider>(); slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
        slider.minValue = min; slider.maxValue = max; slider.wholeNumbers = true; slider.interactable = key != null;
        float value = key != null && values.TryGetValue(key, out float stored) ? stored : initial;
        slider.SetValueWithoutNotify(value);
        System.Action<float> refresh = v => text.text = key == null ? caption : caption + "  " + (choices != null ? choices[Mathf.RoundToInt(v)] : Mathf.RoundToInt(v));
        refresh(slider.value);
        if (key != null)
        {
            values[key] = slider.value;
            slider.onValueChanged.AddListener(v => { values[key] = v; refresh(v); });
        }
        return root;
    }

    private RectTransform Rect(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform)); obj.layer = 5;
        obj.transform.SetParent(parent, false); return obj.GetComponent<RectTransform>();
    }
    private RectTransform Panel(string name, Transform parent, Color color)
    {
        RectTransform rect = Rect(name, parent); rect.gameObject.AddComponent<Image>().color = color; return rect;
    }
    private Text Label(Transform parent, string caption, int size)
    {
        var text = Rect("Label", parent).gameObject.AddComponent<Text>();
        text.font = font; text.text = caption; text.fontSize = size; text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
        text.supportRichText = true; text.resizeTextForBestFit = true; text.resizeTextMinSize = 16; text.resizeTextMaxSize = size;
        return text;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
    }
}

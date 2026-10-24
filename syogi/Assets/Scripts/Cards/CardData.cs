using System;

// IDs identify parameters, not individual cards. Keep existing IDs stable.
public static class CardParameterIds
{
    public const string BulletSpeed = "BulletSpeed";
    public const string BulletRadius = "BulletRadius";
    public const string BulletLifetime = "BulletLifetime";
    public const string FireInterval = "FireInterval";
    public const string BulletCount = "BulletCount";
    public const string FormationAngleStart = "FormationAngleStart";
    public const string FormationAngleEnd = "FormationAngleEnd";
    public const string AngularSpeedStart = "AngularSpeedStart";
    public const string AngularSpeedEnd = "AngularSpeedEnd";
    public const string SpeedOffsetStart = "SpeedOffsetStart";
    public const string SpeedOffsetEnd = "SpeedOffsetEnd";
    public const string PowerSlopeStart = "PowerSlopeStart";
    public const string PowerSlopeEnd = "PowerSlopeEnd";
    public const string PowerConstantStart = "PowerConstantStart";
    public const string PowerConstantEnd = "PowerConstantEnd";
    public const string SplitInterval = "SplitInterval";
    public static string GetLabel(string id)
    {
        switch (id)
        {
            case CardParameterIds.BulletSpeed: return "弾速";
            case CardParameterIds.BulletRadius: return "弾の半径";
            case CardParameterIds.BulletLifetime: return "弾寿命";
            case CardParameterIds.FireInterval: return "発射周期";
            case CardParameterIds.BulletCount: return "発射数";
            case CardParameterIds.FormationAngleStart: return "編隊角度（始点）";
            case CardParameterIds.FormationAngleEnd: return "編隊角度（終点）";
            case CardParameterIds.AngularSpeedStart: return "角速度（始点）";
            case CardParameterIds.AngularSpeedEnd: return "角速度（終点）";
            case CardParameterIds.SpeedOffsetStart: return "弾速差（始点）";
            case CardParameterIds.SpeedOffsetEnd: return "弾速差（終点）";
            case CardParameterIds.PowerSlopeStart: return "威力・時間係数（始点）";
            case CardParameterIds.PowerSlopeEnd: return "威力・時間係数（終点）";
            case CardParameterIds.PowerConstantStart: return "威力・定数（始点）";
            case CardParameterIds.PowerConstantEnd: return "威力・定数（終点）";
            case CardParameterIds.SplitInterval: return "分裂間隔";
            default: return id ?? "未設定";
        }
    }
}

public enum CardDirectionBasis { Aim, Movement }
public enum CardFormationInterpolation { Linear }

[Serializable]
public sealed class CardInitialSettings
{
    public CardDirectionBasis directionBasis = CardDirectionBasis.Aim;
    public CardFormationInterpolation interpolation = CardFormationInterpolation.Linear;
    // The source defines no split delay or child firing structure.
    public bool hasSplitDelay;
    public float splitDelay;
    public bool hasChildFiringStructure;
    // Optional legacy settings reference. CardCatalog.ChildStructure holds the
    // complete child definition (including independent numeric parameters).
#if UNITY_5_3_OR_NEWER
    [UnityEngine.SerializeReference]
#endif
    public CardInitialSettings ChildStructure = null;
}

[Serializable]
public sealed class CardParameterValue
{
    public string ID;
    // true: apply this parameter's modifiers to parent; false: apply to child.
    public bool isToParent = false;
    public float initialValue;
    public float mod;

    public float Value => initialValue + mod;
}

[Serializable]
public sealed class CardItem
{
    public string ID;
    public float mod;
}

[Serializable]
public sealed class CardData
{
    public string displayName;
    public CardItem[] items = Array.Empty<CardItem>();
}

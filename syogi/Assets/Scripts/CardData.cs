using System;

// IDs identify parameters, not individual cards. Keep existing IDs stable.
public static class CardParameterIds
{
    public const string BulletSpeed = "BulletSpeed";
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
}

[Serializable]
public sealed class CardParameterValue
{
    public string ID;
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

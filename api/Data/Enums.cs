namespace BulletinBoard.Api.Data;

public static class Roles
{
    public const string User = "user";
    public const string Moderator = "moderator";
    public const string Admin = "admin";
}

/// <summary>
/// Whether a member may use the board. Everyone who signs in gets an account,
/// but an admin decides who becomes a member — the board is for one village.
///
/// Pending is first on purpose: it is the CLR default, so a newly constructed
/// AppUser is pending without anyone having to remember to set it.
/// </summary>
public enum AccessStatus
{
    Pending,
    Approved,
    Rejected
}

public enum EventStatus
{
    Pending,
    Published,
    Rejected
}

/// <summary>Who the event comes from. Fixed list so the board stays browsable.</summary>
public enum EventCategory
{
    Verwaltung,
    Vereine,
    Kirchen,
    Stadt,
    Sonstiges
}

public enum ServiceType
{
    Seeking,   // "ich suche"
    Offering   // "ich biete"
}

public enum ServiceStatus
{
    Active,
    Closed
}

public enum GoodsStatus
{
    Active,
    Sold,
    Withdrawn
}

public enum GoodsCondition
{
    New,
    LikeNew,
    Good,
    Used,
    Defective
}

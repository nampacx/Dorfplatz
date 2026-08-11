namespace BulletinBoard.Api.Data;

public static class Roles
{
    public const string User = "user";
    public const string Moderator = "moderator";
    public const string Admin = "admin";
}

public enum EventStatus
{
    Pending,
    Published,
    Rejected
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

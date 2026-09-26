namespace Community_C.Utility.Security;

public static class BoardPermissions
{
    public const string Writer = "writer";

    public static bool CanWrite(string? permission)
    {
        return string.Equals(permission, Writer, StringComparison.Ordinal);
    }
}

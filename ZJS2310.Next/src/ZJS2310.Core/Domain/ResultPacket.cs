namespace ZJS2310.Core.Domain;

public sealed record ResultPacket(
    int TestCode,
    IReadOnlyList<double> Values,
    string RawMessage);

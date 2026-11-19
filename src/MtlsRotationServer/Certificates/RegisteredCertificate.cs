namespace MtlsRotationServer.Certificates;

public sealed class RegisteredCertificate
{
    public required string ClientId { get; init; }
    public required string FingerprintSha256 { get; init; }
    public required DateTimeOffset NotAfter { get; init; }
}

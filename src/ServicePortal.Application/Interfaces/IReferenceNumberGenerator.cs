namespace ServicePortal.Application.Interfaces;

public interface IReferenceNumberGenerator
{
    Task<string> GenerateNextReferenceAsync(CancellationToken cancellationToken = default);
}

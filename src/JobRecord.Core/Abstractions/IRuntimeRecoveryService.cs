using JobRecord.Core.Dtos;

namespace JobRecord.Core.Abstractions;

public interface IRuntimeRecoveryService
{
    Task<RuntimeRecoveryResult> RecoverAsync(CancellationToken cancellationToken = default);
}

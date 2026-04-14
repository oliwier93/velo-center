using VeloCenter.Core.Maintenance;

namespace VeloCenter.Infrastructure.Maintenance;

public sealed class NoOpApplicationResetService : IApplicationResetService
{
    public void ResetAllData()
    {
    }
}

using Truvio.Commerce.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Queries;

public sealed class StarterConfigQuery : DataQueryModelBase<StarterConfigModel>
{
    public override StarterConfigModel? GetModel() => new();
}

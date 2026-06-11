using Truvio.Commerce.Serializer.AdminUI.Models;
using Dynamicweb.CoreUI.Data;

namespace Truvio.Commerce.Serializer.AdminUI.Queries;

/// <summary>
/// Query behind the read-only "Excluded fields" SlideOver (CarveOutDetailScreen). Carries the
/// type/predicate name plus the exclusion kind so the model knows which config dict to read.
/// </summary>
public sealed class CarveOutDetailQuery : DataQueryModelBase<CarveOutDetailModel>
{
    public string TypeName { get; set; } = "";

    /// <summary>One of CarveOutDetailModel.Kind* constants.</summary>
    public string Kind { get; set; } = "";

    /// <summary>For Kind=Page: shows every carve-out on the page in one panel.</summary>
    public int PageId { get; set; }

    public override CarveOutDetailModel? GetModel()
        => Kind == CarveOutDetailModel.KindPage
            ? CarveOutDetailModel.LoadForPage(PageId)
            : CarveOutDetailModel.Load(TypeName, Kind);
}

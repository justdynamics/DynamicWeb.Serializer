namespace Truvio.Commerce.Serializer.Providers.SqlTable;

/// <summary>
/// Ensures custom columns defined in <c>EcomProductField</c> exist on the <c>EcomProducts</c>
/// table (LRN-hosted-publish-01). Custom product fields are column-backed: each
/// <c>EcomProductField</c> definition row corresponds to a physical column on
/// <c>EcomProducts</c>. A SqlTable predicate faithfully deserializes the definition rows but
/// never creates the columns, so the target ends up with field definitions whose columns do
/// not exist — every product read throws and index builds silently produce 0 documents.
///
/// This subclass points the shared <see cref="CustomColumnSchemaSync"/> routine at the
/// product-field definition table and its backing table. The orchestrator runs it right after
/// the <c>EcomProductField</c> entry deserializes, so the columns exist BEFORE the
/// <c>EcomProducts</c> predicate (ordered later) writes its rows — otherwise the custom-column
/// values would be silently dropped.
///
/// Note: product *category* fields (<c>EcomProductCategoryField</c>) are row-backed — their
/// values live in <c>EcomProductCategoryFieldValue</c>, need no DDL, and are not exposed to
/// this bug. Only the column-backed <c>EcomProductField</c> path needs this sync.
/// </summary>
public class EcomProductFieldSchemaSync : CustomColumnSchemaSync
{
    public EcomProductFieldSchemaSync(ISqlExecutor sqlExecutor) : base(sqlExecutor)
    {
    }

    protected override string FieldTable => "EcomProductField";
    protected override string SystemNameColumn => "ProductFieldSystemName";
    protected override string TypeIdColumn => "ProductFieldTypeID";
    protected override string TargetTable => "EcomProducts";
}

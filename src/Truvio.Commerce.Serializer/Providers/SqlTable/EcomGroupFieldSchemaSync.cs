namespace Truvio.Commerce.Serializer.Providers.SqlTable;

/// <summary>
/// Ensures custom columns defined in <c>EcomProductGroupField</c> exist on the
/// <c>EcomGroups</c> table. See <see cref="CustomColumnSchemaSync"/> for the shared routine;
/// this subclass simply points it at the group-field definition table and its backing table.
/// </summary>
public class EcomGroupFieldSchemaSync : CustomColumnSchemaSync
{
    public EcomGroupFieldSchemaSync(ISqlExecutor sqlExecutor) : base(sqlExecutor)
    {
    }

    protected override string FieldTable => "EcomProductGroupField";
    protected override string SystemNameColumn => "ProductGroupFieldSystemName";
    protected override string TypeIdColumn => "ProductGroupFieldTypeID";
    protected override string TargetTable => "EcomGroups";
}

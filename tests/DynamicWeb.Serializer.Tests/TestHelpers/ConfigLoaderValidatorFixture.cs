using DynamicWeb.Serializer.Configuration;

namespace DynamicWeb.Serializer.Tests.TestHelpers;

/// <summary>
/// Base class for any test class that calls <see cref="ConfigLoader.Load(string)"/>
/// (the 1-arg production overload) with a config containing SqlTable predicates.
///
/// Installs a permissive <see cref="ConfigLoader.TestOverrideIdentifierValidator"/> in the
/// ctor and clears it in Dispose, on a per-test-class basis. xUnit creates a fresh instance
/// of the test class per test method, so each test method gets its own AsyncLocal install
/// inside its own async flow — no leakage between parallel tests.
///
/// The allowlist is the UNION of every table/column referenced in any test that calls the
/// 1-arg Load overload with a SqlTable config. See Phase 37-06-PLAN.md §interfaces audit.
/// </summary>
public abstract class ConfigLoaderValidatorFixtureBase : IDisposable
{
    protected ConfigLoaderValidatorFixtureBase()
    {
        ConfigLoader.TestOverrideIdentifierValidator = new SqlIdentifierValidator(
            tableLoader: () => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "EcomShops",
                "EcomOrderFlow",
                "EcomOrderFlowV2",
                "EcomShippings",
                "EcomPayments",
                "AccessUser"
            },
            columnLoader: _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                // AccessUser family
                "AccessUserType", "AccessUserUserName", "AccessUserPassword",
                "AccessUserHostingId", "AccessUserHostingName",
                // OrderFlow family
                "OrderFlowName", "OrderFlowDescription", "OrderFlowID", "OrderFlowOrderStateID",
                // Shipping family
                "ShippingName", "ShippingXml", "SettingsXml", "ConfigXml",
                // Generic from PredicateCommandTests (Save_SqlTable_* round-trips)
                "LastModified", "Col1", "Col2", "Col3"
            });
    }

    public virtual void Dispose()
    {
        ConfigLoader.TestOverrideIdentifierValidator = null;
        GC.SuppressFinalize(this);
    }
}

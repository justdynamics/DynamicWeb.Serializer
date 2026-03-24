using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.EventEmitters;

namespace DynamicWeb.Serializer.Infrastructure;

public class ForceStringScalarEmitter : ChainedEventEmitter
{
    public ForceStringScalarEmitter(IEventEmitter next) : base(next) { }

    public override void Emit(ScalarEventInfo eventInfo, IEmitter emitter)
    {
        if (eventInfo.Source.Type == typeof(string) && eventInfo.Source.Value is string value)
        {
            // Use Literal block style for LF-only multiline strings (YAML spec preserves LF in literal blocks)
            // Use DoubleQuoted for strings containing \r (CRLF or CR alone) because YAML literal block
            // scalars normalize \r\n to \n — DoubleQuoted escapes \r correctly as a backslash escape
            if (value.Contains('\n') && !value.Contains('\r'))
                eventInfo.Style = ScalarStyle.Literal;
            else
                eventInfo.Style = ScalarStyle.DoubleQuoted;
        }
        base.Emit(eventInfo, emitter);
    }
}

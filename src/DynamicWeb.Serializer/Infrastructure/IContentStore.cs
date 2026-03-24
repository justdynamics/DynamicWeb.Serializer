using DynamicWeb.Serializer.Models;

namespace DynamicWeb.Serializer.Infrastructure;

public interface IContentStore
{
    void WriteTree(SerializedArea area, string rootDirectory);
    SerializedArea ReadTree(string rootDirectory);
}

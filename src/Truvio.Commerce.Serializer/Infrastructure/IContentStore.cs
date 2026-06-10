using Truvio.Commerce.Serializer.Models;

namespace Truvio.Commerce.Serializer.Infrastructure;

public interface IContentStore
{
    void WriteTree(SerializedArea area, string rootDirectory);
    SerializedArea ReadTree(string rootDirectory, string? areaName = null);
}

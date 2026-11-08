using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;

// Read the audited bytes without loading or running the application assembly.
using var file = File.OpenRead(args.Single());
var sha256 = Convert.ToHexString(SHA256.HashData(file));
file.Position = 0;
using var pe = new PEReader(file);
var reader = pe.GetMetadataReader();
var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
{
    var attribute = reader.GetCustomAttribute(handle);
    if (attribute.Constructor.Kind != HandleKind.MemberReference)
        continue;

    var constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
    if (constructor.Parent.Kind != HandleKind.TypeReference)
        continue;

    var type = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
    if (reader.GetString(type.Namespace) != "System.Reflection" ||
        type.ResolutionScope.Kind != HandleKind.AssemblyReference ||
        reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope).Name) != "System.Runtime")
        continue;

    var name = reader.GetString(type.Name);
    var isMetadata = name == "AssemblyMetadataAttribute";
    if (!isMetadata && name != "AssemblyInformationalVersionAttribute")
        continue;

    byte[] signature = isMetadata ? [0x20, 2, 1, 0x0e, 0x0e] : [0x20, 1, 1, 0x0e];
    var blob = reader.GetBlobReader(attribute.Value);
    if (reader.GetString(constructor.Name) != ".ctor" ||
        !reader.GetBlobBytes(constructor.Signature).AsSpan().SequenceEqual(signature) ||
        blob.ReadUInt16() != 1)
        throw new BadImageFormatException("Invalid assembly build attribute.");

    var key = isMetadata ? blob.ReadSerializedString() : "InformationalVersion";
    var value = blob.ReadSerializedString();
    if (key is null || value is null || blob.ReadUInt16() != 0 || blob.RemainingBytes != 0)
        throw new BadImageFormatException("Invalid assembly build attribute value.");

    var isBuildField = key.Equals("BuildCommit", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("BuildDirtyState", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("ProductVersion", StringComparison.OrdinalIgnoreCase) ||
        (!isMetadata && key == "InformationalVersion");
    if (isBuildField && !values.TryAdd(key, value))
        return 1; // Duplicate values are ambiguous, even if identical.
}

string[] required = ["BuildCommit", "BuildDirtyState", "ProductVersion", "InformationalVersion"];
if (required.Any(key => !values.ContainsKey(key)))
    return 1;

using (var json = new Utf8JsonWriter(Console.OpenStandardOutput()))
{
    json.WriteStartObject();
    json.WriteString("SHA256", sha256);
    foreach (var key in required)
        json.WriteString(key, values[key]);
    json.WriteString("PeProductVersion", FileVersionInfo.GetVersionInfo(args[0]).ProductVersion);
    json.WriteEndObject();
}
Console.WriteLine();
return 0;

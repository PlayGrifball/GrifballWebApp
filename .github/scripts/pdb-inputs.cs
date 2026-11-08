// pdb-inputs.cs <file.pdb>: what a portable PDB records of its compilation, one line each: every source document and
// its checksum, the compiler options, and every metadata reference (name, MVID). Two builds of an assembly that differ
// only in its hashes differ in one of these; diff-oci.py saves both PDBs and docker-publish.yml diffs this output.
//   dotnet run --file .github/scripts/pdb-inputs.cs -- a.pdb
using System.Reflection.Metadata;
using System.Text;

using var stream = File.OpenRead(args[0]);
using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
var reader = provider.GetMetadataReader();

foreach (var handle in reader.Documents)
{
    var document = reader.GetDocument(handle);
    Console.WriteLine($"document {reader.GetString(document.Name)} {Convert.ToHexString(reader.GetBlobBytes(document.Hash))}");
}

var options = new Guid("B5FEEC05-8CD0-4A83-96DA-466284BB4BD8");
var references = new Guid("7E4D4708-096E-4C5C-AEDA-CB10BA6A740D");
foreach (var handle in reader.CustomDebugInformation)
{
    var information = reader.GetCustomDebugInformation(handle);
    var kind = reader.GetGuid(information.Kind);
    var blob = reader.GetBlobBytes(information.Value);
    if (kind == options)
    {
        // name\0value\0 pairs
        var parts = Encoding.UTF8.GetString(blob).Split('\0');
        for (var i = 0; i + 1 < parts.Length; i += 2)
            Console.WriteLine($"option {parts[i]}={parts[i + 1]}");
    }
    else if (kind == references)
    {
        // per reference: name\0 aliases\0 kind(1) timestamp(4) size(4) MVID(16)
        var at = 0;
        while (at < blob.Length)
        {
            var name = ReadString(blob, ref at);
            var aliases = ReadString(blob, ref at);
            at += 1;
            var timestamp = BitConverter.ToUInt32(blob, at);
            var size = BitConverter.ToUInt32(blob, at + 4);
            var mvid = new Guid(blob.AsSpan(at + 8, 16));
            at += 24;
            Console.WriteLine($"reference {name} {aliases} {timestamp:x8} {size} {mvid}");
        }
    }
}

static string ReadString(byte[] blob, ref int at)
{
    var end = Array.IndexOf(blob, (byte)0, at);
    var value = Encoding.UTF8.GetString(blob, at, end - at);
    at = end + 1;
    return value;
}

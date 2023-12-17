using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Serialization;

string ConfigFileName = Path.Join(AppDomain.CurrentDomain.BaseDirectory, "config.json");

if (args.Length == 0)
{
    return;
}

var config = File.Exists(ConfigFileName) ? JsonSerializer.Deserialize(File.ReadAllBytes(ConfigFileName), SerializerContexts.Default.Configuration) ?? Configuration.Default : Configuration.Default;

var targetFormat = config.Format switch
{
    Format.Png => ImageFormat.Png,
    Format.Jpeg => ImageFormat.Jpeg,
    _ => throw new Exception("Unknown Format")
};

var codec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(x => x.FormatID == targetFormat.Guid);
if (codec == null)
    throw new Exception("Codec not found");

using var parameters = new EncoderParameters(1);
parameters.Param[0] = new EncoderParameter(Encoder.Quality, Math.Clamp(config.Quality, 0, 100));

foreach (var path in args)
{
    try
    {
        using var image = Image.FromFile(path);

        (int Width, int Height) size = (image.Width > image.Height) switch
        {
            true => (config.MaxResolution, (int)(image.Height * ((double)config.MaxResolution / image.Width))),
            false => ((int)(image.Width * ((double)config.MaxResolution / image.Height)), config.MaxResolution),
        };

        using var destination = new Bitmap(size.Width, size.Height);
        using var g = Graphics.FromImage(destination);
        g.DrawImage(image, 0, 0, size.Width, size.Height);

        var destinationPath = Path.Join(Path.GetDirectoryName(path.AsSpan()), $"{Path.GetFileNameWithoutExtension(path.AsSpan())}_{size.Width}x{size.Height}");
        var origDestinationPath = destinationPath;
        int idx = 2;
        while (File.Exists($"{destinationPath}.{config.Format.GetExtension()}"))
        {
            destinationPath = $"{origDestinationPath}.{idx++}";
        }
        Console.WriteLine($"{path} => {destinationPath}.{config.Format.GetExtension()}");

        destination.Save($"{destinationPath}.{config.Format.GetExtension()}", codec, parameters);
    }
    catch (Exception e)
    {
        Console.Error.WriteLine(e.ToString());
    }
}
using var fs = File.Create(ConfigFileName);
using var writer = new Utf8JsonWriter(fs, new JsonWriterOptions() { Indented = true });
JsonSerializer.Serialize(writer, config, SerializerContexts.Default.Configuration);
writer.Flush();

public sealed class Configuration
{
    public static readonly Configuration Default = new Configuration() { };

    [JsonConverter(typeof(JsonStringEnumConverter<Format>))]
    public Format Format { get; set; } = Format.Jpeg;

    public int MaxResolution { get; set; } = 1024;

    public int Quality { get; set; } = 80;
}

[JsonSerializable(typeof(Configuration))]
internal partial class SerializerContexts : JsonSerializerContext;

public enum Format
{
    Jpeg,
    Png,
}

public static class FormatExtension
{
    public static string GetExtension(this Format format) => format switch
    {
        Format.Jpeg => "jpg",
        Format.Png => "png",
        _ => null!
    };
}
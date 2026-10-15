using PostyFox.Application.Connectors;
using Xunit;

namespace PostyFox.Application.Tests;

public class MediaFramingValidationTests
{
    private static MediaRef With(MediaFraming? framing) => new("media", "u1/a/pic.png", "image/png", Framing: framing);

    public static TheoryData<MediaFraming?, bool> Cases => new()
    {
        { null, true },
        { new MediaFraming(MediaFramingMode.Pad), true },
        { new MediaFraming(MediaFramingMode.Pad, "#a1B2c3"), true },
        { new MediaFraming(MediaFramingMode.Pad, "red"), false },
        { new MediaFraming(MediaFramingMode.Pad, "#abc"), false },
        { new MediaFraming(MediaFramingMode.Crop, Crop: new CropRegion(0, 0, 1, 1)), true },
        { new MediaFraming(MediaFramingMode.Crop, Crop: new CropRegion(0.25, 0.1, 0.5, 0.8)), true },
        { new MediaFraming(MediaFramingMode.Crop), false },
        { new MediaFraming(MediaFramingMode.Crop, Crop: new CropRegion(-0.1, 0, 0.5, 0.5)), false },
        { new MediaFraming(MediaFramingMode.Crop, Crop: new CropRegion(0, 0, 0, 0.5)), false },
        { new MediaFraming(MediaFramingMode.Crop, Crop: new CropRegion(0.6, 0, 0.6, 1)), false },
        { new MediaFraming((MediaFramingMode)7), false },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void HasValidFraming_accepts_only_well_formed_choices(MediaFraming? framing, bool expected) =>
        Assert.Equal(expected, With(framing).HasValidFraming());
}

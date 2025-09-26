using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WootMouseRemap.Core;
using WootMouseRemap.Core.Mapping;

namespace WootMouseRemap.Tests;

[TestClass]
public sealed class ProfileAdaptersTests
{
    [TestMethod]
    public void MapFromConfigurationProfile_MapsCurvesAndDpi()
    {
        var cfg = new ConfigurationProfile
        {
            Name = "Cfg1",
            MouseDpi = 2400,
            Curves = new ResponseCurves
            {
                Sensitivity = 2.5f,
                Expo = 0.75f,
                Deadzone = 0.01f,
                MaxOutput = 1.2f,
                EmaAlpha = 0.3f,
                ScaleX = 1.1f,
                ScaleY = 0.9f
            }
        };

        var mapped = ProfileAdapters.MapFromConfigurationProfile(cfg);

        Assert.IsNotNull(mapped);
        Assert.AreEqual(cfg.Name, mapped.Name);
        Assert.AreEqual(cfg.MouseDpi, mapped.MouseDpi);
        Assert.AreEqual(cfg.Curves.Sensitivity, mapped.CurveSettings.Sensitivity, 0.0001f);
        Assert.AreEqual(cfg.Curves.Expo, mapped.CurveSettings.Expo, 0.0001f);
    }

    [TestMethod]
    public void MapToConfigurationProfile_RoundTripsCurvesAndDpi()
    {
        var input = new InputMappingProfile
        {
            Name = "IM1",
            MouseDpi = 1200,
            CurveSettings = new CurveSettings
            {
                Sensitivity = 0.8f,
                Expo = 0.2f,
                AntiDeadzone = 0.02f,
                MaxSpeed = 1.5f,
                EmaAlpha = 0.4f,
                ScaleX = 1.0f,
                ScaleY = 1.0f
            }
        };

        var cfg = ProfileAdapters.MapToConfigurationProfile(input);

        Assert.IsNotNull(cfg);
        Assert.AreEqual(input.Name, cfg.Name);
        Assert.AreEqual(input.MouseDpi, cfg.MouseDpi);
        Assert.AreEqual(input.CurveSettings.Sensitivity, cfg.Curves.Sensitivity, 0.0001f);
        Assert.AreEqual(input.CurveSettings.Expo, cfg.Curves.Expo, 0.0001f);
    }
}

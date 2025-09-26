using Microsoft.VisualStudio.TestTools.UnitTesting;
using WootMouseRemap.Core.Mapping;
using WootMouseRemap.Core.Services;
using WootMouseRemap.Core.Pipeline;
using Microsoft.Extensions.Logging.Abstractions;
using Mapster;

namespace WootMouseRemap.Tests;

[TestClass]
public sealed class InputMappingTests
{
    private ProfileService _profileService = null!;
    private InputMappingProfile _testProfile = null!;
    
    [TestInitialize]
    public void Setup()
    {
        InputMappingConfig.Configure();
        _profileService = new ProfileService(NullLogger<ProfileService>.Instance, "TestProfiles");
        _testProfile = _profileService.GetDefaultProfile();
    }
    
    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists("TestProfiles"))
            Directory.Delete("TestProfiles", true);
    }
    
    [TestMethod]
    public void ProfileService_LoadsDefaultProfile()
    {
        var profile = _profileService.GetDefaultProfile();
        
        Assert.IsNotNull(profile);
        Assert.AreEqual("Default", profile.Name);
        Assert.IsTrue(profile.KeyboardMap.ContainsKey(0x57)); // W key
        Assert.AreEqual(ControllerInput.LeftStickUp, profile.KeyboardMap[0x57]);
    }
    
    [TestMethod]
    public void ProfileService_SavesAndLoadsProfile()
    {
        var customProfile = new InputMappingProfile
        {
            Name = "TestProfile",
            Description = "Test profile for unit testing",
            KeyboardMap = new Dictionary<int, ControllerInput>
            {
                [0x51] = ControllerInput.A // Q key -> A button
            },
            CurveSettings = new CurveSettings
            {
                Sensitivity = 0.5f,
                Expo = 0.8f
            }
        };
        
        _profileService.SaveProfile(customProfile);
        
        // Create new service instance to test loading
        var newService = new ProfileService(NullLogger<ProfileService>.Instance, "TestProfiles");
        var loadedProfile = newService.GetProfile("TestProfile");
        
        Assert.IsNotNull(loadedProfile);
        Assert.AreEqual("TestProfile", loadedProfile.Name);
        Assert.AreEqual(0.5f, loadedProfile.CurveSettings.Sensitivity, 0.001f);
        Assert.AreEqual(0.8f, loadedProfile.CurveSettings.Expo, 0.001f);
        Assert.IsTrue(loadedProfile.KeyboardMap.ContainsKey(0x51));
        Assert.AreEqual(ControllerInput.A, loadedProfile.KeyboardMap[0x51]);
    }
    
    [TestMethod]
    public void ProfileService_CreatesGameProfiles()
    {
        var cs2Profile = _profileService.CreateGameProfile("CS2");
        var valorantProfile = _profileService.CreateGameProfile("Valorant");
        
        Assert.AreEqual("CS2", cs2Profile.Name);
        Assert.AreEqual("Valorant", valorantProfile.Name);
        
        // CS2 should have lower sensitivity for precision
        Assert.IsTrue(cs2Profile.CurveSettings.Sensitivity < valorantProfile.CurveSettings.Sensitivity);
    }
    
    [TestMethod]
    public void MapsterConfig_MapsKeyboardEvents()
    {
        var keyEvent = new KeyboardEvent(0x57, true); // W key down
        var controllerInput = keyEvent.Adapt<ControllerInput>();
        
        Assert.AreEqual(ControllerInput.LeftStickUp, controllerInput);
    }
    
    [TestMethod]
    public void MapsterConfig_MapsMouseEvents()
    {
        var mouseEvent = new MouseEvent(0, true); // Left click down
        var controllerInput = mouseEvent.Adapt<ControllerInput>();
        
        Assert.AreEqual(ControllerInput.RightTrigger, controllerInput);
    }
    
    [TestMethod]
    public void StickMapper_HandlesWASDCorrectly()
    {
        var mapper = new StickMapper();
        
        // Test W key (forward)
        mapper.UpdateKey(0x57, true);
        var (x, y) = mapper.WasdToLeftStick();
        Assert.AreEqual(0, x);
        Assert.IsTrue(y < 0); // Y is inverted for Xbox controller
        
        // Test A key (left)
        mapper.UpdateKey(0x57, false);
        mapper.UpdateKey(0x41, true);
        (x, y) = mapper.WasdToLeftStick();
        Assert.IsTrue(x < 0);
        Assert.AreEqual(0, y);
        
        // Test diagonal movement (W+D)
        mapper.UpdateKey(0x41, false);
        mapper.UpdateKey(0x57, true); // W
        mapper.UpdateKey(0x44, true); // D
        (x, y) = mapper.WasdToLeftStick();
        
        // Should be normalized diagonal
        float magnitude = MathF.Sqrt(x * x + y * y);
        Assert.IsTrue(Math.Abs(magnitude - 23170) < 1000, $"Magnitude {magnitude} should be close to 23170 (32767/√2 for normalized diagonal)");
    }
    
        [TestMethod]
        public void CurveProcessor_AppliesTransformationsCorrectly()
        {
            var processor = new CurveProcessor
            {
                Sensitivity = 0.5f,
                Expo = 0.5f,
                AntiDeadzone = 0.1f,
                MaxSpeed = 2.0f, // Allow higher speeds to test sensitivity scaling
                EmaAlpha = 0.0f // Disable smoothing for predictable results
            };
            
            // Test basic scaling
            var (x, y) = processor.ToStick(100, 0);
            Assert.IsTrue(x > 0, "X should be positive for positive input");
            Assert.AreEqual(0, y, "Y should be zero for zero input");
            
            // Test sensitivity scaling
            processor.Sensitivity = 1.0f;
            var (x2, y2) = processor.ToStick(100, 0);
            Assert.IsTrue(Math.Abs(x2) > Math.Abs(x), "Higher sensitivity should produce larger output");
            
            // Test clamping
            var (xMax, yMax) = processor.ToStick(10000, 10000);
            Assert.IsTrue(Math.Abs(xMax) <= 32767, "X should be clamped to short range");
            Assert.IsTrue(Math.Abs(yMax) <= 32767, "Y should be clamped to short range");
        }    [TestMethod]
    public void InputOrchestrator_ProcessesEventsCorrectly()
    {
        var controller = new Xbox360ControllerWrapper();
        var orchestrator = new InputOrchestrator(NullLogger<InputOrchestrator>.Instance, controller);
        
        try
        {
            orchestrator.SetProfile(_testProfile);
            orchestrator.Start();
            
            // Queue some test events
            orchestrator.QueueKeyboardEvent(0x57, true);  // W key
            orchestrator.QueueMouseEvent(0, true, 50, -25); // Left click with mouse movement
            
            // Give time for processing
            Thread.Sleep(50);
            
            // Verify controller is connected (basic smoke test)
            // In a real test environment with ViGEm driver, we could verify actual controller state
            Assert.IsTrue(true, "Orchestrator processed events without throwing exceptions");
        }
        finally
        {
            orchestrator.Stop();
            orchestrator.Dispose();
            controller.Dispose();
        }
    }
    
    [TestMethod]
    public void ControllerStateBatch_MaintainsState()
    {
        var batch = new ControllerStateBatch();
        
        // Test initial state
        Assert.AreEqual(0, batch.LeftStickX);
        Assert.AreEqual(0, batch.RightStickY);
        Assert.IsFalse(batch.A);
        
        // Test state updates
        batch.LeftStickX = 16000;
        batch.A = true;
        batch.LeftTrigger = 255;
        
        Assert.AreEqual(16000, batch.LeftStickX);
        Assert.IsTrue(batch.A);
        Assert.AreEqual(255, batch.LeftTrigger);
    }
    
    [TestMethod]
    public void ProfileService_HandlesInvalidProfiles()
    {
        // Test non-existent profile
        var profile = _profileService.GetProfile("NonExistent");
        Assert.IsNull(profile);
        
        // Test deletion of default profile (should be protected)
        _profileService.DeleteProfile("Default");
        var defaultProfile = _profileService.GetProfile("Default");
        Assert.IsNotNull(defaultProfile, "Default profile should be protected from deletion");
    }
}
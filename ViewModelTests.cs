using System;
using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WootMouseRemap.Core;
using WootMouseRemap.Features;

namespace WootMouseRemap.Tests
{
    /// <summary>
    /// Unit tests for AntiRecoilViewModel
    /// </summary>
    [TestClass]
    public class ViewModelTests
    {
        private AntiRecoilViewModel _viewModel = null!;
        private AntiRecoil _antiRecoil = null!;

        [TestInitialize]
        public void Setup()
        {
            _viewModel = new AntiRecoilViewModel();
            _antiRecoil = new AntiRecoil();
        }

        [TestMethod]
        public void ViewModel_DefaultValues_ShouldMatchAntiRecoilDefaults()
        {
            _viewModel.LoadFrom(_antiRecoil);

            Assert.AreEqual(_antiRecoil.Enabled, _viewModel.Enabled, "Enabled should match");
            Assert.AreEqual(_antiRecoil.Strength, _viewModel.Strength, "Strength should match");
            Assert.AreEqual(_antiRecoil.VerticalThreshold, _viewModel.VerticalThreshold, "VerticalThreshold should match");
            Assert.AreEqual(_antiRecoil.ActivationDelayMs, _viewModel.ActivationDelayMs, "ActivationDelayMs should match");
            Assert.AreEqual(_antiRecoil.HorizontalCompensation, _viewModel.HorizontalCompensation, "HorizontalCompensation should match");
            Assert.AreEqual(_antiRecoil.AdaptiveCompensation, _viewModel.AdaptiveCompensation, "AdaptiveCompensation should match");
        }

        [TestMethod]
        public void ViewModel_PropertyChanged_ShouldFireForAllProperties()
        {
            var changedProperties = new List<string>();

            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.PropertyName))
                {
                    changedProperties.Add(e.PropertyName);
                }
            };

            // Test each property
            _viewModel.Enabled = !_viewModel.Enabled;
            _viewModel.Strength = 0.8f;
            _viewModel.VerticalThreshold = 5.0f;
            _viewModel.ActivationDelayMs = 100;
            _viewModel.HorizontalCompensation = 0.3f;
            _viewModel.AdaptiveCompensation = !_viewModel.AdaptiveCompensation;
            _viewModel.MaxTickCompensation = 15.0f;
            _viewModel.MaxTotalCompensation = 50.0f;
            _viewModel.CooldownMs = 200;
            _viewModel.DecayPerMs = 0.05f;

            Assert.IsTrue(changedProperties.Contains("Enabled"), "PropertyChanged should fire for Enabled");
            Assert.IsTrue(changedProperties.Contains("Strength"), "PropertyChanged should fire for Strength");
            Assert.IsTrue(changedProperties.Contains("VerticalThreshold"), "PropertyChanged should fire for VerticalThreshold");
            Assert.IsTrue(changedProperties.Contains("ActivationDelayMs"), "PropertyChanged should fire for ActivationDelayMs");
            Assert.IsTrue(changedProperties.Contains("HorizontalCompensation"), "PropertyChanged should fire for HorizontalCompensation");
            Assert.IsTrue(changedProperties.Contains("AdaptiveCompensation"), "PropertyChanged should fire for AdaptiveCompensation");
            Assert.IsTrue(changedProperties.Contains("MaxTickCompensation"), "PropertyChanged should fire for MaxTickCompensation");
            Assert.IsTrue(changedProperties.Contains("MaxTotalCompensation"), "PropertyChanged should fire for MaxTotalCompensation");
            Assert.IsTrue(changedProperties.Contains("CooldownMs"), "PropertyChanged should fire for CooldownMs");
            Assert.IsTrue(changedProperties.Contains("DecayPerMs"), "PropertyChanged should fire for DecayPerMs");
        }

        [TestMethod]
        public void ViewModel_IsDirty_ShouldTrackChanges()
        {
            _viewModel.LoadFrom(_antiRecoil);
            Assert.IsFalse(_viewModel.IsDirty, "Should not be dirty after loading");

            _viewModel.Strength = 0.8f;
            Assert.IsTrue(_viewModel.IsDirty, "Should be dirty after changing property");

            _viewModel.ApplyTo(_antiRecoil);
            Assert.IsFalse(_viewModel.IsDirty, "Should not be dirty after applying changes");
        }

        [TestMethod]
        public void ViewModel_ApplyTo_ShouldUpdateAntiRecoilSettings()
        {
            _viewModel.Enabled = true;
            _viewModel.Strength = 0.7f;
            _viewModel.VerticalThreshold = 3.5f;
            _viewModel.ActivationDelayMs = 150;
            _viewModel.HorizontalCompensation = 0.25f;
            _viewModel.AdaptiveCompensation = true;
            _viewModel.MaxTickCompensation = 12.0f;
            _viewModel.MaxTotalCompensation = 75.0f;
            _viewModel.CooldownMs = 300;
            _viewModel.DecayPerMs = 0.08f;

            _viewModel.ApplyTo(_antiRecoil);

            Assert.AreEqual(_viewModel.Enabled, _antiRecoil.Enabled, "Enabled should be applied");
            Assert.AreEqual(_viewModel.Strength, _antiRecoil.Strength, "Strength should be applied");
            Assert.AreEqual(_viewModel.VerticalThreshold, _antiRecoil.VerticalThreshold, "VerticalThreshold should be applied");
            Assert.AreEqual(_viewModel.ActivationDelayMs, _antiRecoil.ActivationDelayMs, "ActivationDelayMs should be applied");
            Assert.AreEqual(_viewModel.HorizontalCompensation, _antiRecoil.HorizontalCompensation, "HorizontalCompensation should be applied");
            Assert.AreEqual(_viewModel.AdaptiveCompensation, _antiRecoil.AdaptiveCompensation, "AdaptiveCompensation should be applied");
            Assert.AreEqual(_viewModel.MaxTickCompensation, _antiRecoil.MaxTickCompensation, "MaxTickCompensation should be applied");
            Assert.AreEqual(_viewModel.MaxTotalCompensation, _antiRecoil.MaxTotalCompensation, "MaxTotalCompensation should be applied");
            Assert.AreEqual(_viewModel.CooldownMs, _antiRecoil.CooldownMs, "CooldownMs should be applied");
            Assert.AreEqual(_viewModel.DecayPerMs, _antiRecoil.DecayPerMs, "DecayPerMs should be applied");
        }

        [TestMethod]
        public void ViewModel_PropertyValidation_ShouldEnforceConstraints()
        {
            // Test strength bounds
            _viewModel.Strength = -0.5f;
            Assert.IsTrue(_viewModel.Strength >= 0, "Strength should be non-negative");

            _viewModel.Strength = 1.5f;
            Assert.IsTrue(_viewModel.Strength <= 1.0f, "Strength should be <= 1.0");

            // Test threshold bounds
            _viewModel.VerticalThreshold = -1.0f;
            Assert.IsTrue(_viewModel.VerticalThreshold >= 0, "VerticalThreshold should be non-negative");

            // Test activation delay bounds
            _viewModel.ActivationDelayMs = -100;
            Assert.IsTrue(_viewModel.ActivationDelayMs >= 0, "ActivationDelayMs should be non-negative");

            // Test horizontal compensation bounds
            _viewModel.HorizontalCompensation = -0.5f;
            Assert.IsTrue(_viewModel.HorizontalCompensation >= 0, "HorizontalCompensation should be non-negative");

            _viewModel.HorizontalCompensation = 1.5f;
            Assert.IsTrue(_viewModel.HorizontalCompensation <= 1.0f, "HorizontalCompensation should be <= 1.0");

            // Test advanced property bounds
            _viewModel.MaxTickCompensation = -5.0f;
            Assert.IsTrue(_viewModel.MaxTickCompensation >= 0, "MaxTickCompensation should be non-negative");

            _viewModel.MaxTotalCompensation = -10.0f;
            Assert.IsTrue(_viewModel.MaxTotalCompensation >= 0, "MaxTotalCompensation should be non-negative");

            _viewModel.CooldownMs = -50;
            Assert.IsTrue(_viewModel.CooldownMs >= 0, "CooldownMs should be non-negative");

            _viewModel.DecayPerMs = -0.1f;
            Assert.IsTrue(_viewModel.DecayPerMs >= 0, "DecayPerMs should be non-negative");
        }

        [TestMethod]
        public void ViewModel_Reset_ShouldRestoreDefaults()
        {
            // Modify all properties
            _viewModel.Enabled = true;
            _viewModel.Strength = 0.9f;
            _viewModel.VerticalThreshold = 10.0f;
            _viewModel.ActivationDelayMs = 500;
            _viewModel.HorizontalCompensation = 0.8f;
            _viewModel.AdaptiveCompensation = true;
            _viewModel.MaxTickCompensation = 20.0f;
            _viewModel.MaxTotalCompensation = 100.0f;
            _viewModel.CooldownMs = 1000;
            _viewModel.DecayPerMs = 0.2f;

            // Reset by loading from a default AntiRecoil instance
            var defaultAntiRecoil = new AntiRecoil();
            _viewModel.LoadFrom(defaultAntiRecoil);

            Assert.AreEqual(defaultAntiRecoil.Enabled, _viewModel.Enabled, "Enabled should be reset to default");
            Assert.AreEqual(defaultAntiRecoil.Strength, _viewModel.Strength, "Strength should be reset to default");
            Assert.AreEqual(defaultAntiRecoil.VerticalThreshold, _viewModel.VerticalThreshold, "VerticalThreshold should be reset to default");
            Assert.AreEqual(defaultAntiRecoil.ActivationDelayMs, _viewModel.ActivationDelayMs, "ActivationDelayMs should be reset to default");
            Assert.AreEqual(defaultAntiRecoil.HorizontalCompensation, _viewModel.HorizontalCompensation, "HorizontalCompensation should be reset to default");
            Assert.AreEqual(defaultAntiRecoil.AdaptiveCompensation, _viewModel.AdaptiveCompensation, "AdaptiveCompensation should be reset to default");
        }

        [TestMethod]
        public void ViewModel_LoadFromNullAntiRecoil_ShouldNotThrow()
        {
            // LoadFrom handles null gracefully by returning
            _viewModel.LoadFrom(null);
            // No assertion needed, just ensure no exception
        }

        [TestMethod]
        public void ViewModel_ApplyToNullAntiRecoil_ShouldNotThrow()
        {
            // ApplyTo handles null gracefully by returning
            _viewModel.ApplyTo(null);
            // No assertion needed, just ensure no exception
        }

        [TestMethod]
        public void ViewModel_PropertyChangedDoesNotFireForSameValue()
        {
            var strengthChangeCount = 0;
            _viewModel.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.Strength))
                    strengthChangeCount++;
            };

            var originalStrength = _viewModel.Strength;

            // Set to same value
            _viewModel.Strength = originalStrength;

            Assert.AreEqual(0, strengthChangeCount, "PropertyChanged should not fire when setting Strength to same value");

            // Set to different value
            _viewModel.Strength = originalStrength + 0.1f;

            Assert.AreEqual(1, strengthChangeCount, "PropertyChanged should fire when setting Strength to different value");
        }
    }
}

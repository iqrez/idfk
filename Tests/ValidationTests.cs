using System;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WootMouseRemap.Features;
using WootMouseRemap.UI;

namespace WootMouseRemap.Tests
{
    /// <summary>
    /// Unit tests for validation system and rules
    /// </summary>
    [TestClass]
    public class ValidationTests
    {
        private AntiRecoilViewModel _viewModel;
        private NumericUpDown _testControl;

        [TestInitialize]
        public void Setup()
        {
            _viewModel = new AntiRecoilViewModel();
            _testControl = new NumericUpDown();
        }

        [TestMethod]
        public void ValidationRule_StrengthZeroWhenEnabled_ShouldReturnWarning()
        {
            _viewModel.Enabled = true;
            _viewModel.Strength = 0.0f;

            var rule = AntiRecoilValidationRules.CreateStrengthValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Warning, result.Type, "Should return warning for zero strength when enabled");
            Assert.IsTrue(result.Message.Contains("no compensation"), "Warning message should mention no compensation");
        }

        [TestMethod]
        public void ValidationRule_StrengthVeryHigh_ShouldReturnWarning()
        {
            _viewModel.Enabled = true;
            _viewModel.Strength = 0.95f;

            var rule = AntiRecoilValidationRules.CreateStrengthValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Warning, result.Type, "Should return warning for very high strength");
            Assert.IsTrue(result.Message.Contains("overcorrection"), "Warning message should mention overcorrection");
        }

        [TestMethod]
        public void ValidationRule_StrengthNormal_ShouldReturnSuccess()
        {
            _viewModel.Enabled = true;
            _viewModel.Strength = 0.5f;

            var rule = AntiRecoilValidationRules.CreateStrengthValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Success, result.Type, "Should return success for normal strength");
        }

        [TestMethod]
        public void ValidationRule_ThresholdTooHigh_ShouldReturnWarning()
        {
            _viewModel.VerticalThreshold = 25.0f;

            var rule = AntiRecoilValidationRules.CreateThresholdValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Warning, result.Type, "Should return warning for very high threshold");
            Assert.IsTrue(result.Message.Contains("prevent activation"), "Warning message should mention prevention of activation");
        }

        [TestMethod]
        public void ValidationRule_ThresholdTooLow_ShouldReturnWarning()
        {
            _viewModel.VerticalThreshold = 0.2f;

            var rule = AntiRecoilValidationRules.CreateThresholdValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Warning, result.Type, "Should return warning for very low threshold");
            Assert.IsTrue(result.Message.Contains("minor movements"), "Warning message should mention minor movements");
        }

        [TestMethod]
        public void ValidationRule_ThresholdNormal_ShouldReturnSuccess()
        {
            _viewModel.VerticalThreshold = 5.0f;

            var rule = AntiRecoilValidationRules.CreateThresholdValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Success, result.Type, "Should return success for normal threshold");
        }

        [TestMethod]
        public void ValidationRule_ActivationDelayTooHigh_ShouldReturnWarning()
        {
            _viewModel.ActivationDelayMs = 600;

            var rule = AntiRecoilValidationRules.CreateActivationDelayValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Warning, result.Type, "Should return warning for very high activation delay");
            Assert.IsTrue(result.Message.Contains("miss initial recoil"), "Warning message should mention missing initial recoil");
        }

        [TestMethod]
        public void ValidationRule_ActivationDelayNormal_ShouldReturnSuccess()
        {
            _viewModel.ActivationDelayMs = 100;

            var rule = AntiRecoilValidationRules.CreateActivationDelayValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Success, result.Type, "Should return success for normal activation delay");
        }

        [TestMethod]
        public void ValidationRule_MaxTickBelowThreshold_ShouldReturnWarning()
        {
            _viewModel.VerticalThreshold = 10.0f;
            _viewModel.MaxTickCompensation = 5.0f;

            var rule = AntiRecoilValidationRules.CreateMaxTickCompensationValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Warning, result.Type, "Should return warning when max tick is below threshold");
            Assert.IsTrue(result.Message.Contains("lower than threshold"), "Warning message should mention threshold comparison");
        }

        [TestMethod]
        public void ValidationRule_MaxTickVeryHigh_ShouldReturnWarning()
        {
            _viewModel.MaxTickCompensation = 60.0f;

            var rule = AntiRecoilValidationRules.CreateMaxTickCompensationValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Warning, result.Type, "Should return warning for very high max tick compensation");
            Assert.IsTrue(result.Message.Contains("jerky movement"), "Warning message should mention jerky movement");
        }

        [TestMethod]
        public void ValidationRule_MaxTotalTooLow_ShouldReturnWarning()
        {
            _viewModel.MaxTickCompensation = 10.0f;
            _viewModel.MaxTotalCompensation = 15.0f; // Less than 3x max tick

            var rule = AntiRecoilValidationRules.CreateMaxTotalCompensationValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Warning, result.Type, "Should return warning when max total is too low relative to max tick");
            Assert.IsTrue(result.Message.Contains("very low"), "Warning message should mention being very low");
        }

        [TestMethod]
        public void ValidationRule_CooldownTooHigh_ShouldReturnWarning()
        {
            _viewModel.CooldownMs = 1200;

            var rule = AntiRecoilValidationRules.CreateCooldownValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Warning, result.Type, "Should return warning for very high cooldown");
            Assert.IsTrue(result.Message.Contains("prevent rapid reactivation"), "Warning message should mention rapid reactivation");
        }

        [TestMethod]
        public void ValidationRule_DecayTooHigh_ShouldReturnWarning()
        {
            _viewModel.DecayPerMs = 0.2f;
            _viewModel.MaxTotalCompensation = 100.0f;

            var rule = AntiRecoilValidationRules.CreateDecayValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Warning, result.Type, "Should return warning for very high decay rate");
            Assert.IsTrue(result.Message.Contains("too quickly"), "Warning message should mention resetting too quickly");
        }

        [TestMethod]
        public void ValidationRule_ConsistencyMaxTotalLessThanMaxTick_ShouldReturnError()
        {
            _viewModel.Enabled = true;
            _viewModel.Strength = 0.5f;
            _viewModel.MaxTickCompensation = 20.0f;
            _viewModel.MaxTotalCompensation = 15.0f; // Less than max tick

            var rule = AntiRecoilValidationRules.CreateConsistencyValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Error, result.Type, "Should return error when max total is less than max tick");
            Assert.IsTrue(result.Message.Contains("cannot be less than"), "Error message should mention the constraint");
        }

        [TestMethod]
        public void ValidationRule_ConsistencyCooldownShorterThanDelay_ShouldReturnWarning()
        {
            _viewModel.Enabled = true;
            _viewModel.Strength = 0.5f;
            _viewModel.ActivationDelayMs = 200;
            _viewModel.CooldownMs = 100;

            var rule = AntiRecoilValidationRules.CreateConsistencyValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Warning, result.Type, "Should return warning when cooldown is shorter than activation delay");
            Assert.IsTrue(result.Message.Contains("rapid cycling"), "Warning message should mention rapid cycling");
        }

        [TestMethod]
        public void ValidationRule_ConsistencyValidConfiguration_ShouldReturnSuccess()
        {
            _viewModel.Enabled = true;
            _viewModel.Strength = 0.5f;
            _viewModel.MaxTickCompensation = 10.0f;
            _viewModel.MaxTotalCompensation = 50.0f;
            _viewModel.ActivationDelayMs = 100;
            _viewModel.CooldownMs = 200;

            var rule = AntiRecoilValidationRules.CreateConsistencyValidation(_viewModel, _testControl);
            var result = rule.Validate();

            Assert.AreEqual(ValidationType.Success, result.Type, "Should return success for valid configuration");
        }

        [TestMethod]
        public void ValidationResult_StaticFactoryMethods_ShouldCreateCorrectly()
        {
            var success = ValidationResult.Success();
            Assert.AreEqual(ValidationType.Success, success.Type, "Success factory should create success result");
            Assert.AreEqual(string.Empty, success.Message, "Success result should have empty message");

            var error = ValidationResult.Error("Test error");
            Assert.AreEqual(ValidationType.Error, error.Type, "Error factory should create error result");
            Assert.AreEqual("Test error", error.Message, "Error result should have correct message");

            var warning = ValidationResult.Warning("Test warning");
            Assert.AreEqual(ValidationType.Warning, warning.Type, "Warning factory should create warning result");
            Assert.AreEqual("Test warning", warning.Message, "Warning result should have correct message");
        }

        [TestMethod]
        public void ValidationRule_Constructor_ShouldSetPropertiesCorrectly()
        {
            var control = new NumericUpDown();
            Func<ValidationResult> validateFunc = () => ValidationResult.Success();
            const string ruleName = "TestRule";

            var rule = new ValidationRule(control, validateFunc, ruleName);

            Assert.AreEqual(control, rule.Control, "Control should be set correctly");
            Assert.AreEqual(validateFunc, rule.Validate, "Validate function should be set correctly");
            Assert.AreEqual(ruleName, rule.Name, "Name should be set correctly");
        }

        [TestMethod]
        public void ValidationState_DefaultValues_ShouldBeCorrect()
        {
            var state = new ValidationState();

            Assert.IsFalse(state.HasError, "HasError should default to false");
            Assert.IsFalse(state.HasWarning, "HasWarning should default to false");
            Assert.IsNull(state.ErrorMessage, "ErrorMessage should default to null");
            Assert.IsNull(state.WarningMessage, "WarningMessage should default to null");
        }

        [TestMethod]
        public void ValidationEventArgs_Constructor_ShouldSetPropertiesCorrectly()
        {
            const bool hasErrors = true;
            const bool hasWarnings = false;
            const int errorCount = 2;
            const int warningCount = 0;

            var eventArgs = new ValidationEventArgs(hasErrors, hasWarnings, errorCount, warningCount);

            Assert.AreEqual(hasErrors, eventArgs.HasErrors, "HasErrors should be set correctly");
            Assert.AreEqual(hasWarnings, eventArgs.HasWarnings, "HasWarnings should be set correctly");
            Assert.AreEqual(errorCount, eventArgs.ErrorCount, "ErrorCount should be set correctly");
            Assert.AreEqual(warningCount, eventArgs.WarningCount, "WarningCount should be set correctly");
        }
    }
}
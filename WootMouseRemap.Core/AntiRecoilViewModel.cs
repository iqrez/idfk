using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WootMouseRemap.Features;

namespace WootMouseRemap.Core
{
    /// <summary>
    /// ViewModel for AntiRecoil configuration form that implements INotifyPropertyChanged
    /// and provides binding support for advanced UI controls
    /// </summary>
    public class AntiRecoilViewModel : INotifyPropertyChanged
    {
        private bool _enabled;
        private float _strength = 0.5f;
        private int _activationDelayMs = 50;
        private float _verticalThreshold = 2.0f;
        private float _horizontalCompensation = 0.0f;
        private bool _adaptiveCompensation = false;

        // New advanced fields
        private float _maxTickCompensation = 10.0f;
        private float _maxTotalCompensation = 100.0f;
        private int _cooldownMs = 0;
        private float _decayPerMs = 0.0f;

        private bool _isDirty = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        #region Properties

        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        public float Strength
        {
            get => _strength;
            set => SetProperty(ref _strength, Math.Clamp(value, 0f, 1f));
        }

        public int ActivationDelayMs
        {
            get => _activationDelayMs;
            set => SetProperty(ref _activationDelayMs, Math.Max(0, value));
        }

        public float VerticalThreshold
        {
            get => _verticalThreshold;
            set => SetProperty(ref _verticalThreshold, Math.Max(0f, value));
        }

        public float HorizontalCompensation
        {
            get => _horizontalCompensation;
            set => SetProperty(ref _horizontalCompensation, Math.Clamp(value, 0f, 1f));
        }

        public bool AdaptiveCompensation
        {
            get => _adaptiveCompensation;
            set => SetProperty(ref _adaptiveCompensation, value);
        }

        public float MaxTickCompensation
        {
            get => _maxTickCompensation;
            set => SetProperty(ref _maxTickCompensation, Math.Max(0f, value));
        }

        public float MaxTotalCompensation
        {
            get => _maxTotalCompensation;
            set => SetProperty(ref _maxTotalCompensation, Math.Max(0f, value));
        }

        public int CooldownMs
        {
            get => _cooldownMs;
            set => SetProperty(ref _cooldownMs, Math.Max(0, value));
        }

        public float DecayPerMs
        {
            get => _decayPerMs;
            set => SetProperty(ref _decayPerMs, Math.Max(0f, value));
        }

        public bool IsDirty
        {
            get => _isDirty;
            private set => SetProperty(ref _isDirty, value, nameof(IsDirty));
        }

        #endregion

        #region Methods

        /// <summary>
        /// Load settings from an AntiRecoil instance
        /// </summary>
        public void LoadFrom(AntiRecoil? antiRecoil)
        {
            if (antiRecoil == null) return;

            _enabled = antiRecoil.Enabled;
            _strength = antiRecoil.Strength;
            _activationDelayMs = antiRecoil.ActivationDelayMs;
            _verticalThreshold = antiRecoil.VerticalThreshold;
            _horizontalCompensation = antiRecoil.HorizontalCompensation;
            _adaptiveCompensation = antiRecoil.AdaptiveCompensation;

            // Load new advanced properties if they exist
            _maxTickCompensation = antiRecoil.MaxTickCompensation;
            _maxTotalCompensation = antiRecoil.MaxTotalCompensation;
            _cooldownMs = antiRecoil.CooldownMs;
            _decayPerMs = antiRecoil.DecayPerMs;

            // Notify all properties changed
            OnPropertyChanged(nameof(Enabled));
            OnPropertyChanged(nameof(Strength));
            OnPropertyChanged(nameof(ActivationDelayMs));
            OnPropertyChanged(nameof(VerticalThreshold));
            OnPropertyChanged(nameof(HorizontalCompensation));
            OnPropertyChanged(nameof(AdaptiveCompensation));
            OnPropertyChanged(nameof(MaxTickCompensation));
            OnPropertyChanged(nameof(MaxTotalCompensation));
            OnPropertyChanged(nameof(CooldownMs));
            OnPropertyChanged(nameof(DecayPerMs));

            _isDirty = false;
            OnPropertyChanged(nameof(IsDirty));
        }

        /// <summary>
        /// Apply current settings to an AntiRecoil instance
        /// </summary>
        public void ApplyTo(AntiRecoil? antiRecoil)
        {
            if (antiRecoil == null) return;

            antiRecoil.Enabled = _enabled;
            antiRecoil.Strength = _strength;
            antiRecoil.ActivationDelayMs = _activationDelayMs;
            antiRecoil.VerticalThreshold = _verticalThreshold;
            antiRecoil.HorizontalCompensation = _horizontalCompensation;
            antiRecoil.AdaptiveCompensation = _adaptiveCompensation;

            // Apply new advanced properties
            antiRecoil.MaxTickCompensation = _maxTickCompensation;
            antiRecoil.MaxTotalCompensation = _maxTotalCompensation;
            antiRecoil.CooldownMs = _cooldownMs;
            antiRecoil.DecayPerMs = _decayPerMs;

            _isDirty = false;
            OnPropertyChanged(nameof(IsDirty));
        }

        /// <summary>
        /// Reset dirty flag without changing properties
        /// </summary>
        public void ClearDirty()
        {
            _isDirty = false;
            OnPropertyChanged(nameof(IsDirty));
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;

            field = value;
            OnPropertyChanged(propertyName);

            // Mark as dirty when any property changes (except IsDirty itself)
            if (propertyName != nameof(IsDirty))
            {
                _isDirty = true;
                OnPropertyChanged(nameof(IsDirty));
            }

            return true;
        }

        #endregion
    }
}
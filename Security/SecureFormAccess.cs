using System;
using System.Windows.Forms;
using WootMouseRemap;

namespace WootMouseRemap.Security
{
    /// <summary>
    /// Secure form access utility to replace unsafe Application.OpenForms usage
    /// </summary>
    public static class SecureFormAccess
    {
        private static Form? _overlayForm;

        /// <summary>
        /// Registers the overlay form for secure access
        /// </summary>
        public static void RegisterOverlayForm(Form overlayForm)
        {
            _overlayForm = overlayForm ?? throw new ArgumentNullException(nameof(overlayForm));
            Logger.Info("Overlay form registered for secure access");
        }

        /// <summary>
        /// Gets the registered overlay form safely
        /// </summary>
        public static Form? GetOverlayForm()
        {
            if (_overlayForm?.IsDisposed == true)
            {
                _overlayForm = null;
            }
            return _overlayForm;
        }

        /// <summary>
        /// Unregisters the overlay form
        /// </summary>
        public static void UnregisterOverlayForm()
        {
            _overlayForm = null;
            Logger.Info("Overlay form unregistered");
        }
    }
}
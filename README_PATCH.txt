PERFECT — Passthrough-to-ViGEm Patch (Small Update)
===================================================
This patch ensures Controller Passthrough mode only starts after the ViGEm virtual
controller is connected, preventing a null target. It does not change behavior for
Mouse/Keyboard or normal ControllerOutput modes.

Files included:
  - OverlayForm.cs  (patched)

What changed (ControllerPassthrough branch in OnModeChanged):
  - Before starting XInputPassthrough: if the ViGEm pad wrapper reports not connected,
    the code calls _pad.Connect() and then starts passthrough. This guarantees the
    mirroring loop writes into a live virtual controller.

Install:
  1) Close the app / stop your build.
  2) Back up your current 'OverlayForm.cs'.
  3) Copy the 'OverlayForm.cs' from this patch over your project file at:
       PERFECT/OverlayForm.cs
  4) Rebuild and run.

Verify in-app:
  - Switch to Controller Passthrough: status should show 'ViGEm: OK'.
  - Physical controller input should be mirrored by the ViGEm device.
  - If you experience “double input”, use HidHide to hide the physical controller.


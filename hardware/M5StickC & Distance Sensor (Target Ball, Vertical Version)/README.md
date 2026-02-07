## M5StickC Combined Controller

### Description
This folder contains a combined M5StickC controller firmware used across
multiple games during development and testing.

The firmware supports multiple input modes within a single script, allowing
the same controller to be reused without re-uploading code.

---

### Mode Switching
- Press **Button A** on the M5StickC to cycle between control modes
- Each mode corresponds to a different game input mapping

---

### Display
- The M5StickC LCD displays the currently active control mode

---

### Notes
- Scripts are written for the **Arduino IDE**
- IMU-based tilt input is used for control
- Serial data is transmitted to Unity for gameplay interaction

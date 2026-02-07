## Hardware

This folder contains hardware-related firmware, test code, and setup notes used to support the interactive components across the project.

All hardware scripts in this folder are written for use with the **Arduino IDE** software and are intended to run on microcontroller-based devices such as the M5StickC and ESP32.

The hardware modules enable sensor-based and motion-based interaction for selected games, allowing players to control gameplay using physical movement rather than standard mouse or keyboard input.

Each subfolder corresponds to a specific hardware configuration or test setup, and includes reference code and brief documentation describing its purpose and usage.

---

### Folder Overview

- **M5StickC IMU (Flappy Bird)**  
  IMU-based tilt control used to control the bird’s vertical movement.

- **M5StickC IMU (Pong)**  
  IMU-based tilt control used to move the paddle left and right.

- **M5StickC Ball Target Game**  
  Firmware supporting ball-based rehabilitation gameplay using tilt input.

- **M5StickC Distance Sensor Ball Target Game**  
  Distance sensor–based control for vertical ball movement in the target ball game.

- **M5StickC Combined Controller**  
  A unified firmware configuration used across multiple games, combining IMU and sensor inputs into a single controller.

- **M5StickC Sensor Test**  
  Test code used to verify IMU readings, sensor values, and serial communication before integration with Unity.

- **Sharp IR Distance Sensor (Arm Rehab Device)**  
  Firmware and setup notes for using a Sharp IR distance sensor with the arm movement rehabilitation device.

- **Sharp IR Distance Sensors (Flower Field)**  
  Code and notes for using multiple distance sensors mapped to left-to-right on-screen flower positions.

- **Sharp IR Distance Sensors Test**  
  Standalone test code for validating distance sensor range, stability, and calibration.

---

### Notes

- Hardware usage is optional and intended to enhance interactivity for selected games.
- All games can still be opened, reviewed, and tested in Unity without connecting hardware.
- Pin assignments depend on how the hardware components are connected.
- The provided pin mappings can be followed as a reference or adapted to suit different wiring configurations.


## Hardware

This folder contains hardware-related firmware, test code, and setup notes used to support the interactive components across the project.

All hardware scripts in this folder are written for use with the **Arduino IDE** software and are intended to run on microcontroller-based devices such as the M5StickC and ESP32.

The hardware modules enable sensor-based and motion-based interaction for selected games.

Each subfolder corresponds to a specific hardware configuration or test setup and includes reference code.

---

## Folder Overview

- **M5StickC & Distance Sensor (Target Ball, Vertical Version)**  
  Firmware for the vertical version of the Target Ball rehabilitation game.  
  Uses an M5StickC with a Sharp IR distance sensor to control the ball’s vertical movement.

- **M5StickC (Flappy Bird)**  
  IMU-based tilt control used to control the bird’s vertical movement in Flappy Bird.

- **M5StickC (Pong)**  
  IMU-based tilt control used to move the player paddle left and right in Pong.

- **M5StickC (Target Ball, Arc Version)**  
  IMU-based tilt control for guiding the ball along a curved arc path in the Target Ball arc version.

- **M5StickC Combined Controller (For Demo)**  
  Combined controller firmware prepared specifically for the final demo presentation.  
  Supports:
  - Flappy Bird  
  - Target Ball (Arc Version)  
  - Target Ball (Vertical Version using distance sensor)

- **M5StickC Combined Controller**  
  Unified controller firmware used across multiple games.  
  Supports:
  - Flappy Bird  
  - Pong  
  - Target Ball (Arc Version)  
  - Target Ball (Vertical Version using distance sensor)

- **M5StickC Sensors Test**  
  Test firmware used to verify IMU readings, distance sensor values, and serial communication before integration with Unity.

- **Sharp IR Distance Sensor (Flower Field)**  
  Firmware and setup code for using multiple Sharp IR distance sensors in the Flower Field game.  
  Sensors are mapped left-to-right to corresponding on-screen flower positions.

- **Sharp IR Distance Sensor Target Ball (Vertical)**  
  Standalone firmware for testing the vertical Target Ball control using a single Sharp IR distance sensor.  
  Designed to run on an ESP32 without the use of an M5StickC, allowing isolated distance-sensor testing and calibration.

---

### Notes

- Pin assignments depend on how the hardware components are connected.
- The provided pin mappings can be followed as a reference or adapted to suit different wiring configurations.

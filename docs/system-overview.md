## System Overview

This project integrates Unity-based interactive games with external
hardware controllers to support sensor-driven interaction.

Unity handles:
- Game logic
- Visual feedback
- User interaction

External hardware (M5StickC / ESP32) handles:
- Sensor data acquisition
- Motion and distance detection
- Serial communication with Unity

Data is transmitted from the microcontroller to Unity over serial,
where it is mapped to in-game actions.

# Interactive Geriatric Games (Rehabilitation and Dementia)

Final Year Project (FYP) – Ngee Ann Polytechnic  
Diploma in Electronic & Computer Engineering  

By Renard Goh Ing Liang

A Unity-based suite of interactive games designed for geriatric rehabilitation and engagement, using both on-screen interaction and custom sensor-based controllers.

## Included Games
- Memory Card (Dementia)
- Tiles (Dementia)
- Pong (Rehabilitation)
- Flappy Bird (Rehabilitation)
- Flower Field (Proof of Concept Game)
- Target-Based Ball Game (Proof of Concept Game, 2 versions)

## Playable Game Builds
This repository contains the full Unity project files and source code.

For playable build versions of the games, please refer to the following repository:

**Build Repository:**  
https://github.com/Rasperised/Interactive-Geriatric-Games-Builds

All playable builds are available as downloadable ZIP files.

## Repository Structure
- `unity/` – Unity project files for all games
- `hardware/` – ESP32 / M5StickC controller code
- `docs/` – System overview and hardware integration notes

## Credits & Third-Party Plugins
Special thanks to the developers of the following open-source plugins used in this project:

* **[UnitySerialPort](https://github.com/prossel/UnitySerialPort)** by prossel – Enables serial communication between Unity and the hardware.
* **[UnityStandaloneFileBrowser](https://github.com/gkngkc/UnityStandaloneFileBrowser)** by gkngkc – Used for the custom image upload feature.

## Notes
- Due to Unity’s folder structure, it is recommended to extract or clone this repository into a short directory path (e.g. `C:\repo\`) to avoid Windows path length issues.

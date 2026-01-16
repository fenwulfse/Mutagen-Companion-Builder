# Mutagen Companion Builder (Fallout 4)

**A C# Console Application for generating complex Fallout 4 Companions programmatically.**

This project uses [Mutagen](https://github.com/Mutagen-Modding/Mutagen) (.NET 8.0) to build a fully recruiting/dismissing companion from scratch, without opening the Creation Kit.

## 🚀 Features
- **Instant Generation:** Creates `CompanionGemini.esp` and the required Papyrus source (`.psc`) in seconds.
- **Full Architecture:**
  - **NPC:** Generates the Actor with Traits, Stats, and correct Faction stacks.
  - **Quest:** Creates the Recruitment Quest (Priority 70) with Stages 80 (Recruit) and 90 (Dismiss).
  - **Scripting:** Automatically writes the `Fragments:Quests` script logic.
  - **Dialogue:** Sets up Topics, Scenes, and Greetings with "Say Once" logic.
  - **World:** Generates a standalone interior cell (`GeminiStartCell`) with the NPC placed inside.

## 🛠️ Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Fallout 4 (Installed)

## 🏃 How to Run
1. Open a terminal in this folder.
2. Run the build command:
   ```bash
   dotnet run
   ```
3. The tool will:
   - Read your Fallout 4 Master files.
   - Generate `CompanionGemini.esp`.
   - Generate `QF_COMGemini_xxxx.psc` (Papyrus Fragment Source).
   - Attempt to deploy the ESP to your Data folder automatically.

## 🧩 Modifying the NPC
Open `Program.cs` to change the NPC's name, ID, or stats.
- **Line 80:** NPC Name/ID
- **Line 100:** Appearance (Hair/Eyes)
- **Line 280:** Dialogue Lines

## 🤝 Contributing
This project is a Proof-of-Concept for automated modding. We are looking for help with:
- **Dialogue/Voice:** Better handling of Scene/Phase logic.
- **Lip Sync:** Generating `.lip` files programmatically.

## 📄 License
MIT License - feel free to use this as a skeleton for your own companion mods.

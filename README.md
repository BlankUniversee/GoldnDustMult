# Gold & Dreamdust Multiplier (Shape of Dreams)

A lightweight BepInEx mod that increases gold and dreamdust rewards, and modifies shop buy/sell prices.  
Designed to stay simple, stable, and easy to tweak.

---

## ✨ Features
- Multiply gold and dreamdust drop amounts.
- Modify shop **sell** and **buy** prices.

---

## ⚙️ Default Config

```ini
[General]
GoldMultiplier = 1.5
DreamdustMultiplier = 1.5

[Shop]
SellPriceMultiplier = 1.5
BuyPriceMultiplier = 1.0
```

---

## 📁 Installation

1. **Download & install [BepInEx 5](https://github.com/BepInEx/BepInEx/releases)**  
- Extract the BepInEx zip into your **Shape of Dreams** game folder.  
- After extracting, your folder should look something like this:
 ```
 Shape of Dreams/
 ├─ BepInEx/
 ├─ doorstop_config.ini
 ├─ winhttp.dll
 └─ Shape of Dreams.exe
 ```

2. **Run the game once**  
- This will make BepInEx finish its setup and create the `BepInEx/config` folder.

3. **Install the mod**  
- Place the `GoldnDustMult.dll` file into:
 ```
 Shape of Dreams/BepInEx/plugins/
 ```

4. **Configure (optional)**  
- A config file will be created at:
 ```
 Shape of Dreams/BepInEx/config/com.blank.goldanddust.cfg
 ```
- Open it in a text editor to adjust gold/dreamdust/shop multipliers.

5. **Launch the game and enjoy!**

## 🧹 Uninstallation

1. **Delete the DLL File**
- Shape of Dreams/BepInEx/plugins/GoldnDustMult.dll

2. **(Optional) Delete the config file**
- Shape of Dreams/BepInEx/config/com.blank.goldanddust.cfg


## 📝 Notes

This mod currently only affects gold and dreamdust.

Stardust may be added later depending on game updates.

Compatible with game version r.1.0.6.2_s (September 2025).
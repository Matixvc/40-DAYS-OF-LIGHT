# ☀️ 40 Days of Light

[![Unity](https://img.shields.io/badge/Unity-6000.6.0f1-black?style=flat-square&logo=unity)](https://unity.com/)
[![Render Pipeline](https://img.shields.io/badge/URP-17.6.0-blue?style=flat-square)](https://unity.com/srp/Universal-Render-Pipeline)
[![Target Platform](https://img.shields.io/badge/Platform-Android%20%7C%20PC-green?style=flat-square)](https://developer.android.com/)
[![GDD Document](https://img.shields.io/badge/GDD-Google%20Docs-yellow?style=flat-square&logo=googledocs)](https://docs.google.com/document/d/165h69wUfPjhvUtIk9g5kWuW4gadALXU3zHCv-xAI24s/edit?usp=sharing)

> **40 Days of Light** es un roguelite de acción y supervivencia top-down en un desierto estilizado de fantasía oscura. Encarna a Dawit, el Pastor de la Luz, y sobrevive a 40 jornadas enfrentando hordas de las *Sombras de la Desolación*.

---

## 📖 Documento de Diseño de Juego (GDD)

El diseño arquitectónico completo, la narrativa, las curvas de balance y las decisiones artísticas del proyecto están documentados en el GDD oficial:

👉 **[Consultar el GDD Completo en Google Docs](https://docs.google.com/document/d/165h69wUfPjhvUtIk9g5kWuW4gadALXU3zHCv-xAI24s/edit?usp=sharing)**

---

## ⚡ Características Principales

* **Arquitectura Zero-Allocation (Móvil 60 FPS):** Implementación de *Object Pooling* dinámico para enemigos, gemas de experiencia y partículas, eliminando picos de *Garbage Collection* en Android.
* **Ciclo Ambiental Día/Noche:** Transición suave de iluminación, niebla lineal y *Skybox* que alterna entre exploración diurna y asedios nocturnos con jefes titánicos cada 10 rondas.
* **Estrategia URP Dual-Pipeline:** Configuraciones optimizadas diferenciadas (`Mobile_RPAsset` a 0.8x Render Scale vs `PC_RPAsset` con sombras dinámicas suaves y SSAO).
* **Controles Híbridos Adaptativos:** Detección automática de plataforma (Teclado/Ratón en PC, Joystick y botones táctiles minimalistas en móviles).
* **UI Minimalista Estilizada:** Interfaz limpia generada mediante activos vectoriales estilizados de alto contraste y tipografía nítida con TextMeshPro.

---

## 🛠️ Tech Stack & Arquitectura

* **Engine:** Unity 6000.6.0f1
* **Render Pipeline:** Universal Render Pipeline (URP 17.6.0)
* **Input System:** Unity New Input System + Touch UI fallback
* **UI & Fonts:** TextMeshPro + Custom Vector UI Assets
* **State Management:** Centralized `GameStateController` & `RunDirector`

---

## 🚀 Instalación y Compilación

### Ejecutar en Editor (PC)
1. Clona el repositorio:
   ```bash
   git clone https://github.com/Matixvc/40-DAYS-OF-LIGHT.git
   ```
2. Abre **Unity Hub** → **Add** → selecciona la carpeta clonada.
3. Abre con **Unity 6000.6.0f1** (URP 17.6.0, Input System 1.20.0).
4. Abre la escena `Assets/Scenes/MainMenu.unity` y presiona **Play**.

### Compilar para Android
1. `File → Build Settings → Android → Switch Platform`.
2. `Quality`: verifica `Android/iPhone = Mobile` (`Mobile_RPAsset`, Render Scale 0.8).
3. `Player Settings → Orientation: Landscape`, `Target Frame Rate: 60` (`PerformanceBootstrap`).

### Compilar para PC
1. `File → Build Settings → Standalone → Switch Platform`.
2. `Quality`: verifica `Standalone = PC` (`PC_RPAsset`, sombras suaves + SSAO).

---

## 🎮 Controles

| Acción | PC (Teclado/Mando) | Móvil (Táctil) |
| --- | --- | --- |
| Moverse | `WASD` / Flechas / Stick izquierdo | Joystick virtual |
| Sprint | `Shift` / `Espacio` / `StickPress` | Botón SPRINT |
| Pausa | `Esc` / `Start` | Botón II |
| Reiniciar (Game Over) | `R` / `Select` | Botón Reintentar |
| Ataque | Automático (`Destello de Luz`) | Automático |

---

## 📂 Estructura del Proyecto

```text
Assets/
├── Extra/
│   ├── InputSystem_Actions.inputactions
│   └── Settings/
│       ├── Mobile_RPAsset.asset + Mobile_Renderer.asset
│       ├── PC_RPAsset.asset + PC_Renderer.asset
│       └── SampleSceneProfile.asset
├── Scenes/
│   ├── MainMenu.unity
│   └── Prototype.unity
└── Scripts/
    ├── Core/GameSessionConfig.cs
    ├── Managers/ (GameManager, GameStateController, AudioManager, UpgradeManager)
    ├── Waves/ (RunDirector, EnemyScalingProfile)
    ├── Enemy/ (EnemyAI, EnemySpawner)
    ├── Player/ (PlayerController, PlayerAttack, PlayerLevelSystem)
    ├── Input/ (PlayerInputReader, TouchControlsUI, VirtualJoystick, VirtualButton)
    ├── Environment/ (EnvironmentLightingController)
    ├── Optimization/ (PerformanceBootstrap, PostProcessingBootstrap, ObjectPoolManager)
    ├── UI/ + ScriptableObject/ + Runtime/RunStats.cs
```

---

## 📜 Licencia

Este proyecto está bajo la [Licencia MIT](LICENSE). Consulta el archivo para más detalles.

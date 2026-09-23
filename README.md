# ✝️ Pastor Survival — 3D Action Roguelite

![Unity Version](https://img.shields.io/badge/Unity-2022.3%2B-blue?style=for-the-badge&logo=unity)
![Language](https://img.shields.io/badge/C%23-10.0-green?style=for-the-badge&logo=csharp)
![Platform](https://img.shields.io/badge/Platform-PC%20%7C%20Windows-lightgrey?style=for-the-badge)
![License](https://img.shields.io/badge/License-MIT-orange?style=for-the-badge)

**Pastor Survival** es un videojuego de acción *roguelite* y supervivencia en 3D desarrollado en **Unity**, inspirado en mecánicas de *Vampire Survivors*. El jugador controla al Pastor, quien debe sobrevivir a hordas crecientes de demonios Imps utilizando habilidades sagradas como el *Destello de Luz*, recolectando gemas de experiencia para subir de nivel y adquiriendo mejoras pasivas en tiempo real.

---

## 🎮 Mecánicas Principales (Gameplay)

- **Sistema de Movimiento y Sprint:** Control de personaje basado en `CharacterController` con mecánicas de aceleración, rotación suave y barra de estamina/sprint dinámica accionable mediante `Shift`, `Espacio` o `Clic Izquierdo`.
- **Ataque Automático en Área:** El sistema de armas (*Destello de Luz*) ejecuta pulsos periódicos basados en `WeaponDataSO`, detectando enemigos dentro de un rango mediante `Physics.OverlapSphere` y filtrado por *Layers*.
- **IA Enemiga Progresiva:** Los Imps utilizan `NavMeshAgent` para perseguir al jugador, realizando ataques frontales con cálculo de ángulo relativo (`Vector3.Dot`) y escalado de dificultad por oleadas.
- **Ciclo de Experiencia y Subida de Nivel:** Las gemas de XP eliminadas cuentan con un sistema de atracción magnética hacia el jugador. Al completar la barra de XP, el juego congela el tiempo e invoca una interfaz dinámica de cartas de mejora (`LevelUpUI`).
- **Sistema de Audio Robusto y Anti-Saturación:** Gestor de audio centralizado (`AudioManager`) con asignación a `AudioMixer` (`Music` y `SFX`), pool de `AudioSources`, variación aleatoria de *pitch*, e intervalos mínimos (0.04s) para evitar la saturación de efectos en combate.
- **Playlist BGM Automática:** Sistema de música de fondo en bucle que alterna suavemente entre múltiples pistas `.mp3` configuradas en `Streaming`.

---

## 🛠️ Arquitectura Técnica y Patrones de Diseño

El proyecto está diseñado bajo estándares de código limpio, modularidad y optimización de rendimiento en Unity:

1. **ScriptableObjects (SO):** Arquitectura orientada a datos (*Data-Driven Design*) utilizada para definir estadísticas de personajes (`CharacterDataSO`), armas (`WeaponDataSO`), enemigos (`EnemyDataSO`) y cartas de mejora (`UpgradeDataSO`).
2. **Singleton Pattern:** Implementado en el `AudioManager` para garantizar un punto de acceso global y persistencia entre escenas con `DontDestroyOnLoad`.
3. **Object Pooling & Anti-Saturation:** Pool de 12 `AudioSources` reutilizables para optimizar la instanciación de efectos sonoros en combates masivos.
4. **Context Menu Resetting:** Implementación de atributos `[ContextMenu]` en todos los ScriptableObjects para permitir a los desarrolladores restaurar estadísticas base desde el Inspector con un solo clic.
5. **Decoupled UI & Events:** Separación estricta entre la lógica de juego y la interfaz a través de C# Actions/Events (`OnXPChanged`, `OnHealthChanged`).

---

## 📂 Estructura del Proyecto

```text
Assets/
├── Audio/
├── AudioMixer/
│   └── MasterMixer.mixer
├── Animations/
│   ├── AnimatorControllers/
│   └── Clips/
├── Prefabs/
│   ├── Enemies/
│   ├── Gems/
│   └── UI/
├── ScriptableObjects/
│   ├── Characters/
│   ├── Enemies/
│   ├── Upgrades/
│   └── Weapons/
└── Scripts/
    ├── Audio/
    │   └── AudioManager.cs
    ├── Combat/
    │   ├── HealthComponent.cs
    │   ├── PlayerAttack.cs
    │   └── WeaponDataSO.cs
    ├── Enemies/
    │   ├── EnemyAI.cs
    │   ├── EnemyDataSO.cs
    │   └── EnemySpawner.cs
    ├── Player/
    │   ├── CharacterDataSO.cs
    │   ├── PlayerController.cs
    │   └── PlayerLevelSystem.cs
    └── UI/
        ├── LevelUpUI.cs
        ├── PauseMenu.cs
        ├── UpgradeCardUI.cs
        └── XPGem.cs

# Entorno de pruebas (TEST)

Arena aislada para validar mecánicas, IA, balance y UI **sin tocar** `Assets/Scenes/Prototype.unity`.

> Nada de lo que hay en esta carpeta modifica el juego. Los campos privados de los scripts de
> producción se cablean con `SerializedObject` **sobre la escena TEST**, nunca en su código.

---

## Generar la escena

`Tools ▸ 40 Days of Light ▸ Construir escena TEST`

Genera `Assets/_TEST/TEST.unity` desde cero. Es **idempotente**: se puede volver a ejecutar
tantas veces como haga falta tras cambiar un script de producción.

| Item de menú | Para qué |
|---|---|
| `Construir escena TEST` | Genera o regenera la escena completa |
| `Ajustes de la escena TEST` | Selecciona el asset de ajustes para asignar prefabs a mano |
| `Quitar TEST de Build Settings` | Retira la escena de la build de producción |

### Resolución del Player (cascada de 3 vías)

1. **Prefab asignado** en `TestBuildSettings.asset` — la vía más fiable.
2. **Búsqueda por nombre**: cualquier prefab `Player` del proyecto.
3. **Copia desde `Prototype.unity`**: abre la escena en aditivo, clona la jerarquía del
   Player y la cierra **sin guardar**.

> Hoy funciona la **vía 3**: el Player es el FBX `Assets/Models/PlayerPastor.fbx` montado
> dentro de la escena de gameplay, no un prefab. `Prototype.unity` no se modifica jamás.
>
> El `PlayerInputReader` que ya viene en la jerarquía del Player se **reutiliza** en vez de
> crear otro: dos instancias dispararían el guardia de singleton y el juego apuntaría al
> componente desactivado (sin pausa ni reinicio).

---

## Atajos

| Tecla | Acción |
|---|---|
| `F1` / `F2` / `F3` | Horda de 10 / 50 / 100 enemigos |
| `F4` | Limpiar la horda del sandbox (sin soltar XP) |
| `F5` | Subir de nivel (abre el `LevelUpUI` real y congela) |
| `F6` | Aplicar la mejora seleccionada, sin pasar por la UI |
| `F7` | Activar / desactivar el ataque del jugador |
| `F8` | Congelar / descongelar la IA enemiga |
| `F9` | Activar / detener el spawner continuo |
| `F10` | Mostrar / ocultar el panel de depuración |
| `Supr` | Reiniciar la escena TEST |

Mismo conjunto de acciones disponible desde los botones del panel: la UI no decide nada,
sólo invoca métodos públicos de `TestEnvironmentController`.

---

## NavMesh

Se hornea **en runtime** en `TestNavMeshGate.Awake()`, no al guardar la escena. Permite
iterar la geometría en Play Mode y rehornear sin volver a generar la escena.

`TestNavMeshGate` publica `IsReady` y el sandbox **bloquea las hordas hasta que el NavMesh
existe**. Sin esa puerta, `NavMesh.SamplePosition` devolvería `false` y el spawn fallaría
en silencio, que es el peor síntoma posible en una herramienta de pruebas.

> Si el NavMesh no se hornea, revisa que el suelo y los muros estén en la capa `Terreno` y
> que el `NavMeshSurface` la tenga en su **Layer Mask**.

---

## Límites conocidos

- Los enemigos que genera el sandbox **no se registran** en el contador interno del
  `EnemySpawner` (`aliveEnemiesCount` / `trackedEnemies`). Es inocuo aquí porque el spawner
  continuo se deja desactivado y la gestión de instancias es del `ObjectPoolManager`.
- `Forzar fin de ronda` usa reflexión: `RunDirector.RoundTimeRemaining` tiene un *setter*
  privado y no se ha querido ampliar la API pública del director por una herramienta de test.
- La escena se registra en **Build Settings habilitada** porque `SceneManager.LoadScene`
  no funciona con escenas no registradas (lo necesita el botón de reinicio). Quítala antes
  de construir la versión de producción.
- Fuera del Editor el prefab enemigo debe asignarse a mano: no existe `AssetDatabase`.
- **El `AudioManager` se crea vacío**, a propósito. En `Prototype.unity` comparte GameObject
  raíz con *todos* los managers del juego, así que cualquier intento de clonarlo (incluso
  `Object.Instantiate` sobre el componente, que clona el GameObject entero con sus hijos)
  arrastra seis singletons duplicados que se autodestruyen al arrancar. No es bloqueante:
  el `AudioManager` tiene fallbacks procedurales y los SFX de combate llegan con clips
  explícitos desde `PlayerAttack` y `HealthComponent`. Lo que falta es la música y las
  variaciones del catálogo: se asignan a mano en el Inspector si se echa de menos.

---

## Estructura

```
Assets/_TEST/
├── TEST.unity                 ← generada, no editar a mano
├── Scripts/                   ← runtime (entran en Assembly-CSharp)
│   ├── TestEnvironmentController.*   lógica del sandbox (4 partials)
│   ├── TestDebugPanel.*              panel y telemetría (2 partials)
│   ├── TestNavMeshGate.cs            horneado + señal de "listo"
│   └── TestHotkeyMap.cs              atajos con el Input System
├── Editor/                    ← sólo Editor
│   ├── TestBuildSettings.cs          ajustes persistentes
│   ├── TestSceneBuilder.*            generador de la escena (8 partials)
│   ├── TestSceneBuilderUtil.cs       cableado por SerializedObject
│   └── TestSceneBuilderUi*.cs        constructores de UI
└── Materials/                 ← generado
```

/// <summary>
/// Comportamiento de runtime de un arquetipo de arma (Fase 3).
///
/// Contrato deliberadamente mínimo: el <see cref="WeaponController"/> es el único que
/// conoce la lista de armas, los niveles y la cadencia; el comportamiento solo se ocupa de
/// "qué hace" el arma en su <see cref="Tick"/>.
///
/// Los comportamientos son COMPONENTES que se crean UNA vez al equipar el arma, no por
/// disparo: por eso pueden cachear buffers, listas y arrays y no generar basura (Zero-GC).
/// </summary>
public interface IWeaponBehaviour
{
    /// <summary>
    /// Se llama una sola vez, justo después de crear el componente y antes del primer Tick.
    /// Aquí es donde se construyen los hijos (orbes, aro del aura...) y se cachean referencias.
    /// </summary>
    void Initialize(WeaponController owner, WeaponDataSO data, int level);

    /// <summary>
    /// Sube o baja el nivel del arma en caliente. Debe ser idempotente: el controlador lo
    /// invoca también al reconstruir el estado tras un cambio de estadísticas.
    /// </summary>
    void SetLevel(int level);

    /// <summary>Latido por frame. El controlador ya filtra si el sistema nuevo está activo.</summary>
    void Tick(float deltaTime);

    /// <summary>El arma se retira: libera hijos y desuscribe eventos. No destruye el host.</summary>
    void Dispose();
}

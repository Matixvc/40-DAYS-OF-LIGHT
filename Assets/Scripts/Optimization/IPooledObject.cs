/// <summary>
/// Implementa esta interfaz en los componentes que necesitan reiniciar su estado
/// cuando su GameObject se recicla desde el <see cref="ObjectPoolManager"/>.
/// </summary>
public interface IPooledObject
{
    /// <summary>Se llama al sacar el objeto del pool (ya activo, antes de configurarlo).</summary>
    void OnPoolSpawned();

    /// <summary>Se llama justo antes de devolver el objeto al pool.</summary>
    void OnPoolDespawned();
}

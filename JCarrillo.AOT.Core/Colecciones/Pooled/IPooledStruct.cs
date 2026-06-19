using System;

namespace JCarrillo.AOT.Core.Colecciones.Pooled
{
    public interface IPooledStruct<TItem> : IDisposable
    {
        // Informacion de la estructura
        int Tamaño { get; }
        bool EsAmpliable { get; }
        bool EstaDisposed { get; }

        // Indexador nativo por referencia (Permite lectura y modificación directa)
        ref TItem this[int indice] { get; }

        // Datos de la estructura
        Span<TItem> Span { get; }
        Memory<TItem> Memory { get; }

        // Duck Typing Enumerador de la estructura
        Span<TItem>.Enumerator GetEnumerator();

        // Metodos de la estructura
        bool IntentarAmpliar(int nuevoTamaño);
    }
}

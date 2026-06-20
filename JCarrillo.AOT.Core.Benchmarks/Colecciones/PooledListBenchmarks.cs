using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using JCarrillo.AOT.Core.Colecciones.Pooled;
using JCarrillo.AOT.Core.Colecciones.Pooled.Ref;

namespace JCarrillo.AOT.Core.Benchmarks.Colecciones
{
    [MemoryDiagnoser]
    [HtmlExporter]
    public class PooledListBenchmarks
    {
        [Params(100, 1000)]
        public int Size { get; set; }

        private string[]? _strings;

        [GlobalSetup]
        public void Setup()
        {
            _strings = new string[Size];
            for (int i = 0; i < Size; i++)
            {
                _strings[i] = $"Item-{i}";
            }
        }

        #region Pruebas de rendimiento para Int (Tipo de valor)

        [Benchmark(Baseline = true)]
        public int List_Int_Dynamic()
        {
            var list = new List<int>();
            for (int i = 0; i < Size; i++)
            {
                list.Add(i);
            }
            return list.Count;
        }

        [Benchmark]
        public int List_Int_Fixed()
        {
            var list = new List<int>(Size);
            for (int i = 0; i < Size; i++)
            {
                list.Add(i);
            }
            return list.Count;
        }

        [Benchmark]
        public int PooledList_Int_Dynamic()
        {
            using var list = new PooledList<int>();
            for (int i = 0; i < Size; i++)
            {
                list.Add(i);
            }
            return list.Tamaño;
        }

        [Benchmark]
        public int PooledList_Int_Fixed()
        {
            using var list = new PooledList<int>(Size);
            for (int i = 0; i < Size; i++)
            {
                list.Add(i);
            }
            return list.Tamaño;
        }

        [Benchmark]
        public int PooledListRef_Int_Dynamic()
        {
            using var list = new PooledListRef<int>();
            for (int i = 0; i < Size; i++)
            {
                list.Add(i);
            }
            return list.Tamaño;
        }

        [Benchmark]
        public int PooledListRef_Int_Fixed()
        {
            using var list = new PooledListRef<int>(Size);
            for (int i = 0; i < Size; i++)
            {
                list.Add(i);
            }
            return list.Tamaño;
        }

        #endregion

        #region Pruebas de rendimiento para String (Tipo de referencia)

        [Benchmark]
        public int List_String_Dynamic()
        {
            var list = new List<string>();
            for (int i = 0; i < Size; i++)
            {
                list.Add(_strings![i]);
            }
            return list.Count;
        }

        [Benchmark]
        public int List_String_Fixed()
        {
            var list = new List<string>(Size);
            for (int i = 0; i < Size; i++)
            {
                list.Add(_strings![i]);
            }
            return list.Count;
        }

        [Benchmark]
        public int PooledList_String_Dynamic()
        {
            using var list = new PooledList<string>();
            for (int i = 0; i < Size; i++)
            {
                list.Add(_strings![i]);
            }
            return list.Tamaño;
        }

        [Benchmark]
        public int PooledList_String_Fixed()
        {
            using var list = new PooledList<string>(Size);
            for (int i = 0; i < Size; i++)
            {
                list.Add(_strings![i]);
            }
            return list.Tamaño;
        }

        [Benchmark]
        public int PooledListRef_String_Dynamic()
        {
            using var list = new PooledListRef<string>();
            for (int i = 0; i < Size; i++)
            {
                list.Add(_strings![i]);
            }
            return list.Tamaño;
        }

        [Benchmark]
        public int PooledListRef_String_Fixed()
        {
            using var list = new PooledListRef<string>(Size);
            for (int i = 0; i < Size; i++)
            {
                list.Add(_strings![i]);
            }
            return list.Tamaño;
        }

        #endregion
    }
}

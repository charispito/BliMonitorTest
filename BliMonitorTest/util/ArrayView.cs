using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCaraTest.util
{
    public class ArrayView<T> : IEnumerable<T>
    {
        private readonly T[] array;
        private readonly int offset, count;

        public ArrayView(T[] array, int offset, int count)
        {
            this.array = array;
            this.offset = offset;
            this.count = count;
        }

        public int Length
        {
            get { return count; }
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= this.count)
                    throw new IndexOutOfRangeException();
                else
                    return this.array[offset + index];
            }
            set
            {
                if (index < 0 || index >= this.count)
                    throw new IndexOutOfRangeException();
                else
                    this.array[offset + index] = value;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = offset; i < offset + count; i++)
                yield return array[i];
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            IEnumerator<T> enumerator = this.GetEnumerator();
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }
    }
}

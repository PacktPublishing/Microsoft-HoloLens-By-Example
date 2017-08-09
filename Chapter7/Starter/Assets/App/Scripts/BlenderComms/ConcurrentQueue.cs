using System.Collections.Generic;

public class ConcurrentQueue<T>
{
    Queue<T> queue;
    readonly object syncLock = new object();

    public ConcurrentQueue()
    {
        queue = new Queue<T>();
    }

    public int Count
    {
        get
        {
            lock (syncLock)
            {
                return queue.Count;
            }
        }
    }

    public void Clear()
    {
        lock (syncLock)
        {
            queue.Clear();
        }
    }

    public bool Contains(T item)
    {
        lock (syncLock)
        {
            return queue.Contains(item);
        }
    }

    public void CopyTo(T[] array, int idx)
    {
        lock (syncLock)
        {
            queue.CopyTo(array, idx);
        }
    }

    public T Dequeue()
    {
        lock (syncLock)
        {
            return queue.Dequeue();
        }
    }

    public void Enqueue(T item)
    {
        lock (syncLock)
        {
            queue.Enqueue(item);
        }
    }

    public Queue<T>.Enumerator GetEnumerator()
    {
        lock (syncLock)
        {
            return queue.GetEnumerator();
        }
    }

    public T Peek()
    {
        lock (syncLock)
        {
            return queue.Peek();
        }
    }

    public T[] ToArray()
    {
        lock (syncLock)
        {
            return queue.ToArray();
        }
    }

    public void TrimExcess()
    {
        lock (syncLock)
        {
            queue.TrimExcess();
        }
    }
}
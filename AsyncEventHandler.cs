using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace borkbot
{
    class AsyncEventHandler<O, T, K>
    {
        HashSet<Func<O, T, Task<K>>> callbacks = new HashSet<Func<O, T, Task<K>>>();

        public void Register(Func<O, T, Task<K>> f)
        {
            lock (callbacks)
            {
                callbacks.Add(f);
            }
        }

        public void DeRegister(Func<O, T, Task<K>> f)
        {
            lock (callbacks)
            {
                callbacks.Remove(f);
            }
        }

        public static AsyncEventHandler<O, T, K> operator +(AsyncEventHandler<O, T, K> that, Func<O, T, Task<K>> f) {
            that.Register(f);
            return that;
        }

        public static AsyncEventHandler<O, T, K> operator -(AsyncEventHandler<O, T, K> that, Func<O, T, Task<K>> f)
        {
            that.DeRegister(f);
            return that;
        }

        public Task<K[]> Invoke(O origin, T payload)
        {
            List<Task<K>> tasks;
            lock (callbacks)
            {
                tasks = new List<Task<K>>(callbacks.Count);
                foreach (var f in callbacks)
                {
                    tasks.Add(f(origin, payload));
                }
            }
            return Task.WhenAll(tasks);
        }

        public Func<O,T,A> Fold<A>(Func<Func<O,T,A>,Func<O,T,Task<K>>,Func<O,T,A>> foldFunc, Func<O,T,A> init)
        {
            var prev = init;
            lock (callbacks)
            {
                foreach (var f in callbacks)
                {
                    prev = foldFunc(prev, f);
                }
            }
            return prev;
        }
    }

    //todo: having to copy paste this is annoying and errorprone, if anyone has a solution please tell me
    class AsyncEventHandler<O, T>
    {
        HashSet<Func<O, T, Task>> callbacks = new HashSet<Func<O, T, Task>>();

        public void Register(Func<O, T, Task> f)
        {
            lock (callbacks)
            {
                callbacks.Add(f);
            }
        }

        public void DeRegister(Func<O, T, Task> f)
        {
            lock (callbacks)
            {
                callbacks.Remove(f);
            }
        }

        public static AsyncEventHandler<O, T> operator +(AsyncEventHandler<O, T> that, Func<O, T, Task> f)
        {
            that.Register(f);
            return that;
        }

        public static AsyncEventHandler<O, T> operator -(AsyncEventHandler<O, T> that, Func<O, T, Task> f)
        {
            that.DeRegister(f);
            return that;
        }

        public Task Invoke(O origin, T payload)
        {
            List<Task> tasks;
            lock (callbacks)
            {
                tasks = new List<Task>(callbacks.Count);
                foreach (var f in callbacks)
                {
                    tasks.Add(f(origin, payload));
                }
            }
            return Task.WhenAll(tasks);
        }
    }
}

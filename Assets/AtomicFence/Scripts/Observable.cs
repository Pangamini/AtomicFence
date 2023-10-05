using System;
using System.Collections.Generic;
public class Observable<T>
{
    private T m_value;
    private Action<T> m_changed;
    public event Action<T> Changed
    {
        add
        {
            m_changed += value;
            m_changed(m_value);
        }
        remove => m_changed -= value;
    }

    public Observable()
    {}

    public Observable(T defaultValue)
    {
        m_value = defaultValue;
    }

    public T Value
    {
        get => m_value;
        set
        {
            if(EqualityComparer<T>.Default.Equals(m_value, value))
                return;
            
            m_value = value;
            m_changed?.Invoke(m_value);
        }
    }

    public View GetView() => new View(this);
    
    public readonly struct View
    {
        private readonly Observable<T> m_observable;
        public T Value => m_observable.Value;

        public View(Observable<T> observable)
        {
            m_observable = observable;
        }
        
        public event Action<T> Changed
        {
            add => m_observable.Changed += value;
            remove => m_observable.Changed -= value;
        }
    }
}

namespace SuperRender.EcmaScript.Runtime;

public sealed class Environment
{
    private readonly Dictionary<string, Binding> _bindings = new(StringComparer.Ordinal);
    public Environment? Parent { get; }

    public Environment(Environment? parent = null) => Parent = parent;

    public sealed class Binding
    {
        public JsValue Value { get; set; } = JsValue.Undefined;
        public bool Mutable { get; init; }
        public bool Initialized { get; set; }
    }

    public void CreateBinding(string name, bool mutable)
    {
        _bindings[name] = new Binding { Mutable = mutable };
    }

    public void InitializeBinding(string name, JsValue value)
    {
        if (_bindings.TryGetValue(name, out var binding))
        {
            binding.Value = value;
            binding.Initialized = true;
        }
    }

    public void CreateAndInitializeBinding(string name, bool mutable, JsValue value)
    {
        _bindings[name] = new Binding { Mutable = mutable, Value = value, Initialized = true };
    }

    public JsValue GetBinding(string name)
    {
        if (_bindings.TryGetValue(name, out var binding))
        {
            if (!binding.Initialized)
            {
                throw new Errors.JsReferenceError($"Cannot access '{name}' before initialization");
            }

            return binding.Value;
        }

        if (Parent is not null)
        {
            return Parent.GetBinding(name);
        }

        throw new Errors.JsReferenceError($"{name} is not defined");
    }

    public void SetBinding(string name, JsValue value)
    {
        if (_bindings.TryGetValue(name, out var binding))
        {
            if (!binding.Mutable)
            {
                throw new Errors.JsTypeError($"Assignment to constant variable '{name}'");
            }

            binding.Value = value;
            return;
        }

        if (Parent is not null)
        {
            Parent.SetBinding(name, value);
            return;
        }

        throw new Errors.JsReferenceError($"{name} is not defined");
    }

    public bool HasBinding(string name)
    {
        if (_bindings.ContainsKey(name))
        {
            return true;
        }

        return Parent?.HasBinding(name) ?? false;
    }

    public bool HasOwnBinding(string name) => _bindings.ContainsKey(name);
}

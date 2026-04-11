namespace SuperRender.EcmaScript.Runtime;

using System.Globalization;

public sealed class JsArray : JsObject
{
    private readonly List<JsValue> _dense = [];

    public int DenseLength => _dense.Count;

    public JsArray() { }

    public JsArray(IEnumerable<JsValue> items)
    {
        foreach (var item in items)
        {
            _dense.Add(item);
        }
    }

    public override JsValue Get(string name)
    {
        if (name == "length")
        {
            return JsNumber.Create(_dense.Count);
        }

        if (IsArrayIndex(name, out var index))
        {
            return index < (uint)_dense.Count ? _dense[(int)index] : Undefined;
        }

        return base.Get(name);
    }

    public override void Set(string name, JsValue value)
    {
        if (name == "length")
        {
            var newLen = (int)value.ToNumber();
            if (newLen < 0)
            {
                throw new Errors.JsRangeError("Invalid array length");
            }

            while (_dense.Count > newLen)
            {
                _dense.RemoveAt(_dense.Count - 1);
            }

            while (_dense.Count < newLen)
            {
                _dense.Add(Undefined);
            }

            return;
        }

        if (IsArrayIndex(name, out var index))
        {
            while ((uint)_dense.Count <= index)
            {
                _dense.Add(Undefined);
            }

            _dense[(int)index] = value;
            return;
        }

        base.Set(name, value);
    }

    public override bool HasProperty(string name)
    {
        if (name == "length")
        {
            return true;
        }

        if (IsArrayIndex(name, out var index))
        {
            return index < (uint)_dense.Count;
        }

        return base.HasProperty(name);
    }

    public override bool Delete(string name)
    {
        if (IsArrayIndex(name, out var index) && index < (uint)_dense.Count)
        {
            _dense[(int)index] = Undefined;
            return true;
        }

        return base.Delete(name);
    }

    public void Push(JsValue value) => _dense.Add(value);

    public JsValue Pop()
    {
        if (_dense.Count == 0)
        {
            return Undefined;
        }

        var last = _dense[^1];
        _dense.RemoveAt(_dense.Count - 1);
        return last;
    }

    public JsValue GetIndex(int index) =>
        index >= 0 && index < _dense.Count ? _dense[index] : Undefined;

    public static bool IsArrayIndex(string key, out uint index)
    {
        if (uint.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out index))
        {
            // Ensure it round-trips (no leading zeros except "0")
            return index.ToString(CultureInfo.InvariantCulture) == key && index < 0xFFFFFFFF;
        }

        index = 0;
        return false;
    }
}

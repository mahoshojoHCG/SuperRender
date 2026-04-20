using SuperRender.EcmaScript.Runtime;
using Xunit;

namespace SuperRender.EcmaScript.Tests.Runtime;

public class JsValueTests
{
    // ═══════════════════════════════════════════
    //  JsNumber
    // ═══════════════════════════════════════════

    [Fact]
    public void JsNumber_Create_ReturnsCorrectValue()
    {
        var num = JsNumber.Create(42);
        Assert.Equal(42.0, num.Value);
    }

    [Fact]
    public void JsNumber_Create_Zero_ReturnsCachedInstance()
    {
        var a = JsNumber.Create(0);
        var b = JsNumber.Create(0);
        Assert.Same(a, b);
    }

    [Fact]
    public void JsNumber_Create_NaN_ReturnsCachedInstance()
    {
        var a = JsNumber.Create(double.NaN);
        var b = JsNumber.NaN;
        Assert.Same(a, b);
    }

    [Fact]
    public void JsNumber_Create_PositiveInfinity_ReturnsCachedInstance()
    {
        var a = JsNumber.Create(double.PositiveInfinity);
        Assert.Same(a, JsNumber.PositiveInfinity);
    }

    [Fact]
    public void JsNumber_Create_NegativeInfinity_ReturnsCachedInstance()
    {
        var a = JsNumber.Create(double.NegativeInfinity);
        Assert.Same(a, JsNumber.NegativeInfinity);
    }

    [Fact]
    public void JsNumber_TypeOf_ReturnsNumber()
    {
        Assert.Equal("number", JsNumber.Create(1).TypeOf);
    }

    [Fact]
    public void JsNumber_ToBoolean_ZeroIsFalse()
    {
        Assert.False(JsNumber.Create(0).ToBoolean());
    }

    [Fact]
    public void JsNumber_ToBoolean_NaNIsFalse()
    {
        Assert.False(JsNumber.NaN.ToBoolean());
    }

    [Fact]
    public void JsNumber_ToBoolean_NonZeroIsTrue()
    {
        Assert.True(JsNumber.Create(1).ToBoolean());
        Assert.True(JsNumber.Create(-1).ToBoolean());
        Assert.True(JsNumber.Create(0.5).ToBoolean());
    }

    [Fact]
    public void JsNumber_ToJsString_Integer()
    {
        Assert.Equal("42", JsNumber.Create(42).ToJsString());
    }

    [Fact]
    public void JsNumber_ToJsString_NaN()
    {
        Assert.Equal("NaN", JsNumber.NaN.ToJsString());
    }

    [Fact]
    public void JsNumber_ToJsString_Infinity()
    {
        Assert.Equal("Infinity", JsNumber.PositiveInfinity.ToJsString());
        Assert.Equal("-Infinity", JsNumber.NegativeInfinity.ToJsString());
    }

    [Fact]
    public void JsNumber_ToJsString_Zero()
    {
        Assert.Equal("0", JsNumber.Create(0).ToJsString());
        Assert.Equal("0", JsNumber.NegativeZero.ToJsString()); // -0 prints as "0"
    }

    [Fact]
    public void JsNumber_StrictEquals_SameValueIsTrue()
    {
        Assert.True(JsNumber.Create(42).StrictEquals(JsNumber.Create(42)));
    }

    [Fact]
    public void JsNumber_StrictEquals_DifferentValueIsFalse()
    {
        Assert.False(JsNumber.Create(1).StrictEquals(JsNumber.Create(2)));
    }

    [Fact]
    public void JsNumber_StrictEquals_NaNIsNotEqualToNaN()
    {
        Assert.False(JsNumber.NaN.StrictEquals(JsNumber.NaN));
    }

    [Fact]
    public void JsNumber_StrictEquals_PositiveZeroEqualsNegativeZero()
    {
        Assert.True(JsNumber.Zero.StrictEquals(JsNumber.NegativeZero));
    }

    [Fact]
    public void JsNumber_StrictEquals_DifferentTypeIsFalse()
    {
        Assert.False(JsNumber.Create(1).StrictEquals(new JsString("1")));
    }

    // ═══════════════════════════════════════════
    //  JsString
    // ═══════════════════════════════════════════

    [Fact]
    public void JsString_Constructor_StoresValue()
    {
        var str = new JsString("hello");
        Assert.Equal("hello", str.Value);
    }

    [Fact]
    public void JsString_Length_ReturnsCorrectLength()
    {
        Assert.Equal(5, new JsString("hello").Length);
        Assert.Equal(0, new JsString("").Length);
    }

    [Fact]
    public void JsString_TypeOf_ReturnsString()
    {
        Assert.Equal("string", new JsString("x").TypeOf);
    }

    [Fact]
    public void JsString_ToNumber_ValidNumber()
    {
        Assert.Equal(42.0, new JsString("42").ToNumber());
    }

    [Fact]
    public void JsString_ToNumber_Float()
    {
        Assert.Equal(3.14, new JsString("3.14").ToNumber(), 5);
    }

    [Fact]
    public void JsString_ToNumber_EmptyStringIsZero()
    {
        Assert.Equal(0.0, new JsString("").ToNumber());
    }

    [Fact]
    public void JsString_ToNumber_WhitespaceIsZero()
    {
        Assert.Equal(0.0, new JsString("  ").ToNumber());
    }

    [Fact]
    public void JsString_ToNumber_InvalidIsNaN()
    {
        Assert.True(double.IsNaN(new JsString("abc").ToNumber()));
    }

    [Fact]
    public void JsString_ToBoolean_EmptyStringIsFalse()
    {
        Assert.False(new JsString("").ToBoolean());
    }

    [Fact]
    public void JsString_ToBoolean_NonEmptyIsTrue()
    {
        Assert.True(new JsString("a").ToBoolean());
        Assert.True(new JsString("false").ToBoolean()); // "false" is truthy!
    }

    [Fact]
    public void JsString_StrictEquals_SameValueIsTrue()
    {
        Assert.True(new JsString("hello").StrictEquals(new JsString("hello")));
    }

    [Fact]
    public void JsString_StrictEquals_DifferentValueIsFalse()
    {
        Assert.False(new JsString("a").StrictEquals(new JsString("b")));
    }

    [Fact]
    public void JsString_StrictEquals_DifferentTypeIsFalse()
    {
        Assert.False(new JsString("1").StrictEquals(JsNumber.Create(1)));
    }

    [Fact]
    public void JsString_Indexer_ReturnsCharacter()
    {
        var str = new JsString("abc");
        var ch = str[1];
        Assert.IsType<JsString>(ch);
        Assert.Equal("b", ((JsString)ch).Value);
    }

    [Fact]
    public void JsString_Indexer_OutOfRange_ReturnsUndefined()
    {
        var str = new JsString("abc");
        Assert.Same(JsValue.Undefined, str[10]);
        Assert.Same(JsValue.Undefined, str[-1]);
    }

    // ═══════════════════════════════════════════
    //  JsBoolean
    // ═══════════════════════════════════════════

    [Fact]
    public void JsBoolean_True_HasCorrectValue()
    {
        Assert.True(JsBoolean.True.Value);
    }

    [Fact]
    public void JsBoolean_False_HasCorrectValue()
    {
        Assert.False(JsBoolean.False.Value);
    }

    [Fact]
    public void JsBoolean_TypeOf_ReturnsBoolean()
    {
        Assert.Equal("boolean", JsBoolean.True.TypeOf);
    }

    [Fact]
    public void JsBoolean_ToNumber_TrueIsOne()
    {
        Assert.Equal(1.0, JsBoolean.True.ToNumber());
    }

    [Fact]
    public void JsBoolean_ToNumber_FalseIsZero()
    {
        Assert.Equal(0.0, JsBoolean.False.ToNumber());
    }

    [Fact]
    public void JsBoolean_ToBoolean_ReturnsSameValue()
    {
        Assert.True(JsBoolean.True.ToBoolean());
        Assert.False(JsBoolean.False.ToBoolean());
    }

    [Fact]
    public void JsBoolean_ToJsString_ReturnsCorrectString()
    {
        Assert.Equal("true", JsBoolean.True.ToJsString());
        Assert.Equal("false", JsBoolean.False.ToJsString());
    }

    [Fact]
    public void JsBoolean_StrictEquals_SameValueIsTrue()
    {
        Assert.True(JsBoolean.True.StrictEquals(JsBoolean.True));
        Assert.True(JsBoolean.False.StrictEquals(JsBoolean.False));
    }

    [Fact]
    public void JsBoolean_StrictEquals_DifferentValueIsFalse()
    {
        Assert.False(JsBoolean.True.StrictEquals(JsBoolean.False));
    }

    // ═══════════════════════════════════════════
    //  JsUndefined
    // ═══════════════════════════════════════════

    [Fact]
    public void JsUndefined_IsSingleton()
    {
        Assert.Same(JsUndefined.Instance, JsValue.Undefined);
    }

    [Fact]
    public void JsUndefined_TypeOf_ReturnsUndefined()
    {
        Assert.Equal("undefined", JsUndefined.Instance.TypeOf);
    }

    [Fact]
    public void JsUndefined_ToBoolean_ReturnsFalse()
    {
        Assert.False(JsUndefined.Instance.ToBoolean());
    }

    [Fact]
    public void JsUndefined_ToNumber_ReturnsNaN()
    {
        Assert.True(double.IsNaN(JsUndefined.Instance.ToNumber()));
    }

    [Fact]
    public void JsUndefined_ToJsString_ReturnsUndefined()
    {
        Assert.Equal("undefined", JsUndefined.Instance.ToJsString());
    }

    // ═══════════════════════════════════════════
    //  JsNull
    // ═══════════════════════════════════════════

    [Fact]
    public void JsNull_IsSingleton()
    {
        Assert.Same(JsNull.Instance, JsValue.Null);
    }

    [Fact]
    public void JsNull_TypeOf_ReturnsObject()
    {
        // This is correct per the JS spec: typeof null === "object"
        Assert.Equal("object", JsNull.Instance.TypeOf);
    }

    [Fact]
    public void JsNull_ToBoolean_ReturnsFalse()
    {
        Assert.False(JsNull.Instance.ToBoolean());
    }

    [Fact]
    public void JsNull_ToNumber_ReturnsZero()
    {
        Assert.Equal(0.0, JsNull.Instance.ToNumber());
    }

    [Fact]
    public void JsNull_ToJsString_ReturnsNull()
    {
        Assert.Equal("null", JsNull.Instance.ToJsString());
    }

    // ═══════════════════════════════════════════
    //  AbstractEquals
    // ═══════════════════════════════════════════

    [Fact]
    public void AbstractEquals_NullEqualsUndefined()
    {
        Assert.True(JsValue.Null.AbstractEquals(JsValue.Undefined));
        Assert.True(JsValue.Undefined.AbstractEquals(JsValue.Null));
    }

    [Fact]
    public void AbstractEquals_NumberAndString_CoercesToNumber()
    {
        Assert.True(JsNumber.Create(1).AbstractEquals(new JsString("1")));
        Assert.True(new JsString("42").AbstractEquals(JsNumber.Create(42)));
    }

    [Fact]
    public void AbstractEquals_BooleanCoercesToNumber()
    {
        // true == 1
        Assert.True(JsBoolean.True.AbstractEquals(JsNumber.Create(1)));
        // false == 0
        Assert.True(JsBoolean.False.AbstractEquals(JsNumber.Create(0)));
    }

    [Fact]
    public void AbstractEquals_SameType_SameValue_IsTrue()
    {
        Assert.True(JsNumber.Create(5).AbstractEquals(JsNumber.Create(5)));
        Assert.True(new JsString("abc").AbstractEquals(new JsString("abc")));
    }

    [Fact]
    public void AbstractEquals_NullNotEqualsZero()
    {
        Assert.False(JsValue.Null.AbstractEquals(JsNumber.Create(0)));
    }

    [Fact]
    public void AbstractEquals_UndefinedNotEqualsZero()
    {
        Assert.False(JsValue.Undefined.AbstractEquals(JsNumber.Create(0)));
    }

    [Fact]
    public void AbstractEquals_UndefinedNotEqualsFalse()
    {
        Assert.False(JsValue.Undefined.AbstractEquals(JsBoolean.False));
    }

    // ═══════════════════════════════════════════
    //  JsDynamicObject
    // ═══════════════════════════════════════════

    [Fact]
    public void JsObject_GetSet_BasicProperty()
    {
        var obj = new JsDynamicObject();
        obj.Set("name", new JsString("test"));
        var result = obj.Get("name");
        Assert.IsType<JsString>(result);
        Assert.Equal("test", ((JsString)result).Value);
    }

    [Fact]
    public void JsObject_Get_MissingProperty_ReturnsUndefined()
    {
        var obj = new JsDynamicObject();
        Assert.Same(JsValue.Undefined, obj.Get("missing"));
    }

    [Fact]
    public void JsObject_TypeOf_ReturnsObject()
    {
        Assert.Equal("object", new JsDynamicObject().TypeOf);
    }

    [Fact]
    public void JsObject_ToBoolean_ReturnsTrue()
    {
        // All objects are truthy in JS
        Assert.True(new JsDynamicObject().ToBoolean());
    }

    [Fact]
    public void JsObject_HasProperty_ReturnsTrueForExisting()
    {
        var obj = new JsDynamicObject();
        obj.Set("key", JsNumber.Create(1));
        Assert.True(obj.HasProperty("key"));
    }

    [Fact]
    public void JsObject_HasProperty_ReturnsFalseForMissing()
    {
        var obj = new JsDynamicObject();
        Assert.False(obj.HasProperty("missing"));
    }

    [Fact]
    public void JsObject_PrototypeChain_InheritsProperties()
    {
        var parent = new JsDynamicObject();
        parent.Set("inherited", new JsString("from parent"));

        var child = new JsDynamicObject { Prototype = parent };
        var result = child.Get("inherited");
        Assert.IsType<JsString>(result);
        Assert.Equal("from parent", ((JsString)result).Value);
    }

    [Fact]
    public void JsObject_PrototypeChain_OwnPropertyShadowsInherited()
    {
        var parent = new JsDynamicObject();
        parent.Set("x", JsNumber.Create(1));

        var child = new JsDynamicObject { Prototype = parent };
        child.Set("x", JsNumber.Create(2));

        var result = child.Get("x");
        Assert.Equal(2.0, ((JsNumber)result).Value);
    }

    [Fact]
    public void JsObject_Delete_RemovesProperty()
    {
        var obj = new JsDynamicObject();
        obj.Set("key", JsNumber.Create(1));
        obj.Delete("key");
        Assert.False(obj.HasProperty("key"));
    }

    [Fact]
    public void JsObject_OwnPropertyKeys_ReturnsInsertionOrder()
    {
        var obj = new JsDynamicObject();
        obj.Set("b", JsNumber.Create(2));
        obj.Set("a", JsNumber.Create(1));
        obj.Set("c", JsNumber.Create(3));

        var keys = obj.OwnPropertyKeys().ToList();
        Assert.Equal(["b", "a", "c"], keys);
    }

    // ═══════════════════════════════════════════
    //  JsArray
    // ═══════════════════════════════════════════

    [Fact]
    public void JsArray_Push_IncreasesLength()
    {
        var arr = new JsArray();
        arr.Push(JsNumber.Create(1));
        arr.Push(JsNumber.Create(2));
        Assert.Equal(2, arr.DenseLength);
    }

    [Fact]
    public void JsArray_Pop_ReturnsAndRemovesLastElement()
    {
        var arr = new JsArray();
        arr.Push(JsNumber.Create(1));
        arr.Push(JsNumber.Create(2));
        var popped = arr.Pop();
        Assert.Equal(2.0, ((JsNumber)popped).Value);
        Assert.Equal(1, arr.DenseLength);
    }

    [Fact]
    public void JsArray_Pop_EmptyArray_ReturnsUndefined()
    {
        var arr = new JsArray();
        Assert.Same(JsValue.Undefined, arr.Pop());
    }

    [Fact]
    public void JsArray_GetIndex_ReturnsCorrectElement()
    {
        var arr = new JsArray();
        arr.Push(new JsString("a"));
        arr.Push(new JsString("b"));
        Assert.Equal("b", ((JsString)arr.GetIndex(1)).Value);
    }

    [Fact]
    public void JsArray_GetIndex_OutOfRange_ReturnsUndefined()
    {
        var arr = new JsArray();
        Assert.Same(JsValue.Undefined, arr.GetIndex(5));
    }

    [Fact]
    public void JsArray_Get_Length_ReturnsCorrectValue()
    {
        var arr = new JsArray();
        arr.Push(JsNumber.Create(1));
        arr.Push(JsNumber.Create(2));
        arr.Push(JsNumber.Create(3));
        var len = arr.Get("length");
        Assert.Equal(3.0, ((JsNumber)len).Value);
    }

    [Fact]
    public void JsArray_Get_ByStringIndex_ReturnsCorrectElement()
    {
        var arr = new JsArray();
        arr.Push(new JsString("zero"));
        arr.Push(new JsString("one"));
        var result = arr.Get("1");
        Assert.Equal("one", ((JsString)result).Value);
    }

    [Fact]
    public void JsArray_Set_ByStringIndex_ExtendsArray()
    {
        var arr = new JsArray();
        arr.Set("3", new JsString("three"));
        // Array should be extended with undefined values
        Assert.Equal(4, arr.DenseLength);
        Assert.Same(JsValue.Undefined, arr.GetIndex(0));
    }

    [Fact]
    public void JsArray_Constructor_WithEnumerable_PopulatesArray()
    {
        var items = new JsValue[] { JsNumber.Create(1), JsNumber.Create(2), JsNumber.Create(3) };
        var arr = new JsArray(items);
        Assert.Equal(3, arr.DenseLength);
        Assert.Equal(1.0, ((JsNumber)arr.GetIndex(0)).Value);
    }

    // ═══════════════════════════════════════════
    //  JsFunction
    // ═══════════════════════════════════════════

    [Fact]
    public void JsFunction_TypeOf_ReturnsFunction()
    {
        var fn = JsFunction.CreateNative("test", (_, _) => JsValue.Undefined, 0);
        Assert.Equal("function", fn.TypeOf);
    }

    [Fact]
    public void JsFunction_Call_InvokesTarget()
    {
        var called = false;
        var fn = JsFunction.CreateNative("test", (_, _) =>
        {
            called = true;
            return JsNumber.Create(42);
        }, 0);

        var result = fn.Call(JsValue.Undefined, []);
        Assert.True(called);
        Assert.Equal(42.0, ((JsNumber)result).Value);
    }

    [Fact]
    public void JsFunction_Get_Name_ReturnsName()
    {
        var fn = JsFunction.CreateNative("myFunc", (_, _) => JsValue.Undefined, 2);
        var name = fn.Get("name");
        Assert.Equal("myFunc", ((JsString)name).Value);
    }

    [Fact]
    public void JsFunction_Get_Length_ReturnsParamCount()
    {
        var fn = JsFunction.CreateNative("myFunc", (_, _) => JsValue.Undefined, 3);
        var length = fn.Get("length");
        Assert.Equal(3.0, ((JsNumber)length).Value);
    }

    // ═══════════════════════════════════════════
    //  JsValue.FromObject
    // ═══════════════════════════════════════════

    [Fact]
    public void FromObject_Null_ReturnsJsNull()
    {
        Assert.Same(JsValue.Null, JsValue.FromObject(null));
    }

    [Fact]
    public void FromObject_Bool_ReturnsJsBoolean()
    {
        Assert.Same(JsValue.True, JsValue.FromObject(true));
        Assert.Same(JsValue.False, JsValue.FromObject(false));
    }

    [Fact]
    public void FromObject_Int_ReturnsJsNumber()
    {
        var result = JsValue.FromObject(42);
        Assert.IsType<JsNumber>(result);
        Assert.Equal(42.0, ((JsNumber)result).Value);
    }

    [Fact]
    public void FromObject_Double_ReturnsJsNumber()
    {
        var result = JsValue.FromObject(3.14);
        Assert.IsType<JsNumber>(result);
        Assert.Equal(3.14, ((JsNumber)result).Value, 5);
    }

    [Fact]
    public void FromObject_String_ReturnsJsString()
    {
        var result = JsValue.FromObject("hello");
        Assert.IsType<JsString>(result);
        Assert.Equal("hello", ((JsString)result).Value);
    }

    [Fact]
    public void FromObject_JsValue_ReturnsSameInstance()
    {
        var original = JsNumber.Create(7);
        var result = JsValue.FromObject(original);
        Assert.Same(original, result);
    }

    // ═══════════════════════════════════════════
    //  JsSymbol
    // ═══════════════════════════════════════════

    [Fact]
    public void JsSymbol_TypeOf_ReturnsSymbol()
    {
        var sym = new JsSymbol("test");
        Assert.Equal("symbol", sym.TypeOf);
    }

    [Fact]
    public void JsSymbol_ToBoolean_ReturnsTrue()
    {
        Assert.True(new JsSymbol().ToBoolean());
    }

    [Fact]
    public void JsSymbol_StrictEquals_SameInstanceIsTrue()
    {
        var sym = new JsSymbol("x");
        Assert.True(sym.StrictEquals(sym));
    }

    [Fact]
    public void JsSymbol_StrictEquals_DifferentInstanceIsFalse()
    {
        var a = new JsSymbol("x");
        var b = new JsSymbol("x");
        Assert.False(a.StrictEquals(b));
    }
}

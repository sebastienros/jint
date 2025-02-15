using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Native.Function;

/// <summary>
/// https://tc39.es/ecma262/#sec-function-constructor
/// </summary>
public sealed class FunctionConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("Function");

    internal FunctionConstructor(
        Engine engine,
        Realm realm,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        PrototypeObject = new FunctionPrototype(engine, realm, objectPrototype);
        _prototype = PrototypeObject;
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
    }

    internal FunctionPrototype PrototypeObject { get; }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, thisObject);
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var function = CreateDynamicFunction(
            this,
            newTarget,
            FunctionKind.Normal,
            arguments);

        return function;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiatefunctionobject
    /// </summary>
    internal Function InstantiateFunctionObject(
        JintFunctionDefinition functionDeclaration,
        Environment scope,
        PrivateEnvironment? privateEnv)
    {
        var function = functionDeclaration.Function;
        if (!function.Generator)
        {
            return function.Async
                ? InstantiateAsyncFunctionObject(functionDeclaration, scope, privateEnv)
                : InstantiateOrdinaryFunctionObject(functionDeclaration, scope, privateEnv);
        }
        else
        {
            return InstantiateGeneratorFunctionObject(functionDeclaration, scope, privateEnv);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiateasyncfunctionobject
    /// </summary>
    private ScriptFunction InstantiateAsyncFunctionObject(
        JintFunctionDefinition functionDeclaration,
        Environment env,
        PrivateEnvironment? privateEnv)
    {
        var F = OrdinaryFunctionCreate(
            _realm.Intrinsics.AsyncFunction.PrototypeObject,
            functionDeclaration,
            functionDeclaration.ThisMode,
            env,
            privateEnv);

        F.SetFunctionName(functionDeclaration.Name ?? "default");

        return F;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiateordinaryfunctionobject
    /// </summary>
    private ScriptFunction InstantiateOrdinaryFunctionObject(
        JintFunctionDefinition functionDeclaration,
        Environment env,
        PrivateEnvironment? privateEnv)
    {
        var F = OrdinaryFunctionCreate(
            _realm.Intrinsics.Function.PrototypeObject,
            functionDeclaration,
            functionDeclaration.ThisMode,
            env,
            privateEnv);

        var name = functionDeclaration.Name ?? "default";
        F.SetFunctionName(name);
        F.MakeConstructor();
        return F;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiategeneratorfunctionobject
    /// </summary>
    private ScriptFunction InstantiateGeneratorFunctionObject(
        JintFunctionDefinition functionDeclaration,
        Environment scope,
        PrivateEnvironment? privateScope)
    {
        var thisMode = functionDeclaration.Strict || _engine._isStrict
            ? FunctionThisMode.Strict
            : FunctionThisMode.Global;

        var name = functionDeclaration.Function.Id?.Name ?? "default";
        var F = OrdinaryFunctionCreate(
            _realm.Intrinsics.GeneratorFunction.PrototypeObject,
            functionDeclaration,
            thisMode,
            scope,
            privateScope);

        F.SetFunctionName(name);

        var prototype = OrdinaryObjectCreate(_engine, _realm.Intrinsics.GeneratorFunction.PrototypeObject.PrototypeObject);
        F.DefinePropertyOrThrow(CommonProperties.Prototype, new PropertyDescriptor(prototype, PropertyFlag.Writable));

        return F;
    }
}

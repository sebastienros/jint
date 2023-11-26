//HintName: MathInstance.g.cs
#nullable enable

#pragma warning disable CS0219

using System.Diagnostics;
using System.Runtime.CompilerServices;

using Jint.HighPerformance;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Math;

public partial class MathInstance
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __E_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __E_property
    {
        [DebuggerStepThrough]
        get { return __E_property_backingField ??= new PropertyDescriptor(MathInstance.E, PropertyFlag.AllForbidden); }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __LN10_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __LN10_property
    {
        [DebuggerStepThrough]
        get { return __LN10_property_backingField ??= new PropertyDescriptor(MathInstance.LN10, PropertyFlag.AllForbidden); }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __LN2_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __LN2_property
    {
        [DebuggerStepThrough]
        get { return __LN2_property_backingField ??= new PropertyDescriptor(MathInstance.LN2, PropertyFlag.AllForbidden); }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __LOG2E_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __LOG2E_property
    {
        [DebuggerStepThrough]
        get { return __LOG2E_property_backingField ??= new PropertyDescriptor(MathInstance.LOG2E, PropertyFlag.AllForbidden); }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __LOG10E_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __LOG10E_property
    {
        [DebuggerStepThrough]
        get { return __LOG10E_property_backingField ??= new PropertyDescriptor(MathInstance.LOG10E, PropertyFlag.AllForbidden); }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __PI_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __PI_property
    {
        [DebuggerStepThrough]
        get { return __PI_property_backingField ??= new PropertyDescriptor(MathInstance.PI, PropertyFlag.AllForbidden); }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __SQRT1_2_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __SQRT1_2_property
    {
        [DebuggerStepThrough]
        get { return __SQRT1_2_property_backingField ??= new PropertyDescriptor(MathInstance.SQRT1_2, PropertyFlag.AllForbidden); }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __SQRT2_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __SQRT2_property
    {
        [DebuggerStepThrough]
        get { return __SQRT2_property_backingField ??= new PropertyDescriptor(MathInstance.SQRT2, PropertyFlag.AllForbidden); }
    }



    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __abs_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __abs { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __abs_backingField ??= new AbsFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __abs_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __abs_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __abs_property_backingField ??= new PropertyDescriptor(__abs, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __acos_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __acos { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __acos_backingField ??= new AcosFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __acos_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __acos_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __acos_property_backingField ??= new PropertyDescriptor(__acos, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __acosh_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __acosh { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __acosh_backingField ??= new AcoshFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __acosh_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __acosh_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __acosh_property_backingField ??= new PropertyDescriptor(__acosh, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __asin_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __asin { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __asin_backingField ??= new AsinFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __asin_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __asin_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __asin_property_backingField ??= new PropertyDescriptor(__asin, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __asinh_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __asinh { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __asinh_backingField ??= new AsinhFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __asinh_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __asinh_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __asinh_property_backingField ??= new PropertyDescriptor(__asinh, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __atan_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __atan { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __atan_backingField ??= new AtanFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __atan_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __atan_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __atan_property_backingField ??= new PropertyDescriptor(__atan, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __atan2_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __atan2 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __atan2_backingField ??= new Atan2Function(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __atan2_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __atan2_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __atan2_property_backingField ??= new PropertyDescriptor(__atan2, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __atanh_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __atanh { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __atanh_backingField ??= new AtanhFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __atanh_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __atanh_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __atanh_property_backingField ??= new PropertyDescriptor(__atanh, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __cbrt_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __cbrt { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __cbrt_backingField ??= new CbrtFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __cbrt_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __cbrt_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __cbrt_property_backingField ??= new PropertyDescriptor(__cbrt, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __ceil_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __ceil { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __ceil_backingField ??= new CeilFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __ceil_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __ceil_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __ceil_property_backingField ??= new PropertyDescriptor(__ceil, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __clz32_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __clz32 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __clz32_backingField ??= new Clz32Function(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __clz32_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __clz32_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __clz32_property_backingField ??= new PropertyDescriptor(__clz32, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __cos_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __cos { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __cos_backingField ??= new CosFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __cos_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __cos_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __cos_property_backingField ??= new PropertyDescriptor(__cos, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __cosh_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __cosh { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __cosh_backingField ??= new CoshFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __cosh_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __cosh_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __cosh_property_backingField ??= new PropertyDescriptor(__cosh, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __exp_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __exp { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __exp_backingField ??= new ExpFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __exp_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __exp_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __exp_property_backingField ??= new PropertyDescriptor(__exp, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __floor_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __floor { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __floor_backingField ??= new FloorFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __floor_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __floor_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __floor_property_backingField ??= new PropertyDescriptor(__floor, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __fround_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __fround { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __fround_backingField ??= new FroundFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __fround_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __fround_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __fround_property_backingField ??= new PropertyDescriptor(__fround, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __hypot_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __hypot { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __hypot_backingField ??= new HypotFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __hypot_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __hypot_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __hypot_property_backingField ??= new PropertyDescriptor(__hypot, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __imul_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __imul { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __imul_backingField ??= new ImulFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __imul_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __imul_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __imul_property_backingField ??= new PropertyDescriptor(__imul, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __log_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __log { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __log_backingField ??= new LogFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __log_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __log_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __log_property_backingField ??= new PropertyDescriptor(__log, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __log10_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __log10 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __log10_backingField ??= new Log10Function(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __log10_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __log10_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __log10_property_backingField ??= new PropertyDescriptor(__log10, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __log2_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __log2 { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __log2_backingField ??= new Log2Function(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __log2_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __log2_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __log2_property_backingField ??= new PropertyDescriptor(__log2, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __max_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __max { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __max_backingField ??= new MaxFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __max_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __max_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __max_property_backingField ??= new PropertyDescriptor(__max, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __min_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __min { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __min_backingField ??= new MinFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __min_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __min_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __min_property_backingField ??= new PropertyDescriptor(__min, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __pow_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __pow { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __pow_backingField ??= new PowFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __pow_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __pow_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __pow_property_backingField ??= new PropertyDescriptor(__pow, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __random_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __random { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __random_backingField ??= new RandomFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __random_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __random_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __random_property_backingField ??= new PropertyDescriptor(__random, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __round_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __round { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __round_backingField ??= new RoundFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __round_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __round_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __round_property_backingField ??= new PropertyDescriptor(__round, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __sign_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __sign { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __sign_backingField ??= new SignFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __sign_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __sign_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __sign_property_backingField ??= new PropertyDescriptor(__sign, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __sin_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __sin { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __sin_backingField ??= new SinFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __sin_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __sin_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __sin_property_backingField ??= new PropertyDescriptor(__sin, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __sinh_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __sinh { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __sinh_backingField ??= new SinhFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __sinh_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __sinh_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __sinh_property_backingField ??= new PropertyDescriptor(__sinh, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __sqrt_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __sqrt { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __sqrt_backingField ??= new SqrtFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __sqrt_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __sqrt_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __sqrt_property_backingField ??= new PropertyDescriptor(__sqrt, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __tan_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __tan { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __tan_backingField ??= new TanFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __tan_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __tan_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __tan_property_backingField ??= new PropertyDescriptor(__tan, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __tanh_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __tanh { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __tanh_backingField ??= new TanhFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __tanh_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __tanh_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __tanh_property_backingField ??= new PropertyDescriptor(__tanh, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance? __trunc_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private FunctionInstance __trunc { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return __trunc_backingField ??= new TruncFunction(this); } }
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor? __trunc_property_backingField;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)] private PropertyDescriptor __trunc_property { [MethodImpl(MethodImplOptions.AggressiveInlining)] [DebuggerStepThrough] get { return __trunc_property_backingField ??= new PropertyDescriptor(__trunc, PropertyFlag.Writable | PropertyFlag.Configurable); } }


    protected override void Initialize()
    {
        CreateProperties();
        CreateSymbols();
    }

    protected override bool TryGetProperty(JsValue property, out PropertyDescriptor? descriptor)
    {
        if (property is JsString jsString)
        {
            var str = jsString._value;
            PropertyDescriptor? match = null;
            switch (str.Length)
            {
                case 1:
                    if (str == "E")
                    {
                        match = __E_property;
                    }
                    break;

                case 2:
                    if (str == "PI")
                    {
                        match = __PI_property;
                    }
                    break;

                case 3:
                    if (str == "abs")
                    {
                        match = __abs_property;
                    }
                    else if (str == "cos")
                    {
                        match = __cos_property;
                    }
                    else if (str == "exp")
                    {
                        match = __exp_property;
                    }
                    else if (str == "log")
                    {
                        match = __log_property;
                    }
                    else if (str == "max")
                    {
                        match = __max_property;
                    }
                    else if (str == "min")
                    {
                        match = __min_property;
                    }
                    else if (str == "pow")
                    {
                        match = __pow_property;
                    }
                    else if (str == "sin")
                    {
                        match = __sin_property;
                    }
                    else if (str == "tan")
                    {
                        match = __tan_property;
                    }
                    else if (str == "LN2")
                    {
                        match = __LN2_property;
                    }
                    break;

                case 4:
                    if (str == "acos")
                    {
                        match = __acos_property;
                    }
                    else if (str == "asin")
                    {
                        match = __asin_property;
                    }
                    else if (str == "atan")
                    {
                        match = __atan_property;
                    }
                    else if (str == "cbrt")
                    {
                        match = __cbrt_property;
                    }
                    else if (str == "ceil")
                    {
                        match = __ceil_property;
                    }
                    else if (str == "cosh")
                    {
                        match = __cosh_property;
                    }
                    else if (str == "imul")
                    {
                        match = __imul_property;
                    }
                    else if (str == "log2")
                    {
                        match = __log2_property;
                    }
                    else if (str == "sign")
                    {
                        match = __sign_property;
                    }
                    else if (str == "sinh")
                    {
                        match = __sinh_property;
                    }
                    else if (str == "sqrt")
                    {
                        match = __sqrt_property;
                    }
                    else if (str == "tanh")
                    {
                        match = __tanh_property;
                    }
                    else if (str == "LN10")
                    {
                        match = __LN10_property;
                    }
                    break;

                case 5:
                    if (str == "acosh")
                    {
                        match = __acosh_property;
                    }
                    else if (str == "asinh")
                    {
                        match = __asinh_property;
                    }
                    else if (str == "atan2")
                    {
                        match = __atan2_property;
                    }
                    else if (str == "atanh")
                    {
                        match = __atanh_property;
                    }
                    else if (str == "clz32")
                    {
                        match = __clz32_property;
                    }
                    else if (str == "floor")
                    {
                        match = __floor_property;
                    }
                    else if (str == "hypot")
                    {
                        match = __hypot_property;
                    }
                    else if (str == "log10")
                    {
                        match = __log10_property;
                    }
                    else if (str == "round")
                    {
                        match = __round_property;
                    }
                    else if (str == "trunc")
                    {
                        match = __trunc_property;
                    }
                    else if (str == "LOG2E")
                    {
                        match = __LOG2E_property;
                    }
                    else if (str == "SQRT2")
                    {
                        match = __SQRT2_property;
                    }
                    break;

                case 6:
                    var disc6 = str[0];
                    if (disc6 == 'f' && str == "fround")
                    {
                        match = __fround_property;
                    }
                    else if (disc6 == 'r' && str == "random")
                    {
                        match = __random_property;
                    }
                    else if (disc6 == 'L' && str == "LOG10E")
                    {
                        match = __LOG10E_property;
                    }
                    break;

                case 7:
                    if (str == "SQRT1_2")
                    {
                        match = __SQRT1_2_property;
                    }
                    break;

            }

            if (match is not null)
            {
                descriptor = match;
                return true;
            }
        }
        return base.TryGetProperty(property, out descriptor);
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (property is JsString jsString)
        {
            var str = jsString._value;
            PropertyDescriptor? match = null;
            switch (str.Length)
            {
                case 1:
                    if (str == "E")
                    {
                        match = __E_property;
                    }
                    break;

                case 2:
                    if (str == "PI")
                    {
                        match = __PI_property;
                    }
                    break;

                case 3:
                    if (str == "abs")
                    {
                        match = __abs_property;
                    }
                    else if (str == "cos")
                    {
                        match = __cos_property;
                    }
                    else if (str == "exp")
                    {
                        match = __exp_property;
                    }
                    else if (str == "log")
                    {
                        match = __log_property;
                    }
                    else if (str == "max")
                    {
                        match = __max_property;
                    }
                    else if (str == "min")
                    {
                        match = __min_property;
                    }
                    else if (str == "pow")
                    {
                        match = __pow_property;
                    }
                    else if (str == "sin")
                    {
                        match = __sin_property;
                    }
                    else if (str == "tan")
                    {
                        match = __tan_property;
                    }
                    else if (str == "LN2")
                    {
                        match = __LN2_property;
                    }
                    break;

                case 4:
                    if (str == "acos")
                    {
                        match = __acos_property;
                    }
                    else if (str == "asin")
                    {
                        match = __asin_property;
                    }
                    else if (str == "atan")
                    {
                        match = __atan_property;
                    }
                    else if (str == "cbrt")
                    {
                        match = __cbrt_property;
                    }
                    else if (str == "ceil")
                    {
                        match = __ceil_property;
                    }
                    else if (str == "cosh")
                    {
                        match = __cosh_property;
                    }
                    else if (str == "imul")
                    {
                        match = __imul_property;
                    }
                    else if (str == "log2")
                    {
                        match = __log2_property;
                    }
                    else if (str == "sign")
                    {
                        match = __sign_property;
                    }
                    else if (str == "sinh")
                    {
                        match = __sinh_property;
                    }
                    else if (str == "sqrt")
                    {
                        match = __sqrt_property;
                    }
                    else if (str == "tanh")
                    {
                        match = __tanh_property;
                    }
                    else if (str == "LN10")
                    {
                        match = __LN10_property;
                    }
                    break;

                case 5:
                    if (str == "acosh")
                    {
                        match = __acosh_property;
                    }
                    else if (str == "asinh")
                    {
                        match = __asinh_property;
                    }
                    else if (str == "atan2")
                    {
                        match = __atan2_property;
                    }
                    else if (str == "atanh")
                    {
                        match = __atanh_property;
                    }
                    else if (str == "clz32")
                    {
                        match = __clz32_property;
                    }
                    else if (str == "floor")
                    {
                        match = __floor_property;
                    }
                    else if (str == "hypot")
                    {
                        match = __hypot_property;
                    }
                    else if (str == "log10")
                    {
                        match = __log10_property;
                    }
                    else if (str == "round")
                    {
                        match = __round_property;
                    }
                    else if (str == "trunc")
                    {
                        match = __trunc_property;
                    }
                    else if (str == "LOG2E")
                    {
                        match = __LOG2E_property;
                    }
                    else if (str == "SQRT2")
                    {
                        match = __SQRT2_property;
                    }
                    break;

                case 6:
                    var disc6 = str[0];
                    if (disc6 == 'f' && str == "fround")
                    {
                        match = __fround_property;
                    }
                    else if (disc6 == 'r' && str == "random")
                    {
                        match = __random_property;
                    }
                    else if (disc6 == 'L' && str == "LOG10E")
                    {
                        match = __LOG10E_property;
                    }
                    break;

                case 7:
                    if (str == "SQRT1_2")
                    {
                        match = __SQRT1_2_property;
                    }
                    break;

            }

            if (match is not null)
            {
                return match;
            }
        }
        return base.GetOwnProperty(property);
    }

    protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (property is JsString jsString)
        {
            var str = jsString._value;
            PropertyDescriptor? match = null;
            switch (str.Length)
            {
                case 1:
                    if (str == "E")
                    {
                        match = __E_property;
                    }
                    break;

                case 2:
                    if (str == "PI")
                    {
                        match = __PI_property;
                    }
                    break;

                case 3:
                    if (str == "abs")
                    {
                        match = __abs_property;
                    }
                    else if (str == "cos")
                    {
                        match = __cos_property;
                    }
                    else if (str == "exp")
                    {
                        match = __exp_property;
                    }
                    else if (str == "log")
                    {
                        match = __log_property;
                    }
                    else if (str == "max")
                    {
                        match = __max_property;
                    }
                    else if (str == "min")
                    {
                        match = __min_property;
                    }
                    else if (str == "pow")
                    {
                        match = __pow_property;
                    }
                    else if (str == "sin")
                    {
                        match = __sin_property;
                    }
                    else if (str == "tan")
                    {
                        match = __tan_property;
                    }
                    else if (str == "LN2")
                    {
                        match = __LN2_property;
                    }
                    break;

                case 4:
                    if (str == "acos")
                    {
                        match = __acos_property;
                    }
                    else if (str == "asin")
                    {
                        match = __asin_property;
                    }
                    else if (str == "atan")
                    {
                        match = __atan_property;
                    }
                    else if (str == "cbrt")
                    {
                        match = __cbrt_property;
                    }
                    else if (str == "ceil")
                    {
                        match = __ceil_property;
                    }
                    else if (str == "cosh")
                    {
                        match = __cosh_property;
                    }
                    else if (str == "imul")
                    {
                        match = __imul_property;
                    }
                    else if (str == "log2")
                    {
                        match = __log2_property;
                    }
                    else if (str == "sign")
                    {
                        match = __sign_property;
                    }
                    else if (str == "sinh")
                    {
                        match = __sinh_property;
                    }
                    else if (str == "sqrt")
                    {
                        match = __sqrt_property;
                    }
                    else if (str == "tanh")
                    {
                        match = __tanh_property;
                    }
                    else if (str == "LN10")
                    {
                        match = __LN10_property;
                    }
                    break;

                case 5:
                    if (str == "acosh")
                    {
                        match = __acosh_property;
                    }
                    else if (str == "asinh")
                    {
                        match = __asinh_property;
                    }
                    else if (str == "atan2")
                    {
                        match = __atan2_property;
                    }
                    else if (str == "atanh")
                    {
                        match = __atanh_property;
                    }
                    else if (str == "clz32")
                    {
                        match = __clz32_property;
                    }
                    else if (str == "floor")
                    {
                        match = __floor_property;
                    }
                    else if (str == "hypot")
                    {
                        match = __hypot_property;
                    }
                    else if (str == "log10")
                    {
                        match = __log10_property;
                    }
                    else if (str == "round")
                    {
                        match = __round_property;
                    }
                    else if (str == "trunc")
                    {
                        match = __trunc_property;
                    }
                    else if (str == "LOG2E")
                    {
                        match = __LOG2E_property;
                    }
                    else if (str == "SQRT2")
                    {
                        match = __SQRT2_property;
                    }
                    break;

                case 6:
                    var disc6 = str[0];
                    if (disc6 == 'f' && str == "fround")
                    {
                        match = __fround_property;
                    }
                    else if (disc6 == 'r' && str == "random")
                    {
                        match = __random_property;
                    }
                    else if (disc6 == 'L' && str == "LOG10E")
                    {
                        match = __LOG10E_property;
                    }
                    break;

                case 7:
                    if (str == "SQRT1_2")
                    {
                        match = __SQRT1_2_property;
                    }
                    break;

            }

            if (match is not null)
            {
throw new System.Exception("TROUBLE");
          //      return;
            }
        }
    }
    private sealed class AbsFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("abs");
        private readonly MathInstance _host;

        public AbsFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Abs(arguments.At(0));
        }

        public override string ToString() => "function abs() { [native code] }";
    }

    private sealed class AcosFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("acos");
        private readonly MathInstance _host;

        public AcosFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Acos(arguments.At(0));
        }

        public override string ToString() => "function acos() { [native code] }";
    }

    private sealed class AcoshFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("acosh");
        private readonly MathInstance _host;

        public AcoshFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Acosh(arguments.At(0));
        }

        public override string ToString() => "function acosh() { [native code] }";
    }

    private sealed class AsinFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("asin");
        private readonly MathInstance _host;

        public AsinFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Asin(arguments.At(0));
        }

        public override string ToString() => "function asin() { [native code] }";
    }

    private sealed class AsinhFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("asinh");
        private readonly MathInstance _host;

        public AsinhFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Asinh(arguments.At(0));
        }

        public override string ToString() => "function asinh() { [native code] }";
    }

    private sealed class AtanFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("atan");
        private readonly MathInstance _host;

        public AtanFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Atan(arguments.At(0));
        }

        public override string ToString() => "function atan() { [native code] }";
    }

    private sealed class Atan2Function : FunctionInstance
    {
        private static readonly JsString _name = new JsString("atan2");
        private readonly MathInstance _host;

        public Atan2Function(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(2), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Atan2(arguments.At(0), arguments.At(1));
        }

        public override string ToString() => "function atan2() { [native code] }";
    }

    private sealed class AtanhFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("atanh");
        private readonly MathInstance _host;

        public AtanhFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Atanh(arguments.At(0));
        }

        public override string ToString() => "function atanh() { [native code] }";
    }

    private sealed class CbrtFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("cbrt");
        private readonly MathInstance _host;

        public CbrtFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Cbrt(arguments.At(0));
        }

        public override string ToString() => "function cbrt() { [native code] }";
    }

    private sealed class CeilFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("ceil");
        private readonly MathInstance _host;

        public CeilFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Ceil(arguments.At(0));
        }

        public override string ToString() => "function ceil() { [native code] }";
    }

    private sealed class Clz32Function : FunctionInstance
    {
        private static readonly JsString _name = new JsString("clz32");
        private readonly MathInstance _host;

        public Clz32Function(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Clz32(arguments.At(0));
        }

        public override string ToString() => "function clz32() { [native code] }";
    }

    private sealed class CosFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("cos");
        private readonly MathInstance _host;

        public CosFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Cos(arguments.At(0));
        }

        public override string ToString() => "function cos() { [native code] }";
    }

    private sealed class CoshFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("cosh");
        private readonly MathInstance _host;

        public CoshFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Cosh(arguments.At(0));
        }

        public override string ToString() => "function cosh() { [native code] }";
    }

    private sealed class ExpFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("exp");
        private readonly MathInstance _host;

        public ExpFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Exp(arguments.At(0));
        }

        public override string ToString() => "function exp() { [native code] }";
    }

    private sealed class FloorFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("floor");
        private readonly MathInstance _host;

        public FloorFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Floor(arguments.At(0));
        }

        public override string ToString() => "function floor() { [native code] }";
    }

    private sealed class FroundFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("fround");
        private readonly MathInstance _host;

        public FroundFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Fround(arguments.At(0));
        }

        public override string ToString() => "function fround() { [native code] }";
    }

    private sealed class HypotFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("hypot");
        private readonly MathInstance _host;

        public HypotFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(2), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Hypot(arguments);
        }

        public override string ToString() => "function hypot() { [native code] }";
    }

    private sealed class ImulFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("imul");
        private readonly MathInstance _host;

        public ImulFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(2), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Imul(arguments.At(0), arguments.At(1));
        }

        public override string ToString() => "function imul() { [native code] }";
    }

    private sealed class LogFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("log");
        private readonly MathInstance _host;

        public LogFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Log(arguments.At(0));
        }

        public override string ToString() => "function log() { [native code] }";
    }

    private sealed class Log10Function : FunctionInstance
    {
        private static readonly JsString _name = new JsString("log10");
        private readonly MathInstance _host;

        public Log10Function(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Log10(arguments.At(0));
        }

        public override string ToString() => "function log10() { [native code] }";
    }

    private sealed class Log2Function : FunctionInstance
    {
        private static readonly JsString _name = new JsString("log2");
        private readonly MathInstance _host;

        public Log2Function(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Log2(arguments.At(0));
        }

        public override string ToString() => "function log2() { [native code] }";
    }

    private sealed class MaxFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("max");
        private readonly MathInstance _host;

        public MaxFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Max(arguments);
        }

        public override string ToString() => "function max() { [native code] }";
    }

    private sealed class MinFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("min");
        private readonly MathInstance _host;

        public MinFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Min(arguments);
        }

        public override string ToString() => "function min() { [native code] }";
    }

    private sealed class PowFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("pow");
        private readonly MathInstance _host;

        public PowFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(2), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Pow(arguments.At(0), arguments.At(1));
        }

        public override string ToString() => "function pow() { [native code] }";
    }

    private sealed class RandomFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("random");
        private readonly MathInstance _host;

        public RandomFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(0), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return _host.Random();
        }

        public override string ToString() => "function random() { [native code] }";
    }

    private sealed class RoundFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("round");
        private readonly MathInstance _host;

        public RoundFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Round(arguments.At(0));
        }

        public override string ToString() => "function round() { [native code] }";
    }

    private sealed class SignFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("sign");
        private readonly MathInstance _host;

        public SignFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Sign(arguments.At(0));
        }

        public override string ToString() => "function sign() { [native code] }";
    }

    private sealed class SinFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("sin");
        private readonly MathInstance _host;

        public SinFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Sin(arguments.At(0));
        }

        public override string ToString() => "function sin() { [native code] }";
    }

    private sealed class SinhFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("sinh");
        private readonly MathInstance _host;

        public SinhFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Sinh(arguments.At(0));
        }

        public override string ToString() => "function sinh() { [native code] }";
    }

    private sealed class SqrtFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("sqrt");
        private readonly MathInstance _host;

        public SqrtFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Sqrt(arguments.At(0));
        }

        public override string ToString() => "function sqrt() { [native code] }";
    }

    private sealed class TanFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("tan");
        private readonly MathInstance _host;

        public TanFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Tan(arguments.At(0));
        }

        public override string ToString() => "function tan() { [native code] }";
    }

    private sealed class TanhFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("tanh");
        private readonly MathInstance _host;

        public TanhFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Tanh(arguments.At(0));
        }

        public override string ToString() => "function tanh() { [native code] }";
    }

    private sealed class TruncFunction : FunctionInstance
    {
        private static readonly JsString _name = new JsString("trunc");
        private readonly MathInstance _host;

        public TruncFunction(MathInstance host) : base(host.Engine, host.Engine.Realm, _name)
        {
            _host = host;
            _prototype = host.Engine._originalIntrinsics.Function.PrototypeObject;
            _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
        }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
             return MathInstance.Trunc(arguments.At(0));
        }

        public override string ToString() => "function trunc() { [native code] }";
    }

}

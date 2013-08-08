using Xunit;

namespace Jint.Tests.Ecma
{
    public class Test_8_12_1 : EcmaTest
    {
        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyPropertyDoesNotExist()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_1.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyWritableConfigurableNonEnumerableOwnValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_10.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyWritableConfigurableEnumerableOwnValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_11.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonWritableNonConfigurableNonEnumerableInheritedValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_12.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonWritableNonConfigurableEnumerableInheritedValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_13.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonWritableConfigurableNonEnumerableInheritedValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_14.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyWritableNonConfigurableNonEnumerableInheritedValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_15.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonWritableConfigurableEnumerableInheritedValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_16.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyWritableNonConfigurableEnumerableInheritedValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_17.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyWritableConfigurableNonEnumerableInheritedValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_18.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyWritableConfigurableEnumerableInheritedValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_19.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyOldStyleOwnProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_2.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyLiteralOwnGetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_20.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyLiteralOwnSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_21.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyLiteralOwnGetterSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_22.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyLiteralInheritedGetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_23.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyLiteralInheritedSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_24.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyLiteralInheritedGetterSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_25.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonConfigurableNonEnumerableOwnGetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_26.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonConfigurableEnumerableOwnGetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_27.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyConfigurableNonEnumerableOwnGetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_28.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyConfigurableEnumerableOwnGetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_29.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyOldStyleInheritedProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_3.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonConfigurableNonEnumerableOwnSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_30.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonConfigurableEnumerableOwnSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_31.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyConfigurableNonEnumerableOwnSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_32.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyConfigurableEnumerableOwnSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_33.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonConfigurableNonEnumerableOwnGetterSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_34.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonConfigurableEnumerableOwnGetterSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_35.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyConfigurableNonEnumerableOwnGetterSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_36.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyConfigurableEnumerableOwnGetterSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_37.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonConfigurableNonEnumerableInheritedGetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_38.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonConfigurableEnumerableInheritedGetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_39.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonWritableNonConfigurableNonEnumerableOwnValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_4.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyConfigurableNonEnumerableInheritedGetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_40.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyConfigurableEnumerableInheritedGetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_41.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonConfigurableNonEnumerableInheritedSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_42.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonConfigurableEnumerableInheritedSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_43.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyConfigurableNonEnumerableInheritedSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_44.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyConfigurableEnumerableInheritedSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_45.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonConfigurableNonEnumerableInheritedGetterSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_46.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonConfigurableEnumerableInheritedGetterSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_47.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyConfigurableNonEnumerableInheritedGetterSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_48.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyConfigurableEnumerableInheritedGetterSetterProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_49.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonWritableNonConfigurableEnumerableOwnValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_5.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonWritableConfigurableNonEnumerableOwnValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_6.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyWritableNonConfigurableNonEnumerableOwnValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_7.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyNonWritableConfigurableEnumerableOwnValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_8.js", false);
        }

        [Fact]
        [Trait("Category", "8.12.1")]
        public void PropertiesHasownpropertyWritableNonConfigurableEnumerableOwnValueProperty()
        {
			RunTest(@"TestCases/ch08/8.12/8.12.1/8.12.1-1_9.js", false);
        }


    }
}

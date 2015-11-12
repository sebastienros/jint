using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Date;
using Jint.Native.Object;
using Jint.Native.RegExp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Jint.Tests.Runtime
{
    public class JsValueConversionTests
    {
        [Fact]
        public void ShouldBeAnArray()
        {
            var value = new JsValue(new ArrayInstance(null));
            Assert.Equal(false, value.IsBoolean());
            Assert.Equal(true, value.IsArray());
            Assert.Equal(false, value.IsDate());
            Assert.Equal(false, value.IsNull());
            Assert.Equal(false, value.IsNumber());
            Assert.Equal(true, value.IsObject());
            Assert.Equal(false, value.IsPrimitive());
            Assert.Equal(false, value.IsRegExp());
            Assert.Equal(false, value.IsString());
            Assert.Equal(false, value.IsUndefined());

            Assert.Equal(true, value.AsArray() != null);
        }

        [Fact]
        public void ShouldBeABoolean()
        {
            var value = new JsValue(true);
            Assert.Equal(true, value.IsBoolean());
            Assert.Equal(false, value.IsArray());
            Assert.Equal(false, value.IsDate());
            Assert.Equal(false, value.IsNull());
            Assert.Equal(false, value.IsNumber());
            Assert.Equal(false, value.IsObject());
            Assert.Equal(true, value.IsPrimitive());
            Assert.Equal(false, value.IsRegExp());
            Assert.Equal(false, value.IsString());
            Assert.Equal(false, value.IsUndefined());

            Assert.Equal(true, value.AsBoolean());
        }

        [Fact]
        public void ShouldBeADate()
        {
            var value = new JsValue(new DateInstance(null));
            Assert.Equal(false, value.IsBoolean());
            Assert.Equal(false, value.IsArray());
            Assert.Equal(true, value.IsDate());
            Assert.Equal(false, value.IsNull());
            Assert.Equal(false, value.IsNumber());
            Assert.Equal(true, value.IsObject());
            Assert.Equal(false, value.IsPrimitive());
            Assert.Equal(false, value.IsRegExp());
            Assert.Equal(false, value.IsString());
            Assert.Equal(false, value.IsUndefined());

            Assert.Equal(true, value.AsDate() != null);
        }

        [Fact]
        public void ShouldBeNull()
        {
            var value = Null.Instance;
            Assert.Equal(false, value.IsBoolean());
            Assert.Equal(false, value.IsArray());
            Assert.Equal(false, value.IsDate());
            Assert.Equal(true, value.IsNull());
            Assert.Equal(false, value.IsNumber());
            Assert.Equal(false, value.IsObject());
            Assert.Equal(true, value.IsPrimitive());
            Assert.Equal(false, value.IsRegExp());
            Assert.Equal(false, value.IsString());
            Assert.Equal(false, value.IsUndefined());
        }

        [Fact]
        public void ShouldBeANumber()
        {
            var value = new JsValue(2);
            Assert.Equal(false, value.IsBoolean());
            Assert.Equal(false, value.IsArray());
            Assert.Equal(false, value.IsDate());
            Assert.Equal(false, value.IsNull());
            Assert.Equal(true, value.IsNumber());
            Assert.Equal(2, value.AsNumber());
            Assert.Equal(false, value.IsObject());
            Assert.Equal(true, value.IsPrimitive());
            Assert.Equal(false, value.IsRegExp());
            Assert.Equal(false, value.IsString());
            Assert.Equal(false, value.IsUndefined());
        }

        [Fact]
        public void ShouldBeAnObject()
        {
            var value = new JsValue(new ObjectInstance(null));
            Assert.Equal(false, value.IsBoolean());
            Assert.Equal(false, value.IsArray());
            Assert.Equal(false, value.IsDate());
            Assert.Equal(false, value.IsNull());
            Assert.Equal(false, value.IsNumber());
            Assert.Equal(true, value.IsObject());
            Assert.Equal(true, value.AsObject() != null);
            Assert.Equal(false, value.IsPrimitive());
            Assert.Equal(false, value.IsRegExp());
            Assert.Equal(false, value.IsString());
            Assert.Equal(false, value.IsUndefined());
        }

        [Fact]
        public void ShouldBeARegExp()
        {
            var value = new JsValue(new RegExpInstance(null));
            Assert.Equal(false, value.IsBoolean());
            Assert.Equal(false, value.IsArray());
            Assert.Equal(false, value.IsDate());
            Assert.Equal(false, value.IsNull());
            Assert.Equal(false, value.IsNumber());
            Assert.Equal(true, value.IsObject());
            Assert.Equal(false, value.IsPrimitive());
            Assert.Equal(true, value.IsRegExp());
            Assert.Equal(true, value.AsRegExp() != null);
            Assert.Equal(false, value.IsString());
            Assert.Equal(false, value.IsUndefined());
        }

        [Fact]
        public void ShouldBeAString()
        {
            var value = new JsValue("a");
            Assert.Equal(false, value.IsBoolean());
            Assert.Equal(false, value.IsArray());
            Assert.Equal(false, value.IsDate());
            Assert.Equal(false, value.IsNull());
            Assert.Equal(false, value.IsNumber());
            Assert.Equal(false, value.IsObject());
            Assert.Equal(true, value.IsPrimitive());
            Assert.Equal(false, value.IsRegExp());
            Assert.Equal(true, value.IsString());
            Assert.Equal("a", value.AsString());
            Assert.Equal(false, value.IsUndefined());
        }

        [Fact]
        public void ShouldBeUndefined()
        {
            var value = Undefined.Instance;
            Assert.Equal(false, value.IsBoolean());
            Assert.Equal(false, value.IsArray());
            Assert.Equal(false, value.IsDate());
            Assert.Equal(false, value.IsNull());
            Assert.Equal(false, value.IsNumber());
            Assert.Equal(false, value.IsObject());
            Assert.Equal(true, value.IsPrimitive());
            Assert.Equal(false, value.IsRegExp());
            Assert.Equal(false, value.IsString());
            Assert.Equal(true, value.IsUndefined());
        }
    }
}

using System;
using Jint.Native;
using Jint.Runtime.Interop;
using Jint.Tests.Runtime.Domain;
using Xunit;

namespace Jint.Tests.Runtime
{
    public partial class InteropTests
    {
        [Fact]
        public void ShouldHideSpecificMembers()
        {
            var engine = new Engine(options => options.SetMemberAccessor((e, target, member) =>
            {
                if (target is HiddenMembers)
                {
                    if (member == nameof(HiddenMembers.Member2) || member == nameof(HiddenMembers.Method2))
                    {
                        return JsValue.Undefined;
                    }
                }

                return null;
            }));

            engine.SetValue("m", new HiddenMembers());

            Assert.Equal("Member1", engine.Evaluate("m.Member1").ToString());
            Assert.Equal("undefined", engine.Evaluate("m.Member2").ToString());
            Assert.Equal("Method1", engine.Evaluate("m.Method1()").ToString());
            // check the method itself, not its invokation as it would mean invoking "undefined"
            Assert.Equal("undefined", engine.Evaluate("m.Method2").ToString());
        }

        [Fact]
        public void ShouldOverrideMembers()
        {
            var engine = new Engine(options => options.SetMemberAccessor((e, target, member) =>
            {
                if (target is HiddenMembers && member == nameof(HiddenMembers.Member1))
                {
                    return "Orange";
                }

                return null;
            }));

            engine.SetValue("m", new HiddenMembers());

            Assert.Equal("Orange", engine.Evaluate("m.Member1").ToString());
        }

        [Fact]
        public void ShouldBeAbleToFilterMembers()
        {
            var engine = new Engine(options => options
                .SetTypeResolver(new TypeResolver
                {
                    MemberFilter = member => !Attribute.IsDefined(member, typeof(ObsoleteAttribute))
                })
            );

            engine.SetValue("m", new HiddenMembers());

            Assert.True(engine.Evaluate("m.Field1").IsUndefined());
            Assert.True(engine.Evaluate("m.Member1").IsUndefined());
            Assert.True(engine.Evaluate("m.Method1").IsUndefined());

            Assert.True(engine.Evaluate("m.Field2").IsString());
            Assert.True(engine.Evaluate("m.Member2").IsString());
            Assert.True(engine.Evaluate("m.Method2()").IsString());
        }

        [Fact]
        public void ShouldBeAbleToHideGetType()
        {
            var engine = new Engine(options => options
                .SetTypeResolver(new TypeResolver
                {
                    MemberFilter = member => !Attribute.IsDefined(member, typeof(ObsoleteAttribute))
                })
            );
            engine.SetValue("m", new HiddenMembers());

            Assert.True(engine.Evaluate("m.Method1").IsUndefined());

            // reflection could bypass some safeguards
            Assert.Equal("Method1", engine.Evaluate("m.GetType().GetMethod('Method1').Invoke(m, [])").AsString());

            // but not when we forbid GetType
            var hiddenGetTypeEngine = new Engine(options => options
                .SetTypeResolver(new TypeResolver
                {
                    MemberFilter = member => member.Name != nameof(GetType)
                })
            );
            hiddenGetTypeEngine.SetValue("m", new HiddenMembers());
            Assert.True(hiddenGetTypeEngine.Evaluate("m.GetType").IsUndefined());
        }

        [Fact]
        public void TypeReferenceShouldUseTypeResolverConfiguration()
        {
            var engine = new Engine(options =>
            {
                options.SetTypeResolver(new TypeResolver
                {
                    MemberFilter = member => !Attribute.IsDefined(member, typeof(ObsoleteAttribute))
                });
            });
            engine.SetValue("EchoService", TypeReference.CreateTypeReference(engine, typeof(EchoService)));
            Assert.Equal("anyone there", engine.Evaluate("EchoService.Echo('anyone there')").AsString());
            Assert.Equal("anyone there", engine.Evaluate("EchoService.echo('anyone there')").AsString());
            Assert.True(engine.Evaluate("EchoService.ECHO").IsUndefined());

            Assert.True(engine.Evaluate("EchoService.Hidden").IsUndefined());
        }

        private static class EchoService
        {
            public static string Echo(string message) => message;

            [Obsolete]
            public static string Hidden(string message) => message;
        }
    }
}
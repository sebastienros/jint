/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-75.js
 * @description Object.defineProperties will not throw TypeError if P.configurable is false, P.writalbe is false, P.value is NaN and properties.value is NaN (8.12.9 step 10.a.ii.1)
 */


function testcase() {

        var obj = {};
        var accessed = false;

        Object.defineProperty(obj, "foo", {
            value: NaN,
            writable: false,
            configurable: false
        });

        Object.defineProperties(obj, {
            foo: {
                value: NaN
            }
        });

        var verifyEnumerable = false;
        for (var p in obj) {
            if (p === "foo") {
                verifyEnumerable = true;
            }
        }

        obj.prop = "overrideData";
        var verifyValue = false;
        verifyValue = obj.foo !== obj.foo && isNaN(obj.foo);

        var desc = Object.getOwnPropertyDescriptor(obj, "foo");

        var verifyConfigurable = false;
        delete obj.foo;
        verifyConfigurable = obj.hasOwnProperty("foo");

        return verifyValue && !verifyEnumerable && verifyConfigurable;
    }
runTestCase(testcase);

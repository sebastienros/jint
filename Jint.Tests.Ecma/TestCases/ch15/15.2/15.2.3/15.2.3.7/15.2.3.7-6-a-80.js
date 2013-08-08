/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-80.js
 * @description Object.defineProperties will not throw TypeError when P.configurable is false, P.writalbe is false, properties.value and P.value are two strings with the same value (8.12.9 step 10.a.ii.1)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", {
            value: "abcd",
            writable: false,
            configurable: false 
        });

        Object.defineProperties(obj, {
            foo: {
                value: "abcd"
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", "abcd", false, false, false);
    }
runTestCase(testcase);

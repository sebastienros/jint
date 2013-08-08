/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-74.js
 * @description Object.defineProperties will not throw TypeError if P.configurable is false, P.writalbe is false, P.value is null and properties.value is null (8.12.9 step 10.a.ii.1)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { 
            value: null, 
            writable: false, 
            configurable: false 
        });

        Object.defineProperties(obj, {
            foo: {
                value: null
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", null, false, false, false);
    }
runTestCase(testcase);

/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.7/15.2.3.7-6-a-73.js
 * @description Object.defineProperties will not throw TypeError if P.configurable is false, P.writalbe is false, P.value is undefined and properties.value is undefined (8.12.9 step 10.a.ii.1)
 */


function testcase() {

        var obj = {};

        Object.defineProperty(obj, "foo", { 
            value: undefined, 
            writable: false, 
            configurable: false 
        });

        Object.defineProperties(obj, {
            foo: {
                value: undefined
            }
        });
        return dataPropertyAttributesAreCorrect(obj, "foo", undefined, false, false, false);
    }
runTestCase(testcase);
